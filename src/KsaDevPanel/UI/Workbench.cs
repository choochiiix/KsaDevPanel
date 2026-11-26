using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KsaDevPanel.UI;

/// <summary>
/// Read-only developer dashboard. The host must call <see cref="Draw"/> from a
/// verified ImGui callback and pass an explicit, isolated settings file path to
/// the constructor.
/// </summary>
public sealed class Workbench : IDisposable
{
    private const string WindowId = "KSA Developer Workbench###KsaDevPanel";
    private const ImGuiKey DefaultHotkey = ImGuiKey.F12;

    private readonly string _settingsPath;
    private UiSettings _settings;
    private UiSettings? _savedSettings;
    private bool _showWindow;
    private bool _captureHotkey;
    private bool _applyLayout = true;
    private long _nextSave;
    private long _lastSettingsWrite;
    private long _lastObservedWrite;
    private long _nextVesselDisplayUpdate;
    private KsaDevPanel.Inspection.VesselSnapshotDisplay? _vesselDisplay;
    private string? _displayVesselId;
    private string _settingsStatus = "Settings are current.";
    private bool _disposed;

    public Workbench(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
            throw new ArgumentException("An isolated settings file path is required.", nameof(settingsPath));
        if (!Path.IsPathFullyQualified(settingsPath))
            throw new ArgumentException("The settings path must be fully qualified; no profile fallback is allowed.", nameof(settingsPath));

        _settingsPath = Path.GetFullPath(settingsPath);
        var directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("The settings path must include an isolated directory.", nameof(settingsPath));

        Directory.CreateDirectory(directory);
        var settingsExisted = File.Exists(_settingsPath);
        _settings = ReadSettings(_settingsPath);
        _settings.Normalize();
        _savedSettings = _settings with { };
        _lastSettingsWrite = LastWriteTicks(_settingsPath);
        _lastObservedWrite = _lastSettingsWrite;
        _showWindow = _settings.Visible;
        if (!settingsExisted)
        {
            _savedSettings = null;
            Persist();
        }
    }

