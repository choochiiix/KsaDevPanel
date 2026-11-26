using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace KsaDevPanel.Performance;

/// <summary>
/// Collects process CPU, host CPU, and memory readings without requiring a
/// game-facing API. CPU percentages need two successful samples to establish
/// their elapsed-time deltas.
/// </summary>
public sealed class SystemMetricsCollector
{
    private readonly object _sync = new();
    private TimeSpan? _previousProcessCpuTime;
    private long? _previousProcessTimestamp;
    private (ulong Idle, ulong Kernel, ulong User)? _previousSystemTimes;

    /// <summary>Captures the readings available for the current sampling interval.</summary>
    public SystemPerformanceSnapshot Sample()
    {
        var sampledAt = DateTimeOffset.UtcNow;
        double? processCpuPercent = null;
        double? processCpuCores = null;
        double? systemCpuPercent = null;
        string? cpuName = null;
        double? cpuCurrentSpeedMHz = null;
        long? workingSet = null;
        long? privateBytes = null;

        lock (_sync)
        {
            try
            {
                using var process = Process.GetCurrentProcess();

                // Process.TotalProcessorTime is cumulative CPU time across all
                // logical CPUs. Divide its delta by monotonic wall time to get
                // busy logical-core equivalents, then normalize by core count.
                TimeSpan cpuTime;
                try
                {
                    cpuTime = process.TotalProcessorTime;
                    var timestamp = Stopwatch.GetTimestamp();
                    if (_previousProcessCpuTime is { } previousCpu &&
                        _previousProcessTimestamp is { } previousTimestamp)
                    {
                        var wallSeconds = (timestamp - previousTimestamp) / (double)Stopwatch.Frequency;
                        var cpuSeconds = (cpuTime - previousCpu).TotalSeconds;
                        if (wallSeconds > 0 && cpuSeconds >= 0)
                        {
                            var cores = cpuSeconds / wallSeconds;
                            var logicalProcessorCount = Math.Max(1, Environment.ProcessorCount);
                            processCpuCores = Math.Clamp(cores, 0, logicalProcessorCount);
                            processCpuPercent = Math.Clamp(
                                cores * 100d / logicalProcessorCount,
                                0,
                                100);
                        }
                    }

                    // Advance only after a successful CPU-time read. A missing
                    // or invalid delta therefore recovers on the next sample.
                    _previousProcessCpuTime = cpuTime;
                    _previousProcessTimestamp = timestamp;
                }
                catch
                {
                    // Keep CPU unavailable for this sample; other metrics can
                    // still be returned.
                }

                // Working set is resident process memory; private bytes are
                // committed private process memory. Either reading may be
                // unavailable independently on restricted platforms.
                try { workingSet = process.WorkingSet64; } catch { }
                try { privateBytes = process.PrivateMemorySize64; } catch { }
            }
            catch
            {
                // Process inspection can fail during shutdown or under host
                // restrictions; return partial values rather than failing the poll.
            }

            systemCpuPercent = ReadSystemCpuPercent();
        }

        long? managedHeapBytes = null;
        try
        {
            // GC.GetTotalMemory(false) is an estimate of currently allocated
            // managed objects; it excludes native and reserved heap memory.
            managedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
        }
        catch { }

        var (totalMemory, availableMemory) = ReadPhysicalMemory();
        cpuName = ReadCpuName();
        cpuCurrentSpeedMHz = ReadCurrentCpuSpeedMHz();

        return new SystemPerformanceSnapshot(
            sampledAt,
            cpuName,
            cpuCurrentSpeedMHz,
            processCpuPercent,
            processCpuCores,
            systemCpuPercent,
            workingSet,
            privateBytes,
            managedHeapBytes,
            totalMemory,
            availableMemory);
    }

    private double? ReadSystemCpuPercent()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                return null;

            var current = (Idle: idle.ToUInt64(), Kernel: kernel.ToUInt64(), User: user.ToUInt64());
            var prior = _previousSystemTimes;
            _previousSystemTimes = current;
            if (prior is null)
                return null;

            // Windows kernel time includes idle time. Subtract idle delta from
            // kernel+user delta to estimate total host CPU busy percentage.
            var idleDelta = Delta(current.Idle, prior.Value.Idle);
            var kernelDelta = Delta(current.Kernel, prior.Value.Kernel);
            var userDelta = Delta(current.User, prior.Value.User);
            if (idleDelta is null || kernelDelta is null || userDelta is null)
                return null;

            var totalDelta = (double)kernelDelta.Value + userDelta.Value;
            if (totalDelta <= 0)
                return null;

            var busyDelta = Math.Max(0d, totalDelta - idleDelta.Value);
            return Math.Clamp(busyDelta * 100d / totalDelta, 0d, 100d);
        }
        catch
        {
            return null;
        }
    }

    private static (long? Total, long? Available) ReadPhysicalMemory()
    {
        if (!OperatingSystem.IsWindows())
            return (null, null);

        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status))
                return (null, null);

            return (ToNullableLong(status.TotalPhys), ToNullableLong(status.AvailPhys));
        }
        catch
        {
            return (null, null);
        }
    }

    private static string? ReadCpuName()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                writable: false);
            if (key is null)
                return null;

            var name = key.GetValue("ProcessorNameString") as string;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static double? ReadCurrentCpuSpeedMHz()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            const ushort allProcessorGroups = 0xFFFF;
            var processorCount = GetActiveProcessorCount(allProcessorGroups);
            if (processorCount == 0 || processorCount > int.MaxValue)
                return null;

            var entrySize = Marshal.SizeOf<ProcessorPowerInformation>();
            var bufferSize = checked(entrySize * (int)processorCount);
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                // ProcessorInformation returns one record per active logical
                // processor. CurrentMhz is the OS power-management reading;
                // this collector reports the arithmetic mean for the sample.
                var status = CallNtPowerInformation(
                    11, // ProcessorInformation
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)bufferSize);
                if (status != 0)
                    return null;

                double sumMHz = 0;
                for (var i = 0; i < (int)processorCount; i++)
                {
                    var offset = checked(i * entrySize);
                    var info = Marshal.PtrToStructure<ProcessorPowerInformation>(IntPtr.Add(buffer, offset));
                    // Zero frequency values indicate unavailable/unsupported
                    // processor information. Require every active processor
                    // to provide a valid reading for a meaningful aggregate.
                    if (info.CurrentMhz == 0 || info.MaxMhz == 0)
                        return null;

                    sumMHz += info.CurrentMhz;
                }

                var averageMHz = sumMHz / processorCount;
                return double.IsFinite(averageMHz) && averageMHz > 0 ? averageMHz : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private static ulong? Delta(ulong current, ulong previous) =>
        current >= previous ? current - previous : null;

    private static long? ToNullableLong(ulong value) =>
        value <= long.MaxValue ? (long)value : null;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetActiveProcessorCount(ushort groupNumber);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern int CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        IntPtr outputBuffer,
        uint outputBufferLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
