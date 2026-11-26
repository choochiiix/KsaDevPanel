using System.Globalization;
using Brutal.ImGuiApi;
using KsaDevPanel.OrbitEditing;

namespace KsaDevPanel.UI;

/// <summary>ImGui editor for a draft orbit. The host supplies immutable body data and an explicit apply action.</summary>
public sealed class OrbitEditorView
{
    private string? _selectedBodyId;
    private string? _lastVesselId;
    private int _lastContextVersion;
    private bool _hasContext;
    private double _periapsisAltitudeKm = 100;
    private double _apoapsisAltitudeKm = 100;
    private double _inclinationDeg;
    private double _longitudeAscendingNodeDeg;
    private double _argumentOfPeriapsisDeg;
    private double _trueAnomalyDeg;
    private bool _circular = true;
    private string _validationStatus = "Choose a body and review the orbit before applying it.";
    private string _applyStatus = "No orbit has been applied this session.";

    public void Draw(
        Func<IReadOnlyList<OrbitBodySnapshot>>? readBodies,
        Func<string, OrbitElements, string>? applyOrbit,
        string? vesselId,
        int contextVersion)
    {
        if (!_hasContext || !string.Equals(_lastVesselId, vesselId, StringComparison.Ordinal) || _lastContextVersion != contextVersion)
        {
            _hasContext = true;
            _lastVesselId = vesselId;
            _lastContextVersion = contextVersion;
            ResetDraft();
            _validationStatus = vesselId is null
                ? "No active vessel. Draft cleared."
                : "Vessel or world context changed. Draft cleared; explicitly choose a body before applying.";
        }

        ImGui.SeparatorText("Set orbit");
        ImGui.TextWrapped("Edit orbital elements around a celestial body. Applying this draft immediately moves the active vessel.");
        if (string.IsNullOrWhiteSpace(vesselId))
        {
            ImGui.TextDisabled("Unavailable: a current vessel identity is required.");
            return;
        }
        if (readBodies is null)
        {
            ImGui.TextDisabled("Body data is unavailable.");
            return;
        }

        IReadOnlyList<OrbitBodySnapshot> bodies;
        try
        {
            bodies = readBodies();
        }
        catch (Exception ex)
        {
            ImGui.TextDisabled("Body list could not be read.");
            _validationStatus = $"Body list unavailable: {ex.Message}";
            return;
        }

        if (bodies.Count == 0)
        {
            if (_selectedBodyId is not null)
                ResetDraft();
            ImGui.TextDisabled("No eligible celestial bodies are available in the current world.");
            return;
        }

        var selectedIndex = FindBody(bodies, _selectedBodyId);
        if (selectedIndex < 0)
        {
            if (_selectedBodyId is not null)
            {
                ResetDraft();
                _validationStatus = "Selected body is no longer available. Draft cleared; choose a body again.";
            }
            _selectedBodyId = null;
        }

        var previewName = selectedIndex >= 0 ? bodies[selectedIndex].DisplayName : "Choose a body...";
        if (ImGui.BeginCombo("Central body", previewName, default))
        {
            for (var i = 0; i < bodies.Count; i++)
            {
                var selected = i == selectedIndex;
                if (ImGui.Selectable($"{bodies[i].DisplayName}##orbit-body-{bodies[i].Id}", selected, default, default))
                {
                    _selectedBodyId = bodies[i].Id;
                    selectedIndex = i;
                    _validationStatus = "Body selected. Review the draft before applying.";
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        if (selectedIndex < 0)
        {
            ImGui.TextDisabled("Choose a body explicitly. Draft values reset when the vessel or world context changes.");
            ImGui.TextWrapped(_validationStatus);
            return;
        }

        var body = bodies[selectedIndex];
        ImGui.TextDisabled($"Radius {F(body.RadiusKm)} km · μ {F(body.MuKm3PerSec2)} km³/s²");
        if (body.MinimumClearanceKm is double clearance)
            ImGui.TextDisabled($"Minimum clearance {F(clearance)} km");
        if (body.SoiRadiusKm is double soi)
            ImGui.TextDisabled($"Sphere of influence {F(soi)} km from body centre");

        ImGui.SeparatorText("Orbit shape");
        ImGui.Checkbox("Circular orbit", ref _circular);
        if (_circular)
        {
            Input("Altitude (km)", ref _periapsisAltitudeKm);
            _apoapsisAltitudeKm = _periapsisAltitudeKm;
        }
        else
        {
            Input("Periapsis altitude (km)", ref _periapsisAltitudeKm);
            Input("Apoapsis altitude (km)", ref _apoapsisAltitudeKm);
        }
        Input("Inclination to body equator (deg)", ref _inclinationDeg);
        Input("Longitude of ascending node (deg)", ref _longitudeAscendingNodeDeg);
        Input("Argument of periapsis (deg)", ref _argumentOfPeriapsisDeg);
        Input("True anomaly (deg)", ref _trueAnomalyDeg);

        OrbitElements? elements = null;
        OrbitState? state = null;
        try
        {
            elements = new OrbitElements(body, _periapsisAltitudeKm, _apoapsisAltitudeKm,
                _inclinationDeg, _longitudeAscendingNodeDeg, _argumentOfPeriapsisDeg, _trueAnomalyDeg);
            state = OrbitModel.Calculate(elements);
            _validationStatus = "Draft is valid. Applying will instantly teleport the active vessel.";
        }
        catch (ArgumentException ex)
        {
            _validationStatus = ex.Message;
        }

        ImGui.SeparatorText("Derived preview");
        if (state is null)
            ImGui.TextWrapped($"Invalid orbit: {_validationStatus}");
        else
        {
            ImGui.TextWrapped($"a {F(state.SemiMajorAxisKm)} km · e {F(state.Eccentricity)} · period {F(state.PeriodSeconds)} s");
            ImGui.TextWrapped($"Body-relative inertial position (km): {Vector(state.PositionKm)}");
            ImGui.TextWrapped($"Body-relative inertial velocity (km/s): {Vector(state.VelocityKmPerSec)}");
        }

        ImGui.Separator();
        ImGui.TextWrapped(_applyStatus);
        if (applyOrbit is null)
        {
            ImGui.TextDisabled("Instant teleport is unavailable: no apply handler is connected.");
        }
        else if (elements is not null && state is not null && ImGui.Button("Apply orbit and teleport now", default))
        {
            try
            {
                _applyStatus = applyOrbit(body.Id, elements);
            }
            catch (Exception ex)
            {
                _applyStatus = $"Apply failed: {ex.Message}";
            }
        }
    }

    private static int FindBody(IReadOnlyList<OrbitBodySnapshot> bodies, string? id)
    {
        if (id is null)
            return -1;
        for (var i = 0; i < bodies.Count; i++)
            if (string.Equals(bodies[i].Id, id, StringComparison.Ordinal))
                return i;
        return -1;
    }

    private void ResetDraft()
    {
        _selectedBodyId = null;
        _periapsisAltitudeKm = 100;
        _apoapsisAltitudeKm = 100;
        _inclinationDeg = 0;
        _longitudeAscendingNodeDeg = 0;
        _argumentOfPeriapsisDeg = 0;
        _trueAnomalyDeg = 0;
        _circular = true;
    }

    private static void Input(string label, ref double value) =>
        ImGui.InputDouble(label, ref value, 0, 0, "%.6f", default);

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Vector(OrbitVector3 value) => $"({F(value.X)}, {F(value.Y)}, {F(value.Z)})";
}
