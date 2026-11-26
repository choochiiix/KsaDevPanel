using System;
using System.Globalization;
using Brutal.ImGuiApi;
using Brutal.ImPlotApi;
using Brutal.Numerics;
using KsaDevPanel.Performance;

namespace KsaDevPanel.UI;

/// <summary>
/// Draws a read-only history graph for one sampled performance metric.
/// </summary>
public static class PerformanceMetricGraph
{
    private const int MaximumSamples = 300;
    private const float PlotHeight = 220f;

    public static void Draw(PerformanceMetricSeries? series)
    {
        if (series is null)
        {
            ImGui.TextDisabled("No performance metric history is available.");
            return;
        }

        var points = series.Points;
        var pointCount = points?.Count ?? 0;
        var start = Math.Max(0, pointCount - MaximumSamples);
        var count = pointCount - start;
        var label = string.IsNullOrWhiteSpace(series.Label) ? series.Id : series.Label;
        var unit = string.IsNullOrWhiteSpace(series.Unit) ? "unit unavailable" : series.Unit;

        ImGui.TextWrapped(label);
        DrawSummary(points, pointCount, start, count, unit);

        if (count == 0)
        {
            ImGui.TextDisabled("No samples are available yet.");
            return;
        }

        var xValues = new double[count];
        var yValues = new double[count];
        var firstTime = points![start].SampledAt;
        var hasFiniteValue = false;
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        for (var i = 0; i < count; i++)
        {
            var point = points[start + i];
            xValues[i] = (point.SampledAt - firstTime).TotalSeconds;
            yValues[i] = point.Value.GetValueOrDefault(double.NaN);
            if (point.Value is not double value || !double.IsFinite(value))
                continue;

            hasFiniteValue = true;
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        if (!hasFiniteValue)
        {
            ImGui.TextDisabled("No finite samples are available in this history window.");
            return;
        }

        float2? plotSize = new float2(0f, PlotHeight);
        var plotTitle = $"{label}##KsaDevPanelMetric_{SafeId(series.Id)}";
        if (!ImPlot.BeginPlot(plotTitle, in plotSize, ImPlotFlags.NoLegend))
            return;

        ImPlot.SetupAxes("Time (s)", unit, ImPlotAxisFlags.None, ImPlotAxisFlags.None);
        DrawFiniteSegments(series.Id, xValues, yValues);
        ImPlot.EndPlot();
    }

    private static void DrawSummary(
        System.Collections.Generic.IReadOnlyList<PerformanceHistoryPoint>? points,
        int pointCount,
        int start,
        int count,
        string unit)
    {
        double? current = pointCount == 0 ? null : points![^1].Value;
        double minimum = double.PositiveInfinity;
        double maximum = double.NegativeInfinity;
        for (var i = start; i < start + count; i++)
        {
            if (points![i].Value is not double value || !double.IsFinite(value))
                continue;
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        var currentText = current is double currentValue && double.IsFinite(currentValue)
            ? FormatValue(currentValue)
            : "unavailable";
        var minimumText = double.IsFinite(minimum) ? FormatValue(minimum) : "unavailable";
        var maximumText = double.IsFinite(maximum) ? FormatValue(maximum) : "unavailable";
        ImGui.TextWrapped(
            $"Current {currentText} · Min {minimumText} · Max {maximumText} {unit}  |  Last {count} samples");
    }

    private static void DrawFiniteSegments(string id, double[] xValues, double[] yValues)
    {
        var segmentStart = 0;
        var segmentNumber = 0;
        while (segmentStart < yValues.Length)
        {
            while (segmentStart < yValues.Length && !double.IsFinite(yValues[segmentStart]))
                segmentStart++;
            if (segmentStart >= yValues.Length)
                break;

            var segmentEnd = segmentStart + 1;
            while (segmentEnd < yValues.Length && double.IsFinite(yValues[segmentEnd]))
                segmentEnd++;

            var segmentLength = segmentEnd - segmentStart;
            if (segmentLength > 0)
            {
                var segmentLabel = $"##{SafeId(id)}_segment_{segmentNumber}";
                var segmentX = xValues.AsSpan(segmentStart, segmentLength);
                var segmentY = yValues.AsSpan(segmentStart, segmentLength);
                if (segmentLength == 1)
                    ImPlot.PlotScatter(segmentLabel, segmentX, segmentY, ImPlotScatterFlags.None, 0);
                else
                    ImPlot.PlotLine(segmentLabel, segmentX, segmentY, ImPlotLineFlags.None, 0);
            }

            segmentNumber++;
            segmentStart = segmentEnd;
        }
    }

    private static string FormatValue(double value) =>
        value.ToString("G5", CultureInfo.InvariantCulture);

    private static string SafeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "metric";

        var safe = new char[id.Length];
        for (var i = 0; i < id.Length; i++)
            safe[i] = char.IsLetterOrDigit(id[i]) || id[i] is '_' or '-' ? id[i] : '_';
        return new string(safe);
    }
}
