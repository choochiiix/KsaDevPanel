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

## Performance tab addition

The `.5949` source now contains a Performance tab and once-per-second background collector for process/system CPU, process/system memory, CPU name and OS-reported average current CPU frequency, per-process GPU Engine counters, and adapter-matched DXGI video-memory budget/usage. The tab retains the last five minutes in memory and lets the user select a metric to graph with ImPlot. GPU clock is intentionally omitted. CPU and RAM APIs use Windows/.NET built-ins; GPU values depend on PDH counters and a unique process-GPU-LUID to DXGI adapter match (D-PERF, official API documentation; implementation: `Performance/SystemMetricsCollector.cs`, `Performance/Gpu/GpuMetricsCollector.cs`, `Performance/PerformanceSampler.cs`, `Performance/PerformanceHistoryBuffer.cs`, `UI/PerformanceTabView.cs`, `UI/PerformanceMetricPicker.cs`, `UI/PerformanceMetricGraph.cs`).

| Check | Expected | Result |
|---|---|---|
| `pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage` | Compile against pinned `.5949`, StarMap 0.5.4, and Harmony 2.4.3 inputs and stage the allowlisted payload. | **Passed.** 0 warnings, 0 errors. Staged and installed DLL SHA-256 matched: `CC1A0379CDF063E54FDD614D9B56D299AE31D1C19533CFC85556FFD85856FF8C`. |
| Deployment dry run and deployment | Resolve the isolated profile and install the staged mod. | **Passed.** The prior deployment was rolled back using the mod's deployment tool; the staged payload was then deployed to the isolated profile. The registered installation was not modified. |
| Isolated runtime launch | Launch the pinned `.5949` development copy and show the Performance tab. | **Passed visually.** `tools/Start-IsolatedKsa.ps1` opened the window owned by `.development/runtime/dotnet.exe`. The `Performance history` view displayed the Process CPU graph with 45 samples, current/minimum/maximum values, and CPU percent over elapsed seconds. |
| Automated tests | Run the relevant portable history/metric checks. | **Not run for this change.** |
| Metric selection and graph behavior | Select several CPU, RAM, and GPU metrics; inspect units, gaps and rolling-window trim; return with **Back**. | **Pending.** Only the Process CPU history view was visually observed; picker interactions, other graph series, gaps, and five-minute trimming were not exercised. |
| Hardware readings | Display real CPU/RAM/GPU identity and readings, with unavailable GPU values handled safely; compare to Task Manager. | **Pending.** The graph view ran in KSA, but hardware summary accuracy was not compared with Task Manager. GPU-engine and Vulkan video-memory reporting remain unverified. |

## Set Orbit addition

Target: KSA Windows x64 2026.11.25.5949, StarMap loader/API 0.5.4, Harmony 2.4.3, .NET SDK 10.0.100.

- Local metadata (L-01) confirms the public OrbitData constructor, Orbit.Create, CelestialSystem.ForEach and Get, and InputEvents.TeleportInputBuffer.Add. The adapter submits TeleportInputData through that input queue. E-08 at 718354d0d7faf6421d373d3a33d957edaa7760f9, VehicleSpawner.Orbits.cs and SpawnOrbitsTest.cs, demonstrates metres, seconds, radians and the body-centred inertial plane. L-03, bundled .5901 history rev 5806, records the earlier Set Orbit near-parabolic fix.
- Build and stage: pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage. Passed with 0 warnings and errors. Stage: .artifacts/stage-ef122cc7fc664859bf953c7645d92eb9.
- Automated: built each console suite with --no-restore --disable-build-servers and ran the resulting DLL with the pinned SDK. Expected deterministic phase/geometry, validation, and inspection/diagnostics regression coverage. Actual: 12 orbit, 5 inspection, and 5 diagnostics checks passed.
- Deployment: prior isolated deployment was rolled back with the mod tool, then the staged payload was deployed to the disposable profile. Installed DLL SHA-256 matched stage: FEAC2F388821225E073F677DC74B075F2750D6334BE044B1046FCD9EBC2D9016. Core remained enabled.
- Isolated startup: passed. Start-IsolatedKsa.ps1 launched pinned .5949; the startup marker reported 0.2.0-alpha.1 and StarMap loaded KsaDevPanel. The live workbench displayed the selected Set Orbit tab with an active Gemini7 vessel and loaded-system body picker. No teleport was applied.
- In-game acceptance of teleport behavior remains with the user. See scenarios 9–10 in Testing.md for placement, parent changes, invalid inputs, context changes, and save reload.

## Lifecycle tab addition

Target: KSA Windows x64 2026.11.25.5949, StarMap loader/API 0.5.4, Harmony 2.4.3, .NET SDK 10.0.100.

- Build and stage: `pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage`. Passed with 0 warnings and errors. Stage: `.artifacts/stage-e019d5d2967a4b6bbe0a0da8ab057373`.
- Automated checks: lifecycle suite passed 5 checks covering loading/editor, fresh sample, retained stale sample, transition clearing, and bounded/sanitized unavailable data. Diagnostics suite passed 6 checks, including editor not being reported as a ready flight world.
- Deployment: prior isolated deployment was rolled back with the mod tool, then the new stage was deployed to the shared disposable profile. Installed DLL SHA-256 matched stage: `245E01A73677C64D03ACBCBE75983E4F0A9BD3922A502A7CD577C377115A00EB`. Core remained enabled.
- Isolated launch: `tools/Start-IsolatedKsa.ps1` opened the pinned `.5949` window owned by `.development/runtime/dotnet.exe`. The app inventory confirmed the window title and process path. The user stopped computer-use inspection with Escape before a screenshot or Lifecycle-tab observation was completed; visual tab verification remains pending.
