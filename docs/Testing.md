# Testing

## Required workflow for each feature change

1. Write the observable acceptance scenario before changing code: starting scene/save, vessel or UI state, action, expected readout/behavior, and any tolerance or units. Keep test saves inside `.development/profile/` or a disposable fixture.
2. Run the relevant automated checks: the pinned build, pure inspection/diagnostics tests, and tooling fixture checks when deployment or path handling changes. Inspect the staged allowlist and deployment dry-run. Record each command and outcome; do not treat compile success as behavioral proof.
3. Close KSA before deployment. Deploy only to the shared isolated profile, verify the installed DLL hash against the stage, and launch through `tools/Start-IsolatedKsa.ps1`.
4. Inspect the actual game window using Windows computer control. Observe the window before every UI action and refresh the observation after it. If the CUA wrapper fails to initialize, use `@oai/sky` through `mcp__node_repl`; target the returned KSA window owned by `.development/runtime/dotnet.exe`, not the registered-game entry. For feature-specific behavior, enter the planned scene and perform the acceptance scenario. For vessel telemetry, sample at least two contrasting states (for example, parked and under thrust/coasting) and compare against a stock display or another documented reference where possible.
5. Verify the new run ID and loader markers in the matching isolated log, check for errors, and confirm the displayed result matches the acceptance criteria. Shut down cleanly before redeploying.
6. If both UI-control routes fail, mark visual/gameplay checks **pending**. Give the user the shortest exact in-game steps, collect their observation or screenshot, and inspect only bounded logs from the isolated profile. A successful launch is startup verification only.
7. Update `docs/Verification.md` with the build, loader, profile, commands, expected and actual results, whether the game ran, and any outstanding gaps. Use separate statuses for build, automated tests, loader startup, visual UI, and gameplay behavior.

## Automated checks

Start with the local build and dependency-free checks. Run the build against the pinned inputs and inspect the staged file list. Dry-run deployment and use synthetic fixtures for wrong-build, unsafe-path, deploy and rollback cases. Run the portable tests relevant to the changed behavior. Do not run unrelated tests just to inflate coverage.

The current inspection/diagnostics commands are:

```powershell
.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Diagnostics.Tests
.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Inspection.Tests
pwsh -NoProfile -File tools/Test-InspectionTools.ps1
```

Use the disposable instance for the runtime scenarios.

1. Launch twice. Confirm exactly one startup marker per launch and distinct run IDs. Check the first GUI event and explain any loader restart separately.
2. Open and close the workbench with F12 in the menu and active-world views. Test focus with a text input and use normal game controls while the panel is closed and open. Capture the actual panel, not just a log marker.
3. Open or create a disposable world with at least two vessels. Switch vessels and check identity, parent, parts and telemetry. Compare panel altitude against the game's altitude display. Record surface and orbital speed references and their units where the game exposes them. Check a low-throttle/coasting state and a powered state: manual throttle should follow its command, acceleration should respond, and propellant and total mass should be plausible for the selected craft. Do not assume KSA-native speed, acceleration, mass, or throttle scales until a reference confirms them. Return to the menu or change worlds and confirm the inspector clears promptly.
4. Change the keybind and move and resize the window. Restart with the same profile and confirm both settings survive.
   While running, edit `KsaDevPanel.settings.json` in `.development/profile/` to change the F-key or window bounds. Confirm the panel applies the edit automatically and the **Reload settings now** button works. Then save malformed JSON and confirm the last valid settings remain active; restore valid JSON and confirm refresh resumes.
5. Copy Runtime Console diagnostics. Check the run ID, bounded events, and absence of private paths or save contents.
6. Roll back the mod entry, relaunch, and confirm the shared Core baseline starts without the workbench.

See [Verification](Verification.md) for outcomes and [Development](Development.md) for the local workflow.