    /// <summary>
    /// Draws the dashboard. This method deliberately has no host-object reads;
    /// diagnostics and inspection supply immutable snapshots to the callback.
    /// </summary>
    public void Draw(
        KsaDevPanel.Diagnostics.SessionDiagnostics diagnostics,
        KsaDevPanel.Inspection.VesselSnapshot? snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var io = ImGui.GetIO();
        RefreshChangedSettings();
        if (!_captureHotkey && !io.WantTextInput && !ImGui.IsAnyItemActive() && ImGui.IsKeyPressed(_settings.Hotkey, false))
            _showWindow = !_showWindow;

        if (_captureHotkey)
            CaptureHotkey();

        if (_showWindow)
            DrawDashboard(diagnostics, snapshot);

        if (Environment.TickCount64 >= _nextSave)
        {
            _nextSave = Environment.TickCount64 + 1_000;
            Persist();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Persist();
    }

    private void DrawDashboard(
        KsaDevPanel.Diagnostics.SessionDiagnostics diagnostics,
        KsaDevPanel.Inspection.VesselSnapshot? snapshot)
    {
        if (_applyLayout)
        {
            _applyLayout = false;
            var display = ImGui.GetIO().DisplaySize;
            var width = Math.Clamp(_settings.Width, 640, Math.Max(640, display.X));
            var height = Math.Clamp(_settings.Height, 420, Math.Max(420, display.Y));
            var position = new float2(
                Math.Clamp(_settings.X, 0, Math.Max(0, display.X - width)),
                Math.Clamp(_settings.Y, 0, Math.Max(0, display.Y - height)));
            ImGui.SetNextWindowPos(position, ImGuiCond.Always);
            ImGui.SetNextWindowSize(new float2(width, height), ImGuiCond.Always);
        }

        var open = true;
        if (!ImGui.Begin(WindowId, ref open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            if (!open)
                _showWindow = false;
            return;
        }

        if (!open)
            _showWindow = false;

        var positionNow = ImGui.GetWindowPos();
        var sizeNow = ImGui.GetWindowSize();
        _settings.X = positionNow.X;
        _settings.Y = positionNow.Y;
        _settings.Width = sizeNow.X;
        _settings.Height = sizeNow.Y;

        ImGui.TextDisabled("KSA DEVELOPER WORKBENCH");
        ImGui.TextWrapped($"Run {diagnostics?.RunId ?? "unavailable"}");
        ImGui.Separator();

        if (ImGui.BeginTabBar("KsaDevPanelTabs", default))
        {
            if (ImGui.BeginTabItem("Vessel", default))
            {
                DrawVesselInspector(snapshot);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Diagnostics", default))
            {
                DrawRuntimeConsole(diagnostics);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings & Refresh", default))
            {
                DrawSettingsCard();
                DrawRefreshCard();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.End();
    }

    private static void DrawRuntimeConsole(KsaDevPanel.Diagnostics.SessionDiagnostics? diagnostics)
    {
        ImGui.SeparatorText("Session diagnostics");
        var summary = SafeSummary(diagnostics);
        ImGui.TextWrapped(summary);
        if (ImGui.SmallButton("Copy diagnostics"))
            ImGui.SetClipboardText(summary);
    }

    private void DrawVesselInspector(KsaDevPanel.Inspection.VesselSnapshot? snapshot)
    {
        ImGui.SeparatorText("Active vessel");
        if (snapshot is null)
        {
            _displayVesselId = null;
            _vesselDisplay = null;
            ImGui.TextDisabled("No vessel snapshot available.");
            ImGui.TextWrapped("The world or active vessel is not ready. This readout updates from the current frame and clears when the vessel is unavailable.");
            return;
        }

        // Inspection owns formatting so this UI remains independent of game
        // object types. The display contract contains only immutable strings.
        var now = Environment.TickCount64;
        if (!snapshot.IsAvailable ||
            !string.Equals(_displayVesselId, snapshot.Id, StringComparison.Ordinal) ||
            now >= _nextVesselDisplayUpdate)
        {
            _vesselDisplay = KsaDevPanel.Inspection.VesselSnapshotFormatter.Format(snapshot);
            _displayVesselId = snapshot.IsAvailable ? snapshot.Id : null;
            _nextVesselDisplayUpdate = now + 200;
        }
        var display = _vesselDisplay ?? KsaDevPanel.Inspection.VesselSnapshotFormatter.Format(snapshot);
        ImGui.TextDisabled($"STATUS  {display.Status}");

        ImGui.SeparatorText("Flight conditions");
        float2? flightTableSize = null;
        if (ImGui.BeginTable("KsaDevPanelFlight", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in flightTableSize, 0f))
        {
            DrawDetailRow("Altitude", display.Metrics.Altitude);
            DrawDetailRow("Surface speed", display.Metrics.SurfaceSpeed);
            DrawDetailRow("Orbital speed", display.Metrics.OrbitalSpeed);
            ImGui.EndTable();
        }

        ImGui.SeparatorText("Motion");
        float2? motionTableSize = null;
        if (ImGui.BeginTable("KsaDevPanelMotion", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in motionTableSize, 0f))
        {
            DrawDetailRow("ECL speed", $"{display.Velocity.Speed} {display.Velocity.Units}");
            DrawDetailRow("Acceleration magnitude", display.Metrics.AccelerationMagnitude);
            ImGui.EndTable();
        }
        if (ImGui.TreeNode("Velocity components · ECL"))
        {
            float2? velocityTableSize = null;
            if (ImGui.BeginTable("KsaDevPanelVelocityComponents", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in velocityTableSize, 0f))
            {
                DrawDetailRow("X", display.Velocity.X);
                DrawDetailRow("Y", display.Velocity.Y);
                DrawDetailRow("Z", display.Velocity.Z);
                ImGui.EndTable();
            }
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("Acceleration components · body-relative"))
        {
            float2? accelerationTableSize = null;
            if (ImGui.BeginTable("KsaDevPanelAccelerationComponents", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in accelerationTableSize, 0f))
            {
                DrawDetailRow("X", display.Metrics.AccelerationX);
                DrawDetailRow("Y", display.Metrics.AccelerationY);
                DrawDetailRow("Z", display.Metrics.AccelerationZ);
                ImGui.EndTable();
            }
            ImGui.TreePop();
        }

        ImGui.SeparatorText("Vehicle mass & control");
        float2? vehicleTableSize = null;
        if (ImGui.BeginTable("KsaDevPanelVehicleMetrics", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in vehicleTableSize, 0f))
        {
            DrawDetailRow("Total mass", display.Metrics.TotalMass);
            DrawDetailRow("Propellant mass", display.Metrics.PropellantMass);
            DrawDetailRow("Manual throttle command", display.Metrics.ManualThrottle);
            ImGui.EndTable();
        }
        ImGui.TextDisabled("Altitude is in km. Native speed, acceleration, and mass scales and the throttle range are unverified.");

        ImGui.SeparatorText("Vessel details");
        float2? tableSize = null;
        if (ImGui.BeginTable("KsaDevPanelVesselDetails", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV, in tableSize, 0f))
        {
            DrawDetailRow("Vessel ID", display.Identity);
            DrawDetailRow("Parent body", display.ParentBody);
            DrawDetailRow("Situation", display.Situation);
            DrawDetailRow("Parts", display.Parts.Count.ToString(CultureInfo.InvariantCulture));
            ImGui.EndTable();
        }

        ImGui.TextDisabled("Telemetry refreshes five times per second. ECL is the game's root inertial frame.");

        if (ImGui.TreeNode($"Part list ({display.Parts.Count})"))
        {
            foreach (var part in display.Parts)
                ImGui.BulletText(part);
            ImGui.TreePop();
        }
    }

    private void DrawSettingsCard()
    {
        ImGui.SeparatorText("Panel settings");
        var keyName = (string)ImGui.GetKeyName(_settings.Hotkey);
        ImGui.Text($"Toggle hotkey: {keyName}");

        if (_captureHotkey)
        {
            ImGui.Text("Press F1–F24 or Escape to cancel.");
            if (ImGui.SmallButton("Cancel capture"))
                _captureHotkey = false;
        }
        else if (ImGui.SmallButton("Change hotkey"))
        {
            _captureHotkey = true;
        }

    }

    private void DrawRefreshCard()
    {
        ImGui.SeparatorText("Live settings refresh");
        ImGui.TextWrapped("Edit KsaDevPanel.settings.json in the isolated profile to refresh the shortcut and window layout while the game runs.");
        ImGui.TextWrapped(_settingsStatus);
        if (ImGui.SmallButton("Reload settings now"))
            ReloadSettings();
        ImGui.SeparatorText("Code changes");
        ImGui.TextWrapped("Changes to C# code still require rebuilding and restarting KSA with the current loader setup.");
    }

    private static void DrawDetailRow(string label, string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TableNextColumn();
        ImGui.TextWrapped(value);
    }

    private void CaptureHotkey()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            _captureHotkey = false;
            return;
        }

        for (var i = (int)ImGuiKey.F1; i <= (int)ImGuiKey.F24; i++)
        {
            var candidate = (ImGuiKey)i;
            if (!ImGui.IsKeyPressed(candidate, false))
                continue;
            _settings.Hotkey = candidate;
            _captureHotkey = false;
            Persist();
            return;
        }
    }

    private static string SafeSummary(KsaDevPanel.Diagnostics.SessionDiagnostics? diagnostics)
    {
        if (diagnostics is null)
            return "Diagnostics unavailable.";
        try
        {
            return diagnostics.CopySummary() ?? "Diagnostics summary unavailable.";
        }
        catch (Exception exception)
        {
            return $"Diagnostics summary failed: {exception.GetType().Name}";
        }
    }

    private void Persist()
    {
        try
        {
            _settings.Visible = _showWindow;
            if (_settings == _savedSettings)
                return;
            if (File.Exists(_settingsPath) &&
                (File.GetAttributes(_settingsPath) & FileAttributes.ReparsePoint) != 0)
                return;
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            var temporary = _settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew))
                using (var writer = new StreamWriter(stream))
                    writer.Write(json);
                File.Move(temporary, _settingsPath, overwrite: true);
                _savedSettings = _settings with { };
                _lastSettingsWrite = LastWriteTicks(_settingsPath);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        catch (IOException)
        {
            // UI state is best effort; a read-only or disconnected isolated path
            // must not take down the host frame callback.
        }
        catch (UnauthorizedAccessException)
        {
            // Same fail-open policy as above; the panel remains usable in memory.
        }
    }

    private void RefreshChangedSettings()
    {
        var writeTicks = LastWriteTicks(_settingsPath);
        if (writeTicks == 0 || writeTicks == _lastObservedWrite)
            return;
        _lastObservedWrite = writeTicks;
        ReloadSettings();
    }

    private void ReloadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath) ||
                (File.GetAttributes(_settingsPath) & FileAttributes.ReparsePoint) != 0)
            {
                _settingsStatus = "Settings file is missing or is not a regular isolated file.";
                return;
            }

            var json = File.ReadAllText(_settingsPath);
            var candidate = JsonSerializer.Deserialize<UiSettings>(json);
            if (candidate is null)
            {
                _settingsStatus = "Settings were empty; the last valid settings are still active.";
                return;
            }

            candidate.Normalize();
            _settings = candidate;
            _savedSettings = candidate with { };
            _showWindow = candidate.Visible;
            _applyLayout = true;
            _lastSettingsWrite = LastWriteTicks(_settingsPath);
            _lastObservedWrite = _lastSettingsWrite;
            _settingsStatus = "Settings refreshed.";
        }
        catch (JsonException)
        {
            _settingsStatus = "Settings JSON is invalid; the last valid settings are still active.";
        }
        catch (IOException)
        {
            _settingsStatus = "Settings file is temporarily unavailable; retrying on the next change.";
        }
        catch (UnauthorizedAccessException)
        {
            _settingsStatus = "Settings file could not be read; the last valid settings are still active.";
        }
    }

    private static long LastWriteTicks(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static UiSettings ReadSettings(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new UiSettings();
            return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(path)) ?? new UiSettings();
        }
        catch (JsonException)
        {
            return new UiSettings();
        }
        catch (IOException)
        {
            return new UiSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new UiSettings();
        }
    }

    private sealed record UiSettings
    {
        public int LayoutVersion { get; set; } = 1;
        public ImGuiKey Hotkey { get; set; } = DefaultHotkey;
        public bool Visible { get; set; } = true;
        public float X { get; set; } = 32;
        public float Y { get; set; } = 72;
        public float Width { get; set; } = 1040;
        public float Height { get; set; } = 700;

        public void Normalize()
        {
            if (LayoutVersion < 2)
            {
                Width = 1040;
                Height = 700;
                LayoutVersion = 2;
            }
            if ((int)Hotkey < (int)ImGuiKey.F1 || (int)Hotkey > (int)ImGuiKey.F24)
                Hotkey = DefaultHotkey;
            if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(Width) || !float.IsFinite(Height))
            {
                X = 32;
                Y = 72;
                Width = 1040;
                Height = 700;
            }
        }
    }
}
