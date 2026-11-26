using System;
using System.Collections.Generic;

namespace KsaDevPanel.Diagnostics;

/// <summary>
/// Holds a small, opt-in diagnostic record for one mod process run.
/// </summary>
/// <remarks>
/// This type deliberately has no dependency on KSA, StarMap, a logger, or the
/// clipboard. The host owns lifecycle callbacks and supplies only the observed
/// facts. Values are normalized and the event list is bounded before they are
/// retained or copied.
/// </remarks>
public sealed class SessionDiagnostics
{
    private const int MaxValueLength = 96;
    private const int MaxEvents = 16;
    private const int MaxSummaryLength = 2048;

    private readonly object _gate = new();
    private readonly List<string> _events = new(MaxEvents);
    private readonly string _runId;

    private string _version = string.Empty;
    private string _build = string.Empty;
    private bool _startupMarked;
    private bool _firstGuiObserved;
    private bool _worldObserved;
    private bool _worldReady;
    private string _scene = "unobserved";

    /// <summary>
    /// Creates a new diagnostic session with a non-sensitive process-local ID.
    /// </summary>
    public SessionDiagnostics()
    {
        _runId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Gets the stable ID for this diagnostic session.
    /// </summary>
    public string RunId => _runId;

    /// <summary>
    /// Records the startup identity and emits one bounded startup marker.
    /// Repeated calls are ignored so a run cannot produce duplicate markers.
    /// </summary>
    /// <param name="version">The mod version, if known.</param>
    /// <param name="build">The verified game build, if known.</param>
    public void MarkStartup(string version, string build)
    {
        string marker;
        lock (_gate)
        {
            if (_startupMarked)
            {
                return;
            }

            _version = Normalize(version);
            _build = Normalize(build);
            _startupMarked = true;
            marker = $"[KsaDevPanel] startup version={_version} build={_build} run={_runId}";
            AddEventLocked("startup");
        }

        // Keep this as the only stdout write in the diagnostics component. The
        // inputs and run ID cannot contain a line break after normalization.
        Console.WriteLine(marker);
    }

    /// <summary>
    /// Records that the first GUI draw callback has been observed.
    /// </summary>
    public void MarkFirstGui()
    {
        lock (_gate)
        {
            if (_firstGuiObserved)
            {
                return;
            }

            _firstGuiObserved = true;
            AddEventLocked("first-gui");
        }
    }

    /// <summary>
    /// Records the latest observed world readiness state.
    /// Repeated observations of the same state are coalesced.
    /// </summary>
    /// <param name="ready">Whether a usable world was observed by the caller.</param>
    public void ObserveWorld(bool ready)
        => ObserveScene(ready ? SessionScene.World : SessionScene.Loading);

    public void ObserveScene(SessionScene scene)
    {
        lock (_gate)
        {
            var label = scene switch
            {
                SessionScene.World => "world",
                SessionScene.Editor => "editor",
                _ => "startup or transition"
            };
            // The editor is an active scene, but it is not a loaded flight world.
            var ready = scene == SessionScene.World;
            if (_scene != label)
            {
                _scene = label;
                AddEventLocked("scene=" + _scene);
            }
            if (_worldObserved && _worldReady == ready)
            {
                return;
            }

            _worldObserved = true;
            _worldReady = ready;
            AddEventLocked(ready ? "world-ready" : "world-not-ready");
        }
    }

    /// <summary>
    /// Returns a bounded, path-free text summary suitable for a future copy action.
    /// This method does not access the system clipboard.
    /// </summary>
    public string CopySummary()
    {
        lock (_gate)
        {
            var summary =
                $"KsaDevPanel diagnostics\n" +
                $"run={_runId}\n" +
                $"startup={( _startupMarked ? $"version={_version} build={_build}" : "not-observed")}\n" +
                $"first-gui={(_firstGuiObserved ? "observed" : "not-observed")}\n" +
                $"scene={_scene}\n" +
                $"world={(!_worldObserved ? "not-observed" : _worldReady ? "ready" : "not-ready")}\n" +
                "events=" + string.Join(",", _events);

            return summary.Length <= MaxSummaryLength
                ? summary
                : summary[..MaxSummaryLength];
        }
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxValueLength)
        {
            normalized = normalized[..MaxValueLength];
        }

        var chars = normalized.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]))
            {
                chars[i] = ' ';
            }
        }

        return new string(chars);
    }

    private void AddEventLocked(string value)
    {
        if (_events.Count == MaxEvents)
        {
            _events.RemoveAt(0);
        }

        _events.Add(value);
    }
}

public enum SessionScene
{
    Loading,
    World,
    Editor
}
