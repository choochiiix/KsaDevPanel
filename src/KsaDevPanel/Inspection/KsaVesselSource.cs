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
            "Controlled vessel",
            parent?.Id.ToString(),
            vehicle.Situation.ToString(),
            Velocity: null,
            Parts: parts);
    }
}
