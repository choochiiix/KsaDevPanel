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

The dark dashboard has two read-only cards:

- **Runtime Console** shows the run ID, bounded diagnostics summary, and a
  clipboard copy button. Copying remains an explicit UI action; diagnostics
  itself never accesses the clipboard.
- **Vessel Inspector** shows availability, identity, body, situation, velocity,
  and part names from the immutable display contract. Startup, world transition,
  and no-active-vessel snapshots remain visible as an unavailable state.

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
(workspace build baseline L-01). See [Verification](Verification.md) for the
observed configuration-screen behavior and pending gameplay checks.

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
| Source/build contract | `Workbench(string)`, `Draw(SessionDiagnostics, VesselSnapshot?)`, `IDisposable` | Implemented in `src/KsaDevPanel/UI/Workbench.cs` | No |
| Settings safety | Relative/blank path rejected; no profile fallback | Enforced by constructor | No |
| Runtime UI | Frame callback, scene/input/layout behavior | Initial configuration did not show the panel after F12. Gameplay checks pending. | Yes |

Open gaps remain the actual StarMap GUI callback invocation and stock hotkey
conflict map. Community UI examples establish the ImGui/capture pattern but do
not replace a `.5949` runtime smoke test (M-07, see workspace
`Documentation/Research/OpenQuestions.md`).
