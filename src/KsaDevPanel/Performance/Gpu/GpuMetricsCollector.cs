using System.Runtime.InteropServices;

namespace KsaDevPanel.Performance.Gpu;

/// <summary>
/// Reads Windows GPU Engine utilization for the current process through PDH.
/// Sampling is limited to once per second. DXGI adapter and process memory
/// readings are returned only when a unique engine LUID matches an installed
/// adapter; Vulkan driver reporting may vary.
/// </summary>
public sealed class GpuMetricsCollector : IDisposable
{
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhFmtNoCap100 = 0x00000800;
    private const uint PdhCStatusValidData = 0x00000000;
    private const uint PdhCStatusNewData = 0x00000001;
    private const string WildcardCounterPath = @"\GPU Engine(*)\Utilization Percentage";
    private static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NoCounterRetryInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ErrorRetryInterval = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly int _processId = Environment.ProcessId;
    private readonly List<Counter> _counters = [];
    private IntPtr _query;
    private IntPtr _adapter3;
    private DateTimeOffset _lastSampleAt;
    private DateTimeOffset _nextInitializationAttempt;
    private GpuPerformanceSnapshot? _lastSnapshot;
    private string? _initializationStatus;
    private string? _gpuName;
    private string? _adapterStatus;
    private bool _disposed;

    /// <summary>Returns the most recent reading, sampling no faster than once per second.</summary>
    public GpuPerformanceSnapshot Sample()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (_disposed)
                return Unavailable(now, "GPU telemetry collector has been disposed.");

            if (_lastSnapshot is not null && now - _lastSampleAt < MinimumSampleInterval)
                return _lastSnapshot;

            EnsureInitialized();
            if (_query == IntPtr.Zero || _counters.Count == 0)
            {
                _lastSampleAt = now;
                return _lastSnapshot = Unavailable(now, _initializationStatus ?? "GPU engine counters are unavailable.");
            }

            uint collectStatus;
            try
            {
                collectStatus = Native.PdhCollectQueryData(_query);
            }
            catch (Exception ex)
            {
                CloseQuery();
                _initializationStatus = $"GPU engine counter collection failed ({ex.GetType().Name}).";
                _nextInitializationAttempt = now + ErrorRetryInterval;
                _lastSampleAt = now;
                return _lastSnapshot = Unavailable(now, _initializationStatus);
            }

            if (collectStatus != 0)
            {
                CloseQuery();
                _initializationStatus = $"GPU engine counter collection failed (PDH 0x{collectStatus:X8}).";
                _nextInitializationAttempt = now + ErrorRetryInterval;
                _lastSampleAt = now;
                return _lastSnapshot = Unavailable(now, _initializationStatus);
            }

            var engines = new List<GpuEngineUsage>(_counters.Count);
            var failedCounters = 0;
            foreach (var counter in _counters)
            {
                uint status;
                FormattedCounterValue value;
                try
                {
                    status = Native.PdhGetFormattedCounterValue(counter.Handle, PdhFmtDouble | PdhFmtNoCap100, out _, out value);
                }
                catch
                {
                    failedCounters++;
                    continue;
                }

                if (status != 0 || (value.CStatus != PdhCStatusValidData && value.CStatus != PdhCStatusNewData)
                    || !double.IsFinite(value.DoubleValue))
                {
                    failedCounters++;
                    continue;
                }

                // Per-engine utilization is a percentage. Guard against invalid driver output
                // while preserving the counter's documented 0..100 interpretation.
                engines.Add(new GpuEngineUsage(counter.EngineName, Math.Clamp(value.DoubleValue, 0d, 100d)));
            }

