namespace KsaDevPanel.Performance;

/// <summary>
/// Keeps a bounded, in-memory history of the performance sampler readings.
/// All series share the same timestamps so charts remain aligned when a
/// metric is unavailable or a GPU engine first appears later in the session.
/// </summary>
public sealed class PerformanceHistoryBuffer
{
    public const int DefaultCapacity = 300;
    public const int MaximumCapacity = 3600;

    private const double BytesPerGiB = 1024d * 1024d * 1024d;
    private readonly object _sync = new();
    private readonly int _capacity;
    private readonly Queue<DateTimeOffset> _sampleTimes = new();
    private readonly Dictionary<string, SeriesBuffer> _series = new(StringComparer.Ordinal);
    private readonly List<string> _seriesOrder = [];

    /// <summary>
    /// Creates a history buffer. Capacity is limited to 1..3600 samples to
    /// keep memory use predictable even when configured by a caller.
    /// </summary>
    public PerformanceHistoryBuffer(int capacity = DefaultCapacity)
    {
        _capacity = Math.Clamp(capacity, 1, MaximumCapacity);

        AddSeries("process-cpu", "Process CPU", "%");
        AddSeries("system-cpu", "System CPU", "%");
        AddSeries("cpu-frequency", "Average CPU frequency", "GHz");
        AddSeries("process-working-set", "Process working set", "GiB");
        AddSeries("process-private-memory", "Process private memory", "GiB");
        AddSeries("managed-heap", "Managed heap", "GiB");
        AddSeries("system-ram-used", "System RAM used", "GiB");
        AddSeries("system-ram-total", "System RAM total", "GiB");
        AddSeries("system-ram-available", "System RAM available", "GiB");
        AddSeries("gpu-local-usage", "GPU local memory usage", "GiB");
        AddSeries("gpu-local-budget", "GPU local memory budget", "GiB");
        AddSeries("gpu-non-local-usage", "GPU non-local memory usage", "GiB");
        AddSeries("gpu-non-local-budget", "GPU non-local memory budget", "GiB");
    }

    /// <summary>Adds one performance sample, retaining only the newest samples.</summary>
    public void Append(PerformanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            _sampleTimes.Enqueue(snapshot.SampledAt);
            while (_sampleTimes.Count > _capacity)
                _sampleTimes.Dequeue();

            var system = snapshot.System;
            var gpu = snapshot.Gpu;
            var values = new Dictionary<string, double?>(StringComparer.Ordinal)
            {
                ["process-cpu"] = ValidPercent(system.ProcessCpuPercent),
                ["system-cpu"] = ValidPercent(system.SystemCpuPercent),
                ["cpu-frequency"] = PositiveFinite(system.CpuCurrentSpeedMHz) is { } mhz ? mhz / 1000d : null,
                ["process-working-set"] = BytesToGiB(system.ProcessWorkingSetBytes),
                ["process-private-memory"] = BytesToGiB(system.ProcessPrivateBytes),
                ["managed-heap"] = BytesToGiB(system.ManagedHeapBytes),
                ["system-ram-used"] = SystemRamUsedGiB(system.TotalPhysicalMemoryBytes, system.AvailablePhysicalMemoryBytes),
                ["system-ram-total"] = BytesToGiB(system.TotalPhysicalMemoryBytes),
                ["system-ram-available"] = BytesToGiB(system.AvailablePhysicalMemoryBytes),
                ["gpu-local-usage"] = BytesToGiB(gpu?.LocalUsageBytes),
                ["gpu-local-budget"] = BytesToGiB(gpu?.LocalBudgetBytes),
                ["gpu-non-local-usage"] = BytesToGiB(gpu?.NonLocalUsageBytes),
                ["gpu-non-local-budget"] = BytesToGiB(gpu?.NonLocalBudgetBytes)
            };

            var engineValues = new Dictionary<string, double?>(StringComparer.Ordinal);
            if (gpu?.Engines is not null)
            {
                foreach (var engine in gpu.Engines)
                {
                    if (string.IsNullOrWhiteSpace(engine.Engine))
                        continue;

                    var key = EngineSeriesId(engine.Engine);
                    var percent = ValidPercent(engine.UtilizationPercent);
                    if (engineValues.TryGetValue(key, out var previous) && previous is { } prior && percent is { } current)
                        engineValues[key] = (prior + current) / 2d;
                    else
                        engineValues[key] = percent;

                    if (!_series.ContainsKey(key))
                    {
                        AddSeries(key, engine.Engine, "%");
                        foreach (var sampledAt in _sampleTimes.Take(_sampleTimes.Count - 1))
                            _series[key].Append(sampledAt, null);
                    }
                }
            }

            foreach (var id in _seriesOrder)
            {
                var value = engineValues.TryGetValue(id, out var engineValue)
                    ? engineValue
                    : values.GetValueOrDefault(id);
                _series[id].Append(snapshot.SampledAt, value);
            }
        }
    }

    /// <summary>Returns an immutable snapshot with copied point collections.</summary>
    public PerformanceHistorySnapshot Snapshot()
    {
        lock (_sync)
        {
            var sampledAt = _sampleTimes.Count == 0 ? DateTimeOffset.MinValue : _sampleTimes.Last();
            var metrics = new PerformanceMetricSeries[_seriesOrder.Count];
            for (var i = 0; i < _seriesOrder.Count; i++)
            {
                var series = _series[_seriesOrder[i]];
                metrics[i] = new PerformanceMetricSeries(
                    series.Id,
                    series.Label,
                    series.Unit,
                    Array.AsReadOnly(series.CopyPoints()));
            }

            return new PerformanceHistorySnapshot(sampledAt, Array.AsReadOnly(metrics));
        }
    }

    private static string EngineSeriesId(string engine) => $"gpu-engine:{engine}";

    private void AddSeries(string id, string label, string unit)
    {
        _series.Add(id, new SeriesBuffer(id, label, unit, _capacity));
        _seriesOrder.Add(id);
    }

    private static double? ValidPercent(double? value) =>
        value is { } number && double.IsFinite(number) && number >= 0d && number <= 100d ? number : null;

    private static double? PositiveFinite(double? value) =>
        value is { } number && double.IsFinite(number) && number > 0d ? number : null;

    private static double? BytesToGiB(long? bytes) =>
        bytes is { } count && count >= 0 ? count / BytesPerGiB : null;

    private static double? SystemRamUsedGiB(long? totalBytes, long? availableBytes)
    {
        if (totalBytes is not { } total || availableBytes is not { } available ||
            total < 0 || available < 0 || available > total)
            return null;

        return (total - available) / BytesPerGiB;
    }

    private sealed class SeriesBuffer(string id, string label, string unit, int capacity)
    {
        private readonly Queue<PerformanceHistoryPoint> _points = new(capacity);

        public string Id { get; } = id;
        public string Label { get; } = label;
        public string Unit { get; } = unit;

        public void Append(DateTimeOffset sampledAt, double? value)
        {
            _points.Enqueue(new PerformanceHistoryPoint(sampledAt, value));
            while (_points.Count > capacity)
                _points.Dequeue();
        }

        public PerformanceHistoryPoint[] CopyPoints() => _points.ToArray();
    }
}
