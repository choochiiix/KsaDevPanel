using KSA;

namespace KsaDevPanel.OrbitEditing;

/// <summary>Converts editor units to the pinned game's orbit input contract.</summary>
internal static class KsaOrbitAdapter
{
    private const double MetresPerKm = 1_000;
    private const double RadiansPerDegree = Math.PI / 180;

    public static IReadOnlyList<OrbitBodySnapshot> ReadBodies(CelestialSystem system)
    {
        var bodies = new List<OrbitBodySnapshot>();
        system.ForEach(item =>
        {
            if (Describe(item) is { } body)
                bodies.Add(body);
        });
        return bodies.OrderBy(body => body.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string Apply(CelestialSystem system, Vehicle vessel, string bodyId, OrbitElements elements)
    {
        if (!Program.IsLoaded || Program.IsEditorOpen ||
            !ReferenceEquals(Universe.CurrentSystem, system) ||
            !ReferenceEquals(Program.ControlledVehicle, vessel) || vessel.IsDisposed)
            return "The active vessel or world changed. Review the orbit and try again.";

        if (Universe.SimulationSpeed != 1 || Universe.IsAutoWarpActive)
            return "Return to 1× time and stop auto warp before teleporting.";

        var target = system.Get(bodyId);
        if (target is not IParentBody parent || Describe(target) is not { } body)
            return "The selected body is no longer available.";

        try
        {
            var state = OrbitModel.Calculate(elements with { Body = body });
            var phase = state.TrueAnomalyDeg * RadiansPerDegree;
            var eccentricAnomaly = Math.Atan2(
                Math.Sqrt(1 - state.Eccentricity * state.Eccentricity) * Math.Sin(phase),
                state.Eccentricity + Math.Cos(phase));
            var meanAnomaly = OrbitModel.NormalizeRadians(eccentricAnomaly - state.Eccentricity * Math.Sin(eccentricAnomaly));
            var secondsSincePeriapsis = meanAnomaly / (2 * Math.PI) * state.PeriodSeconds;
            var data = new OrbitData(
                Universe.GetElapsedTime() - secondsSincePeriapsis,
                parent.Mass,
                (body.RadiusKm + elements.PeriapsisAltitudeKm) * MetresPerKm,
                state.Eccentricity,
                elements.InclinationDeg * RadiansPerDegree,
                OrbitModel.NormalizeDegrees(elements.LongitudeAscendingNodeDeg) * RadiansPerDegree,
                OrbitModel.NormalizeDegrees(elements.ArgumentOfPeriapsisDeg) * RadiansPerDegree);
            var orbit = Orbit.Create(parent, data, vessel.OrbitColor);
            if (!orbit.IsBound() || !double.IsFinite(orbit.Period))
                return "KSA did not construct a finite, bound orbit.";

            InputEvents.TeleportInputBuffer.Add(new InputEvents.TeleportInputData
            {
                Vehicle = vessel,
                Orbit = orbit
            });
            return "Teleport queued. Check the vessel's new orbit in the stock map.";
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    private static OrbitBodySnapshot? Describe(Astronomical item)
    {
        if (item is not IParentBody parent ||
            !double.IsFinite(parent.Mass) || parent.Mass <= 0 ||
            !double.IsFinite(parent.Mu) || parent.Mu <= 0 ||
            !double.IsFinite(item.MeanRadius) || item.MeanRadius < 0)
            return null;

        var radius = item.MeanRadius;
        var minimum = radius;
        if (item is Celestial)
        {
            if (double.IsFinite(parent.MaxTerrainRadius))
                minimum = Math.Max(minimum, parent.MaxTerrainRadius);
            var atmosphereRadius = parent.GetAtmosphereRadius();
            if (double.IsFinite(atmosphereRadius))
                minimum = Math.Max(minimum, atmosphereRadius);
        }

        var soi = parent.SphereOfInfluence;
        return new OrbitBodySnapshot(item.Id, item.Id,
            parent.Mu / 1_000_000_000d, radius / MetresPerKm,
            Math.Max(0.001, (minimum - radius) / MetresPerKm),
            double.IsFinite(soi) && soi > 0 ? soi / MetresPerKm : null);
    }
}
