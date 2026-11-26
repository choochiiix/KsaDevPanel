using KSA;

namespace KsaDevPanel.Inspection;

/// <summary>
/// Narrow build-pinned adapter for KSA .5949. It is intentionally limited to
/// members whose presence was established by the metadata survey. Call it only
/// from the host-owned GUI callback; it must never be scheduled or retained.
/// </summary>
public sealed class KsaVesselSource : IVesselSource
{
    public VesselReadData? ReadActiveVessel()
    {
        var vehicle = Program.ControlledVehicle;
        if (vehicle is null || vehicle.IsDisposed)
            return null;

        // Vehicle.Id is the public inherited identity candidate established by
        // the .5949 metadata survey. It is displayed as a candidate identity
        // until save/load and split/merge behavior are tested.
        var partTree = vehicle.Parts;
        var id = vehicle.Id.ToString() ?? "controlled-vessel";
        var parent = vehicle.Parent;
        var velocity = vehicle.GetVelocityEcl();
        var metrics = new VesselMetricsReadData(
            TryReadScalar(() => Program.CurrentAltitudeKm),
            TryReadScalar(vehicle.GetSurfaceSpeed),
            TryReadScalar(() => vehicle.OrbitalSpeed),
            TryReadAcceleration(vehicle),
            TryReadScalar(() => vehicle.TotalMass),
            TryReadScalar(() => vehicle.PropellantMass),
            TryReadScalar(() => vehicle.GetManualThrottle()));
        var parts = new List<PartReadData>();
        if (partTree is not null)
        {
            foreach (var part in partTree.Parts)
            {
                if (part is null)
                    continue;
                parts.Add(new PartReadData(part.DisplayName?.ToString() ?? "unnamed"));
            }
        }

        return new VesselReadData(
            id,
            id,
            parent?.Id.ToString(),
            vehicle.Situation.ToString(),
            new VelocityReadData(
                velocity.X,
                velocity.Y,
                velocity.Z,
                Frame: "ECL",
                Units: "native units/s; scale unverified"),
            Parts: parts,
            Metrics: metrics);
    }

    private static double? TryReadScalar(Func<double> read)
    {
        try
        {
            var value = read();
            return double.IsFinite(value) ? value : null;
        }
        catch (Exception exception) when (IsTransientReadFailure(exception))
        {
            return null;
        }
    }

    private static AccelerationReadData? TryReadAcceleration(Vehicle vehicle)
    {
        try
        {
            var acceleration = vehicle.AccelerationBody;
            if (!double.IsFinite(acceleration.X) ||
                !double.IsFinite(acceleration.Y) ||
                !double.IsFinite(acceleration.Z))
                return null;
            return new AccelerationReadData(
                acceleration.X,
                acceleration.Y,
                acceleration.Z,
                Frame: "body-relative",
                Units: "native units/s²; scale unverified");
        }
        catch (Exception exception) when (IsTransientReadFailure(exception))
        {
            return null;
        }
    }

    private static bool IsTransientReadFailure(Exception exception) =>
        exception is InvalidOperationException or ObjectDisposedException or NullReferenceException;
}
