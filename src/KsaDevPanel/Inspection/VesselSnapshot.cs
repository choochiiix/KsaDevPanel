using System.Collections.ObjectModel;

namespace KsaDevPanel.Inspection;

/// <summary>
/// Availability of a read-only vessel sample.  A snapshot is independent of
/// KSA objects and is safe to hand to a view after the host draw callback.
/// </summary>
public enum VesselSnapshotAvailability
{
    Available,
    NoActiveVessel,
    WorldTransition,
    Unavailable
}

/// <summary>Primitive data copied from the host-owned vessel during one draw.</summary>
public sealed record VesselReadData(
    string Id,
    string Name,
    string? ParentBody,
    string? Situation,
    VelocityReadData? Velocity,
    IReadOnlyList<PartReadData>? Parts);

/// <summary>Primitive velocity data; no game vector or frame object is retained.</summary>
public sealed record VelocityReadData(double X, double Y, double Z, string? Frame, string? Units);

/// <summary>Primitive part identity copied from a host-owned part tree.</summary>
public sealed record PartReadData(string Name, string? PartType = null);

/// <summary>
/// A stable, immutable vessel sample.  It contains no references to KSA
/// objects, spans, render handles, or mutable game collections.
/// </summary>
public sealed class VesselSnapshot
{
    private VesselSnapshot(
        VesselSnapshotAvailability availability,
        string? id,
        string? name,
        string? parentBody,
        string? situation,
        VelocitySnapshot? velocity,
        IReadOnlyList<PartSnapshot> parts,
        string? reason)
    {
        Availability = availability;
        Id = id;
        Name = name;
        ParentBody = parentBody;
        Situation = situation;
        Velocity = velocity;
        Parts = parts;
        Reason = reason;
    }

    public VesselSnapshotAvailability Availability { get; }
    public string? Id { get; }
    public string? Name { get; }
    public string? ParentBody { get; }
    public string? Situation { get; }
    public VelocitySnapshot? Velocity { get; }
    public IReadOnlyList<PartSnapshot> Parts { get; }
    public string? Reason { get; }

    public bool IsAvailable => Availability == VesselSnapshotAvailability.Available;

    public static VesselSnapshot Unavailable(
        VesselSnapshotAvailability availability,
        string? reason = null) => new(
            availability,
            null,
            null,
            null,
            null,
            null,
            EmptyParts,
            reason);

    internal static VesselSnapshot From(VesselReadData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var id = CleanRequired(data.Id, "unknown");
        var name = CleanRequired(data.Name, id);
        var parts = data.Parts is null
            ? EmptyParts
            : new ReadOnlyCollection<PartSnapshot>(data.Parts
                .Where(static p => p is not null)
                .Select(static p => new PartSnapshot(CleanRequired(p.Name, "unnamed"), CleanOptional(p.PartType)))
                .ToArray());

        var velocity = data.Velocity is null
            ? null
            : new VelocitySnapshot(
                data.Velocity.X,
                data.Velocity.Y,
                data.Velocity.Z,
                CleanOptional(data.Velocity.Frame),
                CleanOptional(data.Velocity.Units));

        return new VesselSnapshot(
            VesselSnapshotAvailability.Available,
            id,
            name,
            CleanOptional(data.ParentBody),
            CleanOptional(data.Situation),
            velocity,
            parts,
            null);
    }

    private static readonly IReadOnlyList<PartSnapshot> EmptyParts =
        new ReadOnlyCollection<PartSnapshot>(Array.Empty<PartSnapshot>());

    private static string CleanRequired(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record VelocitySnapshot(
    double X,
    double Y,
    double Z,
    string? Frame,
    string? Units);

public sealed record PartSnapshot(string Name, string? PartType);

/// <summary>Immutable text values ready for a UI renderer.</summary>
public sealed class VesselSnapshotDisplay
{
    internal VesselSnapshotDisplay(
        string status,
        string identity,
        string parentBody,
        string situation,
        string velocity,
        IReadOnlyList<string> parts)
    {
        Status = status;
        Identity = identity;
        ParentBody = parentBody;
        Situation = situation;
        Velocity = velocity;
        Parts = parts;
    }

    public string Status { get; }
    public string Identity { get; }
    public string ParentBody { get; }
    public string Situation { get; }
    public string Velocity { get; }
    public IReadOnlyList<string> Parts { get; }
}
