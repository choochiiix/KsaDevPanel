using KsaDevPanel.Diagnostics;
using KsaDevPanel.Inspection;

var tests = new (string Name, Action Body)[]
{
    ("loading and editor are not flight worlds", NonWorldScenes),
    ("successful world sample is fresh and identified", FreshWorldSample),
    ("failed read preserves and ages the previous success", FailedReadRetainsPriorSample),
    ("transition clears prior world sample", TransitionClearsSample),
    ("unavailable details are bounded and exclude exception text", UnavailableDetailsAreSafe),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {test.Name}: {exception.Message}");
    }
}

foreach (var failure in failures)
    Console.Error.WriteLine(failure);

return failures.Count == 0 ? 0 : 1;

static void NonWorldScenes()
{
    var loading = RuntimeContextStatus.Create(SessionScene.Loading, false, null, 10, 5);
    Assert(loading.SceneLabel == "Not loaded (menu/loading/transition)", "loading scene should avoid claiming menu-specific detection");
    Assert(!loading.WorldReady && !loading.HasSuccessfulSnapshot, "loading must have no ready world or sample");

    var editor = RuntimeContextStatus.Create(SessionScene.Editor, false, null, 10, 5);
    Assert(editor.SceneLabel == "Editor", "editor scene should be identified");
    Assert(!editor.WorldReady && editor.WorldState == "No flight world", "editor must not be reported as a ready flight world");
}

static void FreshWorldSample()
{
    var snapshot = AvailableSnapshot("Gemini7");
    var status = RuntimeContextStatus.Create(
        SessionScene.World,
        true,
        snapshot,
        nowTick: 1_010,
        lastSuccessfulTick: 1_000,
        lastSuccessfulSnapshotVesselId: "Gemini7");
    var card = LifecycleHealthStatus.From(status, new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));

    Assert(status.SceneLabel == "World" && status.WorldReady, "ready world should be visible");
    Assert(card.ActiveVesselId == "Gemini7" && card.VesselState == "Ready", "active vessel should be identified");
    Assert(card.HasSuccessfulSnapshot && !card.IsSnapshotStale, "successful sample should be fresh");
    Assert(card.SnapshotAge == TimeSpan.FromMilliseconds(10), "age should use monotonic milliseconds");
    Assert(card.LastSuccessfulSnapshotVesselId == "Gemini7", "last sample should name its vessel");
}

static void FailedReadRetainsPriorSample()
{
    var failed = VesselSnapshot.Unavailable(VesselSnapshotAvailability.Unavailable, "sensitive exception text");
    var status = RuntimeContextStatus.Create(
        SessionScene.World,
        true,
        failed,
        nowTick: 2_201,
        lastSuccessfulTick: 100,
        lastSuccessfulSnapshotVesselId: "Gemini7");

    Assert(status.ActiveVesselLabel == "Unavailable", "failed current sample must not show prior vessel as active");
    Assert(status.HasSuccessfulSnapshot && status.IsSampleStale, "prior success should remain and age to stale");
    Assert(status.LastSuccessfulSnapshotVesselId == "Gemini7", "prior sample vessel should be identified");
    Assert(status.SampleAgeTicks == 2_101, "sample age should use monotonic ticks");
    Assert(status.UnavailableReason == "Sample unavailable", "failure reason should be a safe category");
}

static void TransitionClearsSample()
{
    var status = RuntimeContextStatus.Create(
        SessionScene.Loading,
        false,
        null,
        nowTick: 4_000,
        lastSuccessfulTick: 1_000,
        lastSuccessfulSnapshotVesselId: "Gemini7");

    Assert(!status.HasSuccessfulSnapshot && !status.IsSampleStale, "scene transition must clear old world data");
    Assert(status.LastSuccessfulSnapshotVesselId is null, "transition must clear sampled vessel identity");
}

static void UnavailableDetailsAreSafe()
{
    var longId = " craft\t\r\n" + new string('x', 120);
    var snapshot = AvailableSnapshot(longId);
    var status = RuntimeContextStatus.Create(SessionScene.World, true, snapshot, 10, 10);

    Assert(status.ActiveVesselLabel.Length <= 96, "vessel identity should be bounded");
    Assert(!status.ActiveVesselLabel.Any(char.IsControl), "vessel identity should be line safe");

    var unavailable = RuntimeContextStatus.Create(
        SessionScene.World,
        true,
        VesselSnapshot.Unavailable(VesselSnapshotAvailability.Unavailable, "raw exception/path detail"),
        10,
        null);
    Assert(unavailable.UnavailableReason == "Sample unavailable", "raw exception text should not reach the UI");
}

static VesselSnapshot AvailableSnapshot(string id) => VesselSnapshot.From(new VesselReadData(
    id,
    id,
    null,
    "orbiting",
    null,
    Array.Empty<PartReadData>()));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
