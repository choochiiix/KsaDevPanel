using KsaDevPanel.Inspection;

namespace KsaDevPanel.Diagnostics;

/// <summary>
/// Immutable lifecycle snapshot for the workbench. It contains only bounded
/// text and monotonic timing values; no KSA object is retained.
/// </summary>
public sealed record RuntimeContextStatus(
    string SceneLabel,
    bool WorldReady,
    string ActiveVesselLabel,
    string? UnavailableReason,
    long? LastSuccessfulSampleTick,
    string? LastSuccessfulSnapshotVesselId,
    long ObservedAtTick,
    bool IsSampleStale,
    long? SampleAgeTicks,
    string WorldState,
    string VesselState)
{
    public bool HasSuccessfulSnapshot => LastSuccessfulSampleTick.HasValue;

    public static RuntimeContextStatus Create(
        SessionScene scene,
        bool worldReady,
        VesselSnapshot? snapshot,
        long nowTick,
        long? lastSuccessfulTick,
        long staleAfterTicks = 2_000,
        string? lastSuccessfulSnapshotVesselId = null)
    {
        if (nowTick < 0)
            throw new ArgumentOutOfRangeException(nameof(nowTick));
        if (staleAfterTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(staleAfterTicks));

        var inWorld = scene == SessionScene.World && worldReady;
        var usable = inWorld && snapshot?.IsAvailable == true;
        // A transient failed read may retain the last success while the same
        // world is still ready. The host clears it on scene/system/vessel change.
        long? sampleTick = inWorld && lastSuccessfulTick is >= 0 && lastSuccessfulTick <= nowTick
            ? lastSuccessfulTick
            : null;
        long? age = sampleTick.HasValue ? nowTick - sampleTick.Value : null;
        var stale = age.HasValue && age.Value > staleAfterTicks;

        var sceneLabel = scene switch
        {
            SessionScene.World => inWorld ? "World" : "World (not ready)",
            SessionScene.Editor => "Editor",
            _ => "Not loaded (menu/loading/transition)"
        };
        var worldState = inWorld
            ? "Ready"
            : scene == SessionScene.World ? "Waiting for system" : "No flight world";

        var vesselId = usable ? Normalize(snapshot!.Id) : null;
        var unavailableReason = usable ? null : GetUnavailableReason(scene, worldReady, snapshot);
        var activeVessel = vesselId ?? (snapshot?.Availability == VesselSnapshotAvailability.NoActiveVessel
            ? "No active vessel"
            : "Unavailable");
        var vesselState = usable ? "Ready" : unavailableReason ?? "Unavailable";

        return new RuntimeContextStatus(
            sceneLabel,
            inWorld,
            activeVessel,
            unavailableReason,
            sampleTick,
            sampleTick.HasValue ? Normalize(lastSuccessfulSnapshotVesselId) : null,
            nowTick,
            stale,
            age,
            worldState,
            vesselState);
    }

    private static string GetUnavailableReason(SessionScene scene, bool worldReady, VesselSnapshot? snapshot)
    {
        if (scene != SessionScene.World)
            return "No flight world";
        if (!worldReady)
            return "World is not ready";
        return snapshot?.Availability switch
        {
            VesselSnapshotAvailability.NoActiveVessel => "No active vessel",
            VesselSnapshotAvailability.WorldTransition => "World transition",
            VesselSnapshotAvailability.Unavailable => "Sample unavailable",
            _ => "Vessel unavailable"
        };
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > 96)
            trimmed = trimmed[..96];

        var chars = trimmed.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]))
                chars[i] = ' ';
        }
        return new string(chars);
    }
}

/// <summary>Projection used by the small ImGui lifecycle card.</summary>
public sealed record LifecycleHealthStatus(
    string Scene,
    bool WorldReady,
    string WorldState,
    string? ActiveVesselId,
    string VesselState,
    bool HasSuccessfulSnapshot,
    string? LastSuccessfulSnapshotVesselId,
    DateTimeOffset? LastSuccessfulSnapshotUtc,
    TimeSpan? SnapshotAge,
    bool IsSnapshotStale)
{
    public static LifecycleHealthStatus From(
        RuntimeContextStatus status,
        DateTimeOffset? lastSuccessfulSnapshotUtc = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        var activeVesselId = status.ActiveVesselLabel is "No active vessel" or "Unavailable"
            ? null
            : status.ActiveVesselLabel;
        return new LifecycleHealthStatus(
            status.SceneLabel,
            status.WorldReady,
            status.WorldState,
            activeVesselId,
            status.VesselState,
            status.HasSuccessfulSnapshot,
            status.LastSuccessfulSnapshotVesselId,
            status.HasSuccessfulSnapshot ? lastSuccessfulSnapshotUtc : null,
            status.SampleAgeTicks.HasValue ? TimeSpan.FromMilliseconds(status.SampleAgeTicks.Value) : null,
            status.IsSampleStale);
    }
}