            _lastSampleAt = now;
            var memoryInfo = ReadMemoryInfo();
            var adapterStatus = _adapterStatus;
            if (_adapter3 != IntPtr.Zero && memoryInfo is null)
                adapterStatus = $"{adapterStatus} DXGI process video-memory usage/budget could not be queried on this system.";
            string? snapshotStatus = _counters.Count == 0
                ? "Windows reported no GPU Engine counters for this process. Adapter identity and process video-memory usage/budget are unavailable because no process-to-adapter association could be verified."
                : engines.Count == 0
                    ? $"GPU Engine counters were found, but the driver did not provide valid utilization readings yet. {adapterStatus}"
                    : failedCounters == 0
                        ? $"Per-engine utilization from Windows GPU Engine counters. {adapterStatus}"
                        : $"Some GPU Engine counters did not return valid readings ({failedCounters} of {_counters.Count}). {adapterStatus}";

            return _lastSnapshot = new GpuPerformanceSnapshot(
                now,
                GpuName: _gpuName,
                Engines: engines,
                LocalUsageBytes: memoryInfo?.LocalUsageBytes,
                LocalBudgetBytes: memoryInfo?.LocalBudgetBytes,
                NonLocalUsageBytes: memoryInfo?.NonLocalUsageBytes,
                NonLocalBudgetBytes: memoryInfo?.NonLocalBudgetBytes,
                Status: snapshotStatus);
        }
    }

    private void EnsureInitialized()
    {
        var now = DateTimeOffset.UtcNow;
        if (_query != IntPtr.Zero && _counters.Count > 0)
            return;

        if (now < _nextInitializationAttempt)
            return;

        CloseQuery();
        _initializationStatus = null;
        _adapterStatus = null;
        _gpuName = null;

        if (!OperatingSystem.IsWindows())
        {
            _initializationStatus = "Windows GPU Engine counters are available only on Windows.";
            _nextInitializationAttempt = now + ErrorRetryInterval;
            return;
        }

        var status = Native.PdhOpenQueryW(IntPtr.Zero, UIntPtr.Zero, out _query);
        if (status != 0)
        {
            _query = IntPtr.Zero;
            _initializationStatus = $"Could not open Windows GPU Engine counters (PDH 0x{status:X8}).";
            _nextInitializationAttempt = now + ErrorRetryInterval;
            return;
        }

        try
        {
            foreach (var path in ExpandCounterPaths())
            {
                if (!TryGetProcessEngineName(path, _processId, out var engineName, out var instanceName))
                    continue;

                status = Native.PdhAddEnglishCounterW(_query, path, UIntPtr.Zero, out var counterHandle);
                if (status == 0)
                    _counters.Add(new Counter(counterHandle, engineName, instanceName));
            }

            if (_counters.Count == 0)
            {
                _initializationStatus = "Windows GPU Engine counters are unavailable for this process or the installed driver exposes none.";
                _nextInitializationAttempt = now + NoCounterRetryInterval;
            }
            else
                ResolveAdapter();
        }
        catch (Exception ex)
        {
            CloseQuery();
            _initializationStatus = $"Could not enumerate Windows GPU Engine counters ({ex.GetType().Name}).";
            _nextInitializationAttempt = now + ErrorRetryInterval;
        }
    }

    private static IEnumerable<string> ExpandCounterPaths()
    {
        uint characterCount = 0;
        var status = Native.PdhExpandWildCardPathW(IntPtr.Zero, WildcardCounterPath, IntPtr.Zero, ref characterCount, 0);
        if (status != PdhMoreData || characterCount == 0)
            yield break;

        var buffer = Marshal.AllocHGlobal(checked((int)characterCount * sizeof(char)));
        try
        {
            status = Native.PdhExpandWildCardPathW(IntPtr.Zero, WildcardCounterPath, buffer, ref characterCount, 0);
            if (status != 0)
                yield break;

            var start = 0;
            var previousWasNull = false;
            for (var index = 0; index < characterCount; index++)
            {
                var character = (char)Marshal.ReadInt16(buffer, index * sizeof(char));
                if (character == '\0')
                {
                    if (previousWasNull)
                        yield break;

                    var length = index - start;
                    if (length > 0)
                    {
                        var chars = new char[length];
                        Marshal.Copy(IntPtr.Add(buffer, start * sizeof(char)), chars, 0, length);
                        yield return new string(chars);
                    }

                    start = index + 1;
                    previousWasNull = true;
                }
                else
                {
                    previousWasNull = false;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool TryGetProcessEngineName(string path, int processId, out string engineName, out string instanceName)
    {
        engineName = string.Empty;
        instanceName = string.Empty;
        // Windows names instances like pid_1234_luid_..._phys_0_eng_0_engtype_3D.
        // Match the complete PID token to avoid accidental prefix matches.
        var instanceStart = path.IndexOf('(');
        var instanceEnd = path.IndexOf(')', instanceStart + 1);
        if (instanceStart < 0 || instanceEnd <= instanceStart)
            return false;

        var instance = path[(instanceStart + 1)..instanceEnd];
        var pidToken = $"pid_{processId}_";
        if (!instance.Contains(pidToken, StringComparison.OrdinalIgnoreCase))
            return false;

        instanceName = instance;
        var engineType = ReadInstanceToken(instance, "engtype_") ?? "GPU";
        var physicalEngine = ReadInstanceToken(instance, "phys_");
        var engineIndex = ReadInstanceToken(instance, "eng_");
        engineName = physicalEngine is not null && engineIndex is not null
            ? $"{engineType} (physical {physicalEngine}, engine {engineIndex})"
            : engineType;
        return true;
    }

    private static string? ReadInstanceToken(string instance, string token)
    {
        var start = instance.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += token.Length;
        var end = instance.IndexOf('_', start);
        if (end < 0)
            end = instance.Length;
        return end > start ? instance[start..end] : null;
    }

    private void ResolveAdapter()
    {
        var luids = new HashSet<Luid>();
        foreach (var counter in _counters)
        {
            if (!TryParseLuid(counter.InstanceName, out var luid))
            {
                _adapterStatus = "Adapter name and video-memory usage/budget are unavailable because Windows did not expose a parseable GPU Engine LUID for every process counter.";
                return;
            }

            luids.Add(luid);
        }

        if (luids.Count != 1)
        {
            _adapterStatus = "Adapter name and video-memory usage/budget are unavailable because this process has GPU Engine counters on multiple adapters.";
            return;
        }

        var targetLuid = luids.Single();
        var status = Dxgi.TryFindAdapter(targetLuid, out var adapterName, out _adapter3);
        if (status != AdapterLookupStatus.Found)
        {
            _adapterStatus = status switch
            {
                AdapterLookupStatus.Unsupported => "Adapter name and video-memory usage/budget are unavailable because DXGI adapter enumeration is unsupported on this Windows version.",
                AdapterLookupStatus.NotFound => "Adapter name and video-memory usage/budget are unavailable because the process GPU Engine LUID did not match an installed DXGI adapter.",
                AdapterLookupStatus.Ambiguous => "Adapter name and video-memory usage/budget are unavailable because the process GPU Engine LUID matched multiple DXGI adapters.",
                _ => "Adapter name and video-memory usage/budget are unavailable because DXGI could not verify a unique adapter match."
            };
            return;
        }

        _gpuName = adapterName;
        _adapterStatus = _adapter3 == IntPtr.Zero
            ? "Adapter identity matched the current process GPU Engine LUID to one DXGI adapter, but process video-memory usage/budget are unavailable because IDXGIAdapter3 is unsupported."
            : "Adapter identity matched the current process GPU Engine LUID to one DXGI adapter. DXGI video-memory usage/budget are process-specific; Vulkan driver reporting may vary.";
    }

    private (long LocalUsageBytes, long LocalBudgetBytes, long NonLocalUsageBytes, long NonLocalBudgetBytes)? ReadMemoryInfo()
    {
        if (_adapter3 == IntPtr.Zero)
            return null;

        try
        {
            if (!Dxgi.TryQueryVideoMemoryInfo(_adapter3, MemorySegmentGroup.Local, out var local)
                || !Dxgi.TryQueryVideoMemoryInfo(_adapter3, MemorySegmentGroup.NonLocal, out var nonLocal))
                return null;

            return (ToLong(local.CurrentUsage), ToLong(local.Budget), ToLong(nonLocal.CurrentUsage), ToLong(nonLocal.Budget));
        }
        catch
        {
            return null;
        }
    }

    private static long ToLong(ulong value) => value > long.MaxValue ? long.MaxValue : (long)value;

    private static bool TryParseLuid(string engineName, out Luid luid)
    {
        luid = default;
        var marker = engineName.IndexOf("luid_0x", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
            return false;

        var parts = engineName[(marker + 7)..].Split('_');
        if (parts.Length < 2 || !uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var high)
            || !uint.TryParse(parts[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? parts[1][2..] : parts[1], System.Globalization.NumberStyles.HexNumber, null, out var low))
            return false;

        // The first LUID half is the signed HighPart; the second is LowPart.
        luid = new Luid(low, unchecked((int)high));
        return true;
    }

    private GpuPerformanceSnapshot Unavailable(DateTimeOffset sampledAt, string status) =>
        new(sampledAt, null, Array.Empty<GpuEngineUsage>(), null, null, null, null, status);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            CloseQuery();
            _lastSnapshot = null;
        }
    }

    private void CloseQuery()
    {
        if (_query != IntPtr.Zero)
        {
            try
            {
                Native.PdhCloseQuery(_query);
            }
            catch
            {
                // Continue releasing the independently owned DXGI adapter reference.
            }

            _query = IntPtr.Zero;
        }

        _counters.Clear();
        if (_adapter3 != IntPtr.Zero)
        {
            Marshal.Release(_adapter3);
            _adapter3 = IntPtr.Zero;
        }
    }

    private sealed record Counter(IntPtr Handle, string EngineName, string InstanceName);

    private readonly record struct Luid(uint LowPart, int HighPart);

    private enum MemorySegmentGroup : uint
    {
        Local = 0,
        NonLocal = 1
    }

    private enum AdapterLookupStatus
    {
        Found,
        Unsupported,
        NotFound,
        Ambiguous,
        Failed
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public Luid AdapterLuid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiQueryVideoMemoryInfo
    {
        public ulong Budget;
        public ulong CurrentUsage;
        public ulong AvailableForReservation;
        public ulong CurrentReservation;
    }

    private static class Dxgi
    {
        private static readonly Guid Factory1Id = new("770aae78-f26f-4dba-a829-253c83d1b387");
        private static readonly Guid Adapter3Id = new("645967A4-1392-4310-A798-8053CE3E93FD");
        private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
        private const uint EnumAdapters1Slot = 12;
        private const uint GetDesc1Slot = 10;
        private const uint QueryVideoMemoryInfoSlot = 14;

        internal static AdapterLookupStatus TryFindAdapter(Luid targetLuid, out string? adapterName, out IntPtr adapter3)
        {
            adapterName = null;
            adapter3 = IntPtr.Zero;
            IntPtr factory = IntPtr.Zero;
            IntPtr foundAdapter = IntPtr.Zero;
            try
            {
                var factoryId = Factory1Id;
                var result = Native.CreateDXGIFactory1(ref factoryId, out factory);
                if (result < 0 || factory == IntPtr.Zero)
                    return AdapterLookupStatus.Unsupported;

                var enumerate = GetMethod<EnumAdapters1Delegate>(factory, EnumAdapters1Slot);
                var matchCount = 0;
                for (uint index = 0; ; index++)
                {
                    result = enumerate(factory, index, out var adapter);
                    if (result == DxgiErrorNotFound)
                        break;
                    if (result < 0 || adapter == IntPtr.Zero)
                        return AdapterLookupStatus.Failed;

                    try
                    {
                        var getDesc = GetMethod<GetDesc1Delegate>(adapter, GetDesc1Slot);
                        if (getDesc(adapter, out var description) >= 0 && description.AdapterLuid == targetLuid)
                        {
                            matchCount++;
                            if (foundAdapter != IntPtr.Zero)
                            {
                                Marshal.Release(foundAdapter);
                                foundAdapter = IntPtr.Zero;
                            }
                            foundAdapter = adapter;
                            adapter = IntPtr.Zero;
                            adapterName = description.Description?.TrimEnd('\0');
                        }
                    }
                    finally
                    {
                        if (adapter != IntPtr.Zero)
                            Marshal.Release(adapter);
                    }
                }

                if (matchCount == 0)
                    return AdapterLookupStatus.NotFound;
                if (matchCount != 1)
                {
                    adapterName = null;
                    return AdapterLookupStatus.Ambiguous;
                }

                try
                {
                    var iid = Adapter3Id;
                    Marshal.QueryInterface(foundAdapter, in iid, out adapter3);
                }
                finally
                {
                    Marshal.Release(foundAdapter);
                    foundAdapter = IntPtr.Zero;
                }

                return AdapterLookupStatus.Found;
            }
            catch (DllNotFoundException)
            {
                return AdapterLookupStatus.Unsupported;
            }
            catch (EntryPointNotFoundException)
            {
                return AdapterLookupStatus.Unsupported;
            }
            catch
            {
                if (adapter3 != IntPtr.Zero)
                {
                    Marshal.Release(adapter3);
                    adapter3 = IntPtr.Zero;
                }

                adapterName = null;
                return AdapterLookupStatus.Failed;
            }
            finally
            {
                if (foundAdapter != IntPtr.Zero)
                    Marshal.Release(foundAdapter);
                if (factory != IntPtr.Zero)
                    Marshal.Release(factory);
            }
        }

        internal static bool TryQueryVideoMemoryInfo(IntPtr adapter3, MemorySegmentGroup group, out DxgiQueryVideoMemoryInfo info)
        {
            var query = GetMethod<QueryVideoMemoryInfoDelegate>(adapter3, QueryVideoMemoryInfoSlot);
            return query(adapter3, 0, group, out info) >= 0;
        }

        private static T GetMethod<T>(IntPtr instance, uint slot) where T : Delegate
        {
            var vtable = Marshal.ReadIntPtr(instance);
            var method = Marshal.ReadIntPtr(vtable, checked((int)slot * IntPtr.Size));
            return Marshal.GetDelegateForFunctionPointer<T>(method);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnumAdapters1Delegate(IntPtr self, uint index, out IntPtr adapter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDesc1Delegate(IntPtr self, out DxgiAdapterDesc1 description);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryVideoMemoryInfoDelegate(IntPtr self, uint nodeIndex, MemorySegmentGroup group, out DxgiQueryVideoMemoryInfo info);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedCounterValue
    {
        public uint CStatus;
        public double DoubleValue;
    }

    private static class Native
    {
        [DllImport("dxgi.dll", EntryPoint = "CreateDXGIFactory1")]
        internal static extern int CreateDXGIFactory1(ref Guid interfaceId, out IntPtr factory);

        [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode)]
        internal static extern uint PdhOpenQueryW(IntPtr dataSource, UIntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", EntryPoint = "PdhExpandWildCardPathW", CharSet = CharSet.Unicode)]
        internal static extern uint PdhExpandWildCardPathW(IntPtr dataSource, string path, IntPtr buffer, ref uint bufferSize, uint flags);

        [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode)]
        internal static extern uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, UIntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll", EntryPoint = "PdhCollectQueryData")]
        internal static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue")]
        internal static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint counterType, out FormattedCounterValue value);

        [DllImport("pdh.dll", EntryPoint = "PdhCloseQuery")]
        internal static extern uint PdhCloseQuery(IntPtr query);
    }
}
