using KsaDevPanel.Inspection;

var tests = new (string Name, Action Body)[]
{
    ("read copies primitive vessel data", ReadCopiesData),
    ("formatting marks unavailable state", FormatsUnavailable),
    ("formatting labels live vessel metrics", FormatsMetrics),
    ("missing telemetry remains field-local", HandlesMissingMetrics),
    ("world transition is explicit", HandlesTransition)
};

foreach (var test in tests)
{
    test.Body();
    Console.WriteLine($"PASS {test.Name}");
}

static void ReadCopiesData()
{
    var parts = new List<PartReadData> { new("Core", "Command") };
    var reader = new VesselReader(new FakeSource(() => new VesselReadData(
        "v-1", "Aurora", "Earth", "Orbiting", new VelocityReadData(1, 2, 3, "ECL", "m/s"), parts,
        new VesselMetricsReadData(
            123.45,
            40.25,
            39.75,
            new AccelerationReadData(1, 2, 2, "body-relative", "native units/s²; scale unverified"),
            100.5,
            25.25,
            0.75))));
    var snapshot = reader.Read();
    parts.Add(new PartReadData("Leaked", "Test"));

    Assert(snapshot.IsAvailable, "snapshot should be available");
    Assert(snapshot.Id == "v-1" && snapshot.Name == "Aurora", "identity was not copied");
    Assert(snapshot.Parts.Count == 1, "parts should be copied, not retained");
    var display = VesselSnapshotFormatter.Format(snapshot);
    Assert(display.Velocity.Frame == "ECL", "velocity frame was not formatted");
    Assert(display.Velocity.Speed == "3.7", "velocity magnitude was not formatted");
    Assert(display.Velocity.X == "1.0" && display.Velocity.Y == "2.0" && display.Velocity.Z == "3.0", "velocity axes were not separated");
    Assert(display.Metrics.Altitude == "123.5 km", "altitude unit or precision was not formatted");
    Assert(display.Metrics.SurfaceSpeed == "40.3 native units/s; scale unverified", "surface speed unit was not labeled");
    Assert(display.Metrics.OrbitalSpeed == "39.8 native units/s; scale unverified", "orbital speed unit was not labeled");
    Assert(display.Metrics.AccelerationMagnitude == "3.0 native units/s²; scale unverified", "acceleration magnitude was not formatted");
    Assert(display.Metrics.AccelerationX == "1.0" && display.Metrics.AccelerationY == "2.0" && display.Metrics.AccelerationZ == "2.0", "acceleration components were not separated");
    Assert(display.Metrics.TotalMass == "100.5 native mass units; scale unverified", "total mass unit was not labeled");
    Assert(display.Metrics.PropellantMass == "25.3 native mass units; scale unverified", "propellant mass unit was not labeled");
    Assert(display.Metrics.ManualThrottle == "0.75", "manual throttle command was not formatted");
}

static void FormatsMetrics()
{
    var reader = new VesselReader(new FakeSource(() => new VesselReadData(
        "v-2", "Test craft", "Earth", "Orbiting", null, null,
        new VesselMetricsReadData(80, null, 2, null, double.NaN, 5, 0))));
    var display = VesselSnapshotFormatter.Format(reader.Read());
    Assert(display.Metrics.Altitude == "80.0 km", "valid metric was unavailable");
    Assert(display.Metrics.SurfaceSpeed == "Unavailable", "missing surface speed was not isolated");
    Assert(display.Metrics.OrbitalSpeed == "2.0 native units/s; scale unverified", "valid orbital speed was lost");
    Assert(display.Metrics.AccelerationMagnitude == "Unavailable", "missing acceleration was not labeled");
    Assert(display.Metrics.TotalMass == "Unavailable", "non-finite mass should be unavailable");
    Assert(display.Metrics.PropellantMass == "5.0 native mass units; scale unverified", "valid propellant mass was lost");
    Assert(display.Metrics.ManualThrottle == "0.00", "zero throttle should remain a valid reading");
}

static void HandlesMissingMetrics()
{
    var reader = new VesselReader(new FakeSource(() => new VesselReadData(
        "v-3", "Test craft", null, "Orbiting", null, null)));
    var display = VesselSnapshotFormatter.Format(reader.Read());
    Assert(display.Metrics.Altitude == "Unavailable", "missing telemetry should be explicit");
    Assert(display.Metrics.ManualThrottle == "Unavailable", "missing throttle should be explicit");
}

static void FormatsUnavailable()
{
    var reader = new VesselReader(new FakeSource(() => null));
    var snapshot = reader.Read();
    var display = VesselSnapshotFormatter.Format(snapshot);
    Assert(snapshot.Availability == VesselSnapshotAvailability.NoActiveVessel, "missing vessel state changed");
    Assert(display.Status.StartsWith("Unavailable", StringComparison.Ordinal), "missing vessel was not labeled");
}

static void HandlesTransition()
{
    var reader = new VesselReader(new FakeSource(() => throw new VesselWorldTransitionException("loading")));
    Assert(reader.Read().Availability == VesselSnapshotAvailability.WorldTransition, "transition state changed");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeSource(Func<VesselReadData?> read) : IVesselSource
{
    public VesselReadData? ReadActiveVessel() => read();
}
