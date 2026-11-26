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
    IReadOnlyList<PartReadData>? Parts,
    VesselMetricsReadData? Metrics = null);

/// <summary>Primitive velocity data; no game vector or frame object is retained.</summary>
public sealed record VelocityReadData(double X, double Y, double Z, string? Frame, string? Units);

/// <summary>Primitive values copied from the active vehicle during one host draw.</summary>
public sealed record VesselMetricsReadData(
    double? AltitudeKm,
    double? SurfaceSpeed,
    double? OrbitalSpeed,
    AccelerationReadData? Acceleration,
    double? TotalMass,
    double? PropellantMass,
    double? ManualThrottle);

/// <summary>Primitive acceleration vector; no game vector or frame object is retained.</summary>
public sealed record AccelerationReadData(double X, double Y, double Z, string? Frame, string? Units);

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
        VesselMetricsSnapshot? metrics,
        IReadOnlyList<PartSnapshot> parts,
        string? reason)
    {
        Availability = availability;
        Id = id;
        Name = name;
        ParentBody = parentBody;
        Situation = situation;
        Velocity = velocity;
        Metrics = metrics;
        Parts = parts;
        Reason = reason;
    }

    public VesselSnapshotAvailability Availability { get; }
    public string? Id { get; }
    public string? Name { get; }
    public string? ParentBody { get; }
    public string? Situation { get; }
    public VelocitySnapshot? Velocity { get; }
    public VesselMetricsSnapshot? Metrics { get; }
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

        var metrics = data.Metrics is null
            ? null
            : new VesselMetricsSnapshot(
                data.Metrics.AltitudeKm,
                data.Metrics.SurfaceSpeed,
                data.Metrics.OrbitalSpeed,
                data.Metrics.Acceleration is null
                    ? null
                    : new AccelerationSnapshot(
                        data.Metrics.Acceleration.X,
                        data.Metrics.Acceleration.Y,
                        data.Metrics.Acceleration.Z,
                        CleanOptional(data.Metrics.Acceleration.Frame),
                        CleanOptional(data.Metrics.Acceleration.Units)),
                data.Metrics.TotalMass,
                data.Metrics.PropellantMass,
                data.Metrics.ManualThrottle);

        return new VesselSnapshot(
            VesselSnapshotAvailability.Available,
            id,
            name,
            CleanOptional(data.ParentBody),
            CleanOptional(data.Situation),
            velocity,
            metrics,
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

public sealed record VesselMetricsSnapshot(
    double? AltitudeKm,
    double? SurfaceSpeed,
    double? OrbitalSpeed,
    AccelerationSnapshot? Acceleration,
    double? TotalMass,
    double? PropellantMass,
    double? ManualThrottle);

public sealed record AccelerationSnapshot(
    double X,
    double Y,
    double Z,
    string? Frame,
    string? Units);

public sealed record PartSnapshot(string Name, string? PartType);

public sealed record VesselVelocityDisplay(
    string Speed,
    string X,
    string Y,
    string Z,
    string Frame,
    string Units);

public sealed record VesselMetricsDisplay(
    string Altitude,
    string SurfaceSpeed,
    string OrbitalSpeed,
    string AccelerationMagnitude,
    string AccelerationX,
    string AccelerationY,
    string AccelerationZ,
    string AccelerationFrame,
    string AccelerationUnits,
    string TotalMass,
    string PropellantMass,
    string ManualThrottle);

/// <summary>Immutable text values ready for a UI renderer.</summary>
public sealed class VesselSnapshotDisplay
{
    internal VesselSnapshotDisplay(
        string status,
        string identity,
        string parentBody,
        string situation,
        VesselVelocityDisplay velocity,
        VesselMetricsDisplay metrics,
        IReadOnlyList<string> parts)
    {
        Status = status;
        Identity = identity;
        ParentBody = parentBody;
        Situation = situation;
        Velocity = velocity;
        Metrics = metrics;
        Parts = parts;
    }

    public string Status { get; }
    public string Identity { get; }
    public string ParentBody { get; }
    public string Situation { get; }
    public VesselVelocityDisplay Velocity { get; }
    public VesselMetricsDisplay Metrics { get; }
    public IReadOnlyList<string> Parts { get; }
}
