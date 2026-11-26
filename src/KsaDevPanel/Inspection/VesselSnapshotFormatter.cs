using System.Collections.ObjectModel;
using System.Globalization;

namespace KsaDevPanel.Inspection;

/// <summary>Pure formatting for the UI; contains no KSA references.</summary>
public static class VesselSnapshotFormatter
{
    public static VesselSnapshotDisplay Format(VesselSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.IsAvailable)
        {
            var unavailable = string.IsNullOrWhiteSpace(snapshot.Reason)
                ? "Unavailable"
                : $"Unavailable: {snapshot.Reason}";
            return new VesselSnapshotDisplay(
                unavailable,
                "—",
                "—",
                "—",
                "—",
                EmptyStrings);
        }

        var velocity = snapshot.Velocity is null
            ? "Unavailable"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"({snapshot.Velocity.X:0.###}, {snapshot.Velocity.Y:0.###}, {snapshot.Velocity.Z:0.###}) " +
                $"{snapshot.Velocity.Frame ?? "frame unavailable"}, {snapshot.Velocity.Units ?? "units unavailable"}");

        var parts = new ReadOnlyCollection<string>(snapshot.Parts
            .Select(static p => string.IsNullOrWhiteSpace(p.PartType) ? p.Name : $"{p.Name} ({p.PartType})")
            .ToArray());

        return new VesselSnapshotDisplay(
            "Ready",
            snapshot.Name ?? snapshot.Id ?? "Unknown vessel",
            snapshot.ParentBody ?? "Unavailable",
            snapshot.Situation ?? "Unavailable",
            velocity,
            parts);
    }

    private static readonly IReadOnlyList<string> EmptyStrings =
        new ReadOnlyCollection<string>(Array.Empty<string>());
}
