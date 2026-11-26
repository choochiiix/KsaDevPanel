# KsaDevPanel UI

The UI adapter targets KSA Windows x64 **2026.11.25.5949**, `net10.0`, and the
locally pinned StarMap/API **0.5.4** (Harmony **2.4.3**). It uses the
`Brutal.ImGuiApi.ImGui` API shipped with that reference copy; the API surface
was checked statically against `Brutal.ImGui.dll` and `Brutal.ImGui.Abstractions.dll`
(workspace evidence L-01; API usage pattern M-07).

## Contract

`KsaDevPanel.UI.Workbench` has this host-facing shape:

```csharp
var panel = new Workbench(isolatedSettingsFile);
// Only from the verified GUI callback:
panel.Draw(diagnostics, vesselSnapshot);
// On unload:
panel.Dispose();
```

`isolatedSettingsFile` must be an absolute path supplied by the host. The UI
does not infer a normal profile directory or read `LocalPaths.local.json`.
It writes `KsaDevPanel.settings.json` in the disposable profile. The file
contains the shortcut, panel visibility, and window position and size.
ImGui's shared host layout is outside this settings contract.

The diagnostics boundary consumes `RunId` and `CopySummary()` only. The
inspection boundary consumes `VesselSnapshotFormatter.Format(snapshot)`, so no
KSA object, world reference, render handle, or mutable game collection enters
the renderer.

## Dashboard behavior

The dark dashboard has three tabs:

- **Vessel** groups altitude, surface/orbital speed, ECL speed and acceleration
  under flight and motion sections. Total mass, propellant mass and manual
  throttle have their own section. Expand the ECL or body-relative rows to see
  vector components, or expand the part list for craft details. Values refresh
  five times a second. Altitude is in km; other physical scales and the throttle
  range remain unverified. ECL is the game's root inertial frame.
- **Diagnostics** shows the run ID and bounded lifecycle summary. Copying stays
  an explicit UI action; diagnostics itself never accesses the clipboard.
- **Settings & Refresh** groups the shortcut controls, live settings refresh,
  and the restart requirement for code changes.

Older saved layouts migrate to a larger 1040×700 workbench window.

The default toggle is **F12**. Its full stock conflict map is pending. A hotkey
can be changed to F1–F24 and is saved in the isolated JSON file. Escape cancels
capture. While ImGui reports `WantTextInput` or an active item, the panel does
not consume its toggle key. This preserves text fields and modal navigation;
the host still owns input routing.

Position and size are applied and sampled from `Draw`. Changed settings are
published at most once a second during ordinary drawing. Rebinding and unload
also flush the current settings. File failures leave the panel usable in memory.

## Manual UI checks (authorized isolated profile only)

Use a disposable profile and record the executable identity before testing
(workspace build baseline L-01). The workbench and live telemetry have been
visually observed in one active-world scenario; see [Verification](Verification.md)
for the measured values and remaining acceptance checks.

1. In a gameplay or editor draw context without an active vessel, verify the
   panel opens and reports unavailable vessel state.
2. Enter a world with an active vessel. Verify the inspector shows the copied
   identity, body, situation, velocity and parts, then switch scenes/worlds and
   verify stale data is not retained by the caller.
3. Focus a text-entry control and press F12. Verify the focused control keeps
   input and the panel does not toggle. Press F12 outside text entry to toggle.
4. Change the hotkey, restart the isolated profile, and verify the new key and
   panel visibility persist. Move/resize the window, restart, and verify layout
   persistence without touching the normal profile.
5. Check F12 and the chosen replacement against the stock controls in the same
   build. Record any conflict rather than assuming the candidate is safe.
6. Exercise pause, warp, a world transition, stock HUD toggling, modal dialogs,
   and exit/restart. Stock navigation must remain usable and the panel must fail
   open if its snapshot is unavailable.

## Static checks performed

| Check | Expected | Actual | Game ran |
| --- | --- | --- | --- |
| Local `Brutal.ImGui.dll` compile check | `ImGui.Begin/End`, `GetIO`, `IsKeyPressed`, window position and size calls, F1–F24 | Compiled against the `.5949` reference copy | No |
| Pinned tab layout and vessel details | `BeginTabBar`, `BeginTabItem`, row-background detail table and expandable part list | Compiled against the pinned `.5949` ImGui wrapper | No |
| Source/build contract | `Workbench(string)`, `Draw(SessionDiagnostics, VesselSnapshot?)`, `IDisposable` | Implemented in `src/KsaDevPanel/UI/Workbench.cs` | No |
| Settings safety | Relative/blank path rejected; no profile fallback | Enforced by constructor | No |
| Runtime UI | Frame callback, scene/input/layout behavior | Workbench and live vessel data were visible in a loaded .5949 test world; F12 opened it from a saved-hidden state. Cross-scene, hotkey conflict and restart persistence checks remain open. | Yes |

Open gaps remain callback behavior across editor and transition scenes, the stock
hotkey conflict map, vessel switching and restart persistence. Community UI
examples establish the ImGui/capture pattern but do not replace those
`.5949` acceptance checks (M-07, see workspace
`Documentation/Research/OpenQuestions.md`).
