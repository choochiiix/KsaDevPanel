using System.Globalization;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KsaDevPanel.Performance;

namespace KsaDevPanel.UI;

/// <summary>
/// Overview and selected-metric history graph for the Performance tab.
/// </summary>
public static class PerformanceTabView
{
    private const string Unavailable = "Unavailable";

    public static string? Draw(
        PerformanceSnapshot? current,
        PerformanceHistorySnapshot? history,
        string? selectedMetricId)
    {
        ImGui.SeparatorText("Performance history");

        if (selectedMetricId is not null && history is not null)
        {
            var updatedSelection = PerformanceMetricPicker.Draw(history, selectedMetricId);
            if (updatedSelection == selectedMetricId)
                PerformanceMetricGraph.Draw(FindMetric(history, selectedMetricId));
            return updatedSelection;
        }

        DrawHardwareSummary(current, history);
        ImGui.SeparatorText("Select a metric to graph");

        if (history is null || history.Metrics.Count == 0)
        {
            ImGui.TextDisabled("Collecting performance history…");
            return null;
        }

        ImGui.TextDisabled("Up to five minutes of in-memory history at one sample per second. History resets when KSA exits.");
        return PerformanceMetricPicker.Draw(history, selectedMetricId: null);
    }

    private static void DrawHardwareSummary(
        PerformanceSnapshot? current,
        PerformanceHistorySnapshot? history)
    {
        ImGui.SeparatorText("Hardware");
        float2? tableSize = null;
        if (ImGui.BeginTable("KsaDevPanelPerformanceHardware", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in tableSize, 0f))
        {
            DrawDetailRow("CPU model", FormatName(current?.System.CpuName));
            DrawDetailRow("CPU average current speed", FormatFrequencyMHz(current?.System.CpuCurrentSpeedMHz));
            DrawDetailRow("GPU model", FormatName(current?.Gpu?.GpuName));
            DrawDetailRow("GPU status", string.IsNullOrWhiteSpace(current?.Gpu?.Status)
                ? Unavailable
                : current.Gpu.Status);
            DrawDetailRow("Latest sample", current is null ? Unavailable : FormatAge(current.SampledAt));
            DrawDetailRow("History sample", history is null || history.SampledAt == DateTimeOffset.MinValue
                ? Unavailable
                : FormatAge(history.SampledAt));
            ImGui.EndTable();
        }

        ImGui.TextDisabled("CPU and GPU percentages are collector readings. GPU engine activity is not a diagnosis of a bottleneck.");
    }

    private static PerformanceMetricSeries? FindMetric(PerformanceHistorySnapshot history, string id)
    {
        foreach (var metric in history.Metrics)
        {
            if (string.Equals(metric.Id, id, System.StringComparison.Ordinal))
                return metric;
        }

        return null;
    }

    private static void DrawDetailRow(string label, string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(value);
    }

    private static string FormatName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Unavailable : value;

    private static string FormatFrequencyMHz(double? value)
    {
        if (value is not double frequencyMHz || !double.IsFinite(frequencyMHz) || frequencyMHz <= 0)
            return Unavailable;
        return frequencyMHz >= 1000
            ? $"{(frequencyMHz / 1000d).ToString("F2", CultureInfo.InvariantCulture)} GHz ({frequencyMHz.ToString("F0", CultureInfo.InvariantCulture)} MHz)"
            : $"{frequencyMHz.ToString("F0", CultureInfo.InvariantCulture)} MHz";
    }

    private static string FormatAge(DateTimeOffset sampledAt)
    {
        var age = DateTimeOffset.UtcNow - sampledAt.ToUniversalTime();
        if (age < TimeSpan.Zero)
            return "Sample time is in the future";

        var seconds = age.TotalSeconds;
        var ageText = seconds < 60
            ? $"{seconds.ToString("F1", CultureInfo.InvariantCulture)} s"
            : age.TotalMinutes < 60
                ? $"{age.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture)} min"
                : $"{age.TotalHours.ToString("F1", CultureInfo.InvariantCulture)} h";
        return ageText;
    }
}
