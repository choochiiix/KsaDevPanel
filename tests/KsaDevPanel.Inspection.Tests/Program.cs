using KsaDevPanel.Inspection;

var tests = new (string Name, Action Body)[]
{
    ("read copies primitive vessel data", ReadCopiesData),
    ("formatting marks unavailable state", FormatsUnavailable),
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
        "v-1", "Aurora", "Earth", "Orbiting", new VelocityReadData(1, 2, 3, "ECL", "m/s"), parts)));
    var snapshot = reader.Read();
    parts.Add(new PartReadData("Leaked", "Test"));

    Assert(snapshot.IsAvailable, "snapshot should be available");
    Assert(snapshot.Id == "v-1" && snapshot.Name == "Aurora", "identity was not copied");
    Assert(snapshot.Parts.Count == 1, "parts should be copied, not retained");
    var display = VesselSnapshotFormatter.Format(snapshot);
    Assert(display.Velocity.Contains("ECL", StringComparison.Ordinal), "velocity frame was not formatted");
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
