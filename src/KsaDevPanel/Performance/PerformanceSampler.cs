using System.Diagnostics;
using KsaDevPanel.Performance.Gpu;

namespace KsaDevPanel.Performance;

/// <summary>
/// Polls operating-system performance data away from the game/UI callback and
/// publishes the latest immutable snapshot for the Performance tab.
/// </summary>
public sealed class PerformanceSampler : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly SystemMetricsCollector _systemMetrics = new();
    private readonly GpuMetricsCollector _gpuMetrics = new();
    private readonly PerformanceHistoryBuffer _historyBuffer = new();
    private readonly CancellationTokenSource _shutdown = new();
    private PerformanceSnapshot? _latest;
    private PerformanceHistorySnapshot? _history;
    private int _disposeRequested;

    public PerformanceSampler()
    {
        _ = Task.Run(() => RunAsync(_shutdown.Token));
    }

    public PerformanceSnapshot? Latest => Volatile.Read(ref _latest);

    public PerformanceHistorySnapshot? History => Volatile.Read(ref _history);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
            return;

        try
        {
            _shutdown.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The worker can finish and release the source while shutdown races
            // with a final cancellation request.
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(SampleInterval);
            SampleOnce();
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                SampleOnce();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal unload path.
        }
        catch (Exception exception)
        {
            // Keep a broken performance collector from taking down the mod.
            // Preserve the failure category without publishing machine paths.
            try
            {
                Publish(CreateUnavailableSnapshot(exception.GetType().Name));
            }
            catch
            {
                Volatile.Write(ref _latest, CreateUnavailableSnapshot(exception.GetType().Name));
            }
        }
        finally
        {
            try { _gpuMetrics.Dispose(); } catch { }
            _shutdown.Dispose();
        }
    }

    private void SampleOnce()
    {
        SystemPerformanceSnapshot system;
        try
        {
            system = _systemMetrics.Sample();
        }
        catch (Exception exception)
        {
            system = CreateUnavailableSystemSnapshot();
            Debug.WriteLine($"[KsaDevPanel] system performance sample unavailable: {exception.GetType().Name}");
        }

        GpuPerformanceSnapshot? gpu;
        try
        {
            gpu = _gpuMetrics.Sample();
        }
        catch (Exception exception)
        {
            gpu = new GpuPerformanceSnapshot(
                DateTimeOffset.UtcNow,
                GpuName: null,
                Engines: Array.Empty<GpuEngineUsage>(),
                LocalUsageBytes: null,
                LocalBudgetBytes: null,
                NonLocalUsageBytes: null,
                NonLocalBudgetBytes: null,
                Status: $"GPU metrics unavailable ({exception.GetType().Name}).");
        }

        Publish(new PerformanceSnapshot(DateTimeOffset.UtcNow, system, gpu));
    }

    private void Publish(PerformanceSnapshot snapshot)
    {
        _historyBuffer.Append(snapshot);
        Volatile.Write(ref _latest, snapshot);
        Volatile.Write(ref _history, _historyBuffer.Snapshot());
    }

    private static PerformanceSnapshot CreateUnavailableSnapshot(string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var gpu = new GpuPerformanceSnapshot(
            now,
            GpuName: null,
            Engines: Array.Empty<GpuEngineUsage>(),
            LocalUsageBytes: null,
            LocalBudgetBytes: null,
            NonLocalUsageBytes: null,
            NonLocalBudgetBytes: null,
            Status: $"GPU metrics unavailable ({reason}).");
        return new PerformanceSnapshot(now, CreateUnavailableSystemSnapshot(now), gpu);
    }

    private static SystemPerformanceSnapshot CreateUnavailableSystemSnapshot(DateTimeOffset? sampledAt = null) => new(
        sampledAt ?? DateTimeOffset.UtcNow,
        CpuName: null,
        CpuCurrentSpeedMHz: null,
        ProcessCpuPercent: null,
        ProcessCpuCores: null,
        SystemCpuPercent: null,
        ProcessWorkingSetBytes: null,
        ProcessPrivateBytes: null,
        ManagedHeapBytes: null,
        TotalPhysicalMemoryBytes: null,
        AvailablePhysicalMemoryBytes: null);
}
