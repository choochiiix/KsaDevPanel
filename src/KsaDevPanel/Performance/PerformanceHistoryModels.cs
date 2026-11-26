namespace KsaDevPanel.Performance;

public sealed record PerformanceHistoryPoint(DateTimeOffset SampledAt, double? Value);

public sealed record PerformanceMetricSeries(
    string Id,
    string Label,
    string Unit,
    IReadOnlyList<PerformanceHistoryPoint> Points)
{
    public double? CurrentValue => Points.Count == 0 ? null : Points[^1].Value;
}

public sealed record PerformanceHistorySnapshot(
    DateTimeOffset SampledAt,
    IReadOnlyList<PerformanceMetricSeries> Metrics);
