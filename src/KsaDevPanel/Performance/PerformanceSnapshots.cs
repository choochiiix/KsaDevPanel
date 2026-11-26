namespace KsaDevPanel.Performance;

/// <summary>
/// Immutable process and host resource readings captured at one sample time.
/// Percent values are nullable when the collector cannot calculate a valid sample.
/// ProcessCpuPercent is normalized against total logical processor capacity;
/// ProcessCpuCores retains the unnormalized number of fully busy logical CPUs.
/// </summary>
public sealed record SystemPerformanceSnapshot(
    DateTimeOffset SampledAt,
    string? CpuName,
    double? CpuCurrentSpeedMHz,
    double? ProcessCpuPercent,
    double? ProcessCpuCores,
    double? SystemCpuPercent,
    long? ProcessWorkingSetBytes,
    long? ProcessPrivateBytes,
    long? ManagedHeapBytes,
    long? TotalPhysicalMemoryBytes,
    long? AvailablePhysicalMemoryBytes);

/// <summary>
/// Per-engine GPU utilization and process video-memory budget readings.
/// A missing value means the platform/driver did not provide that metric.
/// </summary>
public sealed record GpuPerformanceSnapshot(
    DateTimeOffset SampledAt,
    string? GpuName,
    IReadOnlyList<GpuEngineUsage> Engines,
    long? LocalUsageBytes,
    long? LocalBudgetBytes,
    long? NonLocalUsageBytes,
    long? NonLocalBudgetBytes,
    string? Status);

public sealed record GpuEngineUsage(string Engine, double UtilizationPercent);

/// <summary>
/// Combined immutable view consumed by the UI. Platform collectors may fail
/// independently so CPU/memory remains useful if GPU telemetry is unavailable.
/// </summary>
public sealed record PerformanceSnapshot(
    DateTimeOffset SampledAt,
    SystemPerformanceSnapshot System,
    GpuPerformanceSnapshot? Gpu);
