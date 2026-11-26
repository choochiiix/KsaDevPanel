using System;
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
    private const string WindowId = "KSA Dev Panel###KsaDevPanel";
    private const ImGuiKey DefaultHotkey = ImGuiKey.F12;

    private readonly string _settingsPath;
    private UiSettings _settings;
    private UiSettings? _savedSettings;
    private bool _showWindow;
    private bool _captureHotkey;
    private bool _applyLayout = true;
    private long _nextSave;
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
        _settings = ReadSettings(_settingsPath);
        _settings.Normalize();
        _savedSettings = _settings with { };
        _showWindow = _settings.Visible;
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
            var width = Math.Clamp(_settings.Width, 320, Math.Max(320, display.X));
            var height = Math.Clamp(_settings.Height, 240, Math.Max(240, display.Y));
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

        ImGui.Text(snapshot?.IsAvailable == true ? "Active vessel available" : "Active vessel unavailable");
        ImGui.Separator();

        float2? tableSize = null;
        if (ImGui.BeginTable("KsaDevPanelCards", 2, ImGuiTableFlags.BordersInnerV, in tableSize, 0f))
        {
            ImGui.TableNextColumn();
            DrawRuntimeConsole(diagnostics);
            ImGui.TableNextColumn();
            DrawVesselInspector(snapshot);
            ImGui.EndTable();
        }

        ImGui.Separator();
        DrawSettingsCard();
        ImGui.End();
    }

    private static void DrawRuntimeConsole(KsaDevPanel.Diagnostics.SessionDiagnostics diagnostics)
    {
        ImGui.SeparatorText("Runtime Console");
        ImGui.Text("Session");
        ImGui.SameLine();
        ImGui.Text(diagnostics?.RunId ?? "unavailable");

        var summary = SafeSummary(diagnostics);
        ImGui.TextWrapped(summary);
        if (ImGui.SmallButton("Copy diagnostics"))
            ImGui.SetClipboardText(summary);
    }

    private static void DrawVesselInspector(KsaDevPanel.Inspection.VesselSnapshot? snapshot)
    {
        ImGui.SeparatorText("Vessel Inspector");
        if (snapshot is null)
        {
            ImGui.TextDisabled("No vessel snapshot available.");
            ImGui.TextWrapped("The world or active vessel is not ready. The panel remains available for startup and scene transitions.");
            return;
        }

        // Inspection owns formatting so this UI remains independent of game
        // object types. The display contract contains only immutable strings.
        var display = KsaDevPanel.Inspection.VesselSnapshotFormatter.Format(snapshot);
        ImGui.Text($"Status: {display.Status}");
        ImGui.Text($"Vessel: {display.Identity}");
        ImGui.Text($"Body: {display.ParentBody}");
        ImGui.Text($"Situation: {display.Situation}");
        ImGui.TextWrapped($"Velocity: {display.Velocity}");
        ImGui.Text($"Parts: {display.Parts.Count}");
        foreach (var part in display.Parts)
            ImGui.BulletText(part);
    }

    private void DrawSettingsCard()
    {
        ImGui.SeparatorText("Panel Settings");
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
        public ImGuiKey Hotkey { get; set; } = DefaultHotkey;
        public bool Visible { get; set; }
        public float X { get; set; } = 32;
        public float Y { get; set; } = 72;
        public float Width { get; set; } = 860;
        public float Height { get; set; } = 440;

        public void Normalize()
        {
            if ((int)Hotkey < (int)ImGuiKey.F1 || (int)Hotkey > (int)ImGuiKey.F24)
                Hotkey = DefaultHotkey;
            if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(Width) || !float.IsFinite(Height))
            {
                X = 32;
                Y = 72;
                Width = 860;
                Height = 440;
            }
        }
    }
}
