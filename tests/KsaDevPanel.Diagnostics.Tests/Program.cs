using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KsaDevPanel.Diagnostics;

var tests = new (string Name, Action Body)[]
{
    ("fresh run IDs are non-empty GUIDs", FreshRunIdIsGuid),
    ("startup writes one marker and is idempotent", StartupIsSingleMarker),
    ("startup values are bounded and line-safe", StartupValuesAreSafe),
    ("observations are coalesced and included", ObservationsAreCoalesced),
    ("summary and event buffer are bounded", SummaryIsBounded),
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
{
    Console.Error.WriteLine(failure);
}

return failures.Count == 0 ? 0 : 1;

static void FreshRunIdIsGuid()
{
    var diagnostics = new SessionDiagnostics();
    Assert(diagnostics.RunId.Length == 32, "run ID must use the compact GUID form");
    Assert(Guid.TryParseExact(diagnostics.RunId, "N", out _), "run ID must parse as a GUID");
    Assert(diagnostics.RunId != new SessionDiagnostics().RunId, "separate sessions need separate IDs");
}

static void StartupIsSingleMarker()
{
    var diagnostics = new SessionDiagnostics();
    var output = CaptureStdout(() =>
    {
        diagnostics.MarkStartup("0.2.0-alpha.1", "2026.11.25.5949");
        diagnostics.MarkStartup("ignored", "ignored");
    });

    var expected = $"[KsaDevPanel] startup version=0.2.0-alpha.1 build=2026.11.25.5949 run={diagnostics.RunId}{Environment.NewLine}";
    Assert(output == expected, "startup must emit exactly one marker line");
    Assert(diagnostics.CopySummary().Contains("version=0.2.0-alpha.1 build=2026.11.25.5949", StringComparison.Ordinal), "summary must retain the first startup identity");
}

static void StartupValuesAreSafe()
{
    var diagnostics = new SessionDiagnostics();
    var longVersion = new string('v', 200) + "\r\nforged";
    var output = CaptureStdout(() => diagnostics.MarkStartup(longVersion, "build\t\u0001"));
    var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    Assert(lines.Length == 1, "normalized startup values must remain one output line");
    Assert(lines[0].Length < 300, "startup output must be bounded");
    Assert(!lines[0].Contains('\r') && !lines[0].Contains('\n'), "startup marker must not contain embedded line breaks");
    Assert(lines[0].Contains("build=build  ", StringComparison.Ordinal), "control characters must be replaced");
}

static void ObservationsAreCoalesced()
{
    var diagnostics = new SessionDiagnostics();
    diagnostics.MarkFirstGui();
    diagnostics.MarkFirstGui();
    diagnostics.ObserveWorld(false);
    diagnostics.ObserveWorld(false);
    diagnostics.ObserveWorld(true);
    diagnostics.ObserveWorld(true);

    var summary = diagnostics.CopySummary();
    Assert(summary.Contains("first-gui=observed", StringComparison.Ordinal), "first GUI state must be reported");
    Assert(summary.Contains("world=ready", StringComparison.Ordinal), "latest world state must be reported");
    Assert(summary.Contains("scene=world\n", StringComparison.Ordinal), "latest scene must be reported");
    Assert(summary.Contains("events=first-gui,scene=startup or transition,world-not-ready,scene=world,world-ready", StringComparison.Ordinal), "unchanged observations must be coalesced");
}

static void SummaryIsBounded()
{
    var diagnostics = new SessionDiagnostics();
    for (var i = 0; i < 100; i++)
    {
        diagnostics.ObserveWorld(i % 2 == 0);
    }

    var summary = diagnostics.CopySummary();
    Assert(summary.Length <= 2048, "summary must have a hard size bound");
    var eventText = summary[(summary.IndexOf("events=", StringComparison.Ordinal) + "events=".Length)..];
    Assert(eventText.Split(',', StringSplitOptions.RemoveEmptyEntries).Length <= 16, "event buffer must have a hard count bound");
    Assert(!summary.Contains("saves", StringComparison.OrdinalIgnoreCase), "summary must not include save data");
}

static string CaptureStdout(Action action)
{
    var original = Console.Out;
    var writer = new StringWriter(new StringBuilder());
    Console.SetOut(writer);
    try
    {
        action();
        return writer.ToString();
    }
    finally
    {
        Console.SetOut(original);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
