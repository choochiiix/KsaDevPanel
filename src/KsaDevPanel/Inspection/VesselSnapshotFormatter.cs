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
                new VesselVelocityDisplay("—", "—", "—", "—", "—", "—"),
                UnavailableMetrics,
                EmptyStrings);
        }

        var velocity = snapshot.Velocity is null
            ? new VesselVelocityDisplay("Unavailable", "Unavailable", "Unavailable", "Unavailable", "unavailable", "")
            : new VesselVelocityDisplay(
                string.Create(CultureInfo.InvariantCulture,
                    $"{Math.Sqrt(snapshot.Velocity.X * snapshot.Velocity.X + snapshot.Velocity.Y * snapshot.Velocity.Y + snapshot.Velocity.Z * snapshot.Velocity.Z):0.0}"),
                FormatComponent(snapshot.Velocity.X),
                FormatComponent(snapshot.Velocity.Y),
                FormatComponent(snapshot.Velocity.Z),
                snapshot.Velocity.Frame ?? "frame unavailable",
                snapshot.Velocity.Units ?? "units unavailable");

        var metrics = snapshot.Metrics is null
            ? UnavailableMetrics
            : FormatMetrics(snapshot.Metrics);

        var parts = new ReadOnlyCollection<string>(snapshot.Parts
            .Select(static p => string.IsNullOrWhiteSpace(p.PartType) ? p.Name : $"{p.Name} ({p.PartType})")
            .ToArray());

        return new VesselSnapshotDisplay(
            "Ready",
            snapshot.Name ?? snapshot.Id ?? "Unknown vessel",
            snapshot.ParentBody ?? "Unavailable",
            snapshot.Situation ?? "Unavailable",
            velocity,
            metrics,
            parts);
    }

    private static VesselMetricsDisplay FormatMetrics(VesselMetricsSnapshot metrics)
    {
        var acceleration = metrics.Acceleration;
        var accelerationMagnitude = acceleration is null
            ? "Unavailable"
            : FormatWithUnits(
                Magnitude(acceleration.X, acceleration.Y, acceleration.Z),
                acceleration.Units ?? "native units/s²; scale unverified");

        return new VesselMetricsDisplay(
            FormatWithUnits(metrics.AltitudeKm, "km"),
            FormatWithUnits(metrics.SurfaceSpeed, "native units/s; scale unverified"),
            FormatWithUnits(metrics.OrbitalSpeed, "native units/s; scale unverified"),
            accelerationMagnitude,
            acceleration is null ? "Unavailable" : FormatComponent(acceleration.X),
            acceleration is null ? "Unavailable" : FormatComponent(acceleration.Y),
            acceleration is null ? "Unavailable" : FormatComponent(acceleration.Z),
            acceleration?.Frame ?? "frame unavailable",
            acceleration?.Units ?? "units unavailable",
            FormatWithUnits(metrics.TotalMass, "native mass units; scale unverified"),
            FormatWithUnits(metrics.PropellantMass, "native mass units; scale unverified"),
            FormatNumber(metrics.ManualThrottle, "0.00"));
    }

    private static string FormatWithUnits(double? value, string units)
    {
        var formatted = FormatNumber(value, "0.0");
        return formatted == "Unavailable" ? formatted : $"{formatted} {units}";
    }

    private static string FormatNumber(double? value, string format)
    {
        if (value is not { } number || !double.IsFinite(number))
            return "Unavailable";
        return number.ToString(format, CultureInfo.InvariantCulture);
    }

    private static double? Magnitude(double x, double y, double z)
    {
        var magnitude = Math.Sqrt(x * x + y * y + z * z);
        return double.IsFinite(magnitude) ? magnitude : null;
    }

    private static readonly IReadOnlyList<string> EmptyStrings =
        new ReadOnlyCollection<string>(Array.Empty<string>());

    private static readonly VesselMetricsDisplay UnavailableMetrics = new(
        "Unavailable", "Unavailable", "Unavailable", "Unavailable",
        "Unavailable", "Unavailable", "Unavailable", "frame unavailable",
        "units unavailable", "Unavailable", "Unavailable", "Unavailable");

    private static string FormatComponent(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);
}
