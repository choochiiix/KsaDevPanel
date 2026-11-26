# Verification record

Target: Windows x64 KSA `2026.11.25.5949`, StarMap loader/API `0.5.4`, Harmony `2.4.3`, .NET SDK `10.0.100`, PowerShell 7.6.9. Mod version: `0.2.0-alpha.1`. All runtime work used the authorized shared disposable game and profile; the registered installation and normal saves remain read-only.

## Build and automated checks

| Check | Expected | Result |
| --- | --- | --- |
| `pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage` | Compile against the pinned KSA, StarMap and Harmony inputs and stage only the allowlisted payload. | **Passed.** Zero warnings/errors. Staged and installed DLL SHA-256: `1676D172F67D406CC45F93B70189CA2853B2EBA65E8C7F5D655009991660DEBC`. |
| `.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Inspection.Tests --no-restore` with `DOTNET_CLI_HOME` redirected under `.local/` | Snapshot copying, metric formatting, missing-field handling, unavailable state and world transition checks. | **Passed.** Five inspection checks. |
| `.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Diagnostics.Tests --no-restore` with `DOTNET_CLI_HOME` redirected under `.local/` | Run ID, startup marker, bounds, privacy and lifecycle coalescing. | **Passed.** Five diagnostics checks. |
| `pwsh -NoProfile -File tools/Test-InspectionTools.ps1` | Unsafe path/build rejection, synthetic deployment/rollback, manifest preservation and bounded log filtering. | **Passed.** Synthetic fixtures only. |
| Deployment dry run and installed payload hash | Resolve the shared disposable profile; ensure installed DLL matches stage. | **Passed.** Existing disposable deployment was rolled back first; new stage deployed to the shared profile, Core remained enabled, and the installed hash matched the stage. |

## Runtime observations

The isolated workbench appeared in a loaded-world test scenario. `@oai/sky` through `mcp__node_repl` launched the disposable game, cleared its first-run welcome dialog, and inspected the live KsaDevPanel window. The separate `mcp__cua_repl` wrapper still fails while creating its kernel assets; the direct supported `@oai/sky` route completed the required window interaction.

| Scenario | Expected | Result |
| --- | --- | --- |
| Versioned startup | One marker with the current mod version and pinned KSA build; StarMap loads the manifest entry. | **Passed.** The isolated log reports `version=0.2.0-alpha.1`, KSA build `2026.11.25.5949`, and StarMap loaded KsaDevPanel from the manifest. |
| Readable vessel panel | Grouped flight, motion, mass/control and vessel details; vector axes expandable; live values available. | **Passed visually** in one active-world `Rocket` scenario. |
| Telemetry reference smoke | Compare a sample with stock altitude, speed and resource displays. | **Partial.** The panel showed about `210.5 km` altitude, `7784.4` orbital-speed units/s, `0.0` acceleration, `563182.5` total-mass units, `534835.9` propellant units and `1.00` manual throttle. Stock HUD references were approximately `207.0 KM` for apses, `007.8 KM` for speed and `00535 MG` for propellant. The values suggest plausible scale conversions, but the API units and stock abbreviations are not confirmed. |
| Powered/coasting response and vessel switch | Check throttle, acceleration, propellant and identity in contrasting states and on another craft. | **Pending.** Only the freefall test state was inspected; no powered/coasting comparison or vessel switch was run. |
| Scene and input behavior | Workbench shortcut and data clearing behave correctly across menu, editor, transitions and text input. | **Partial.** F12 opened the panel from its saved-hidden state in the loaded world. F12 in the initial configuration screen did not show it. Menu/editor/transition, text-focus and stock-key conflict checks remain open. |
| Settings and restart persistence | Hotkey and window layout persist; live settings refresh rejects malformed input safely. | **Automated design is present; in-game acceptance pending.** |
| Diagnostics copy | Bounded report contains run ID and observed lifecycle state without paths or save data. | **Automated formatter passed; clipboard UI acceptance pending.** |

Use [Testing](Testing.md) for the full acceptance scenarios. A build, unit-test pass or startup marker is not evidence that untested gameplay behavior passed.
