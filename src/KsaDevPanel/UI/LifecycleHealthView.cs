using System;
using System.Globalization;
using Brutal.ImGuiApi;
using KsaDevPanel.Diagnostics;

namespace KsaDevPanel.UI;

/// <summary>
/// Renders the immutable lifecycle status supplied by the host. This view is
/// deliberately passive: it never reads KSA objects or changes simulation
/// state, and it does not own a tab or window.
/// </summary>
public static class LifecycleHealthView
{
    private const string Unavailable = "Unavailable";

    /// <summary>
    /// Draws the lifecycle and sampling health card. The status is expected to
    /// contain only primitive, already-normalized values from the host adapter.
    /// </summary>
    public static void Draw(LifecycleHealthStatus? status)
    {
        ImGui.SeparatorText("Scene and lifecycle health");

        if (status is null)
        {
            ImGui.TextDisabled("Lifecycle status is not available yet.");
            ImGui.TextWrapped("The panel will show the active scene, world readiness, vessel context, and snapshot freshness after the host observes its first frame.");
            return;
        }

        if (ImGui.BeginTable("KsaDevPanelLifecycleHealth", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, null, 0f))
        {
            Detail("Scene", Display(status.Scene));
            Detail("World", status.WorldReady ? "Ready" : Display(status.WorldState));
            Detail("Active vessel", Display(status.ActiveVesselId));
            Detail("Vessel context", Display(status.VesselState));
            Detail("Last snapshot", FormatSnapshot(status));
            Detail("Sampled vessel", Display(status.LastSuccessfulSnapshotVesselId));
            Detail("Snapshot age", FormatAge(status));
            ImGui.EndTable();
        }

        if (status.IsSnapshotStale)
        {
            ImGui.TextDisabled("Snapshot is stale. The panel is retaining the last known status until a fresh world sample arrives.");
        }
        else if (!status.HasSuccessfulSnapshot)
        {
            ImGui.TextDisabled("No successful vessel snapshot has been observed in this session.");
        }
        else if (string.IsNullOrWhiteSpace(status.ActiveVesselId))
        {
            ImGui.TextDisabled("The current vessel sample is unavailable; the last successful sample is shown for context.");
        }
        else
        {
            ImGui.TextDisabled("Snapshot is fresh for the current lifecycle context.");
        }
    }

    private static void Detail(string label, string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(value);
    }

    private static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Unavailable : value.Trim();

    private static string FormatSnapshot(LifecycleHealthStatus status)
    {
        if (!status.HasSuccessfulSnapshot)
            return Unavailable;
        return status.LastSuccessfulSnapshotUtc is DateTimeOffset timestamp
            ? timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
            : "Observed";
    }

    private static string FormatAge(LifecycleHealthStatus status)
    {
        if (!status.HasSuccessfulSnapshot)
            return Unavailable;
        if (status.SnapshotAge is not TimeSpan age)
            return "Unknown";
        if (age < TimeSpan.Zero)
            return "Clock skew";
        if (age.TotalSeconds < 60)
            return $"{age.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s";
        if (age.TotalMinutes < 60)
            return $"{age.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture)} min";
        return $"{age.TotalHours.ToString("F1", CultureInfo.InvariantCulture)} h";
    }
}
