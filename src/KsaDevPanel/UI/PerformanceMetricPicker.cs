using System.Globalization;
using Brutal.ImGuiApi;
using KsaDevPanel.Performance;

namespace KsaDevPanel.UI;

/// <summary>
/// Read-only metric selection view for performance history.
/// </summary>
public static class PerformanceMetricPicker
{
    private const string Unavailable = "Unavailable";

    /// <summary>
    /// Draws the metric list or the selected metric summary. Returns a newly
    /// selected metric ID, null when Back is clicked, or the existing selection.
    /// </summary>
    public static string? Draw(PerformanceHistorySnapshot history, string? selectedMetricId)
    {
        if (selectedMetricId is not null)
        {
            if (ImGui.SmallButton("Back"))
                return null;

            var selected = FindMetric(history, selectedMetricId);
            if (selected is null)
            {
                ImGui.TextDisabled(Unavailable);
                return selectedMetricId;
            }

            ImGui.SeparatorText(selected.Label);
            ImGui.TextWrapped(FormatMetricValue(selected));
            return selectedMetricId;
        }

        if (history.Metrics.Count == 0)
        {
            ImGui.TextDisabled(Unavailable);
            return null;
        }

        foreach (var metric in history.Metrics)
        {
            var label = string.IsNullOrWhiteSpace(metric.Label) ? metric.Id : metric.Label;
            var value = FormatMetricValue(metric);
            if (ImGui.SmallButton($"{label}: {value}##{metric.Id}"))
                return metric.Id;
        }

        return null;
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

    private static string FormatMetricValue(PerformanceMetricSeries metric)
    {
        var value = metric.CurrentValue is double current && double.IsFinite(current)
            ? current.ToString("G", CultureInfo.InvariantCulture)
            : Unavailable;
        return string.IsNullOrWhiteSpace(metric.Unit) ? value : $"{value} {metric.Unit}";
    }
}
