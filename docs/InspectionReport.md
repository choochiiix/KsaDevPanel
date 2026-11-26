# Inspection handoff

## Contract delivered

- `VesselReader.Read()` converts the host adapter's primitive `VesselReadData` into `VesselSnapshot`.
- `VesselSnapshot` copies and seals part data; unavailable, no-vessel, and world-transition states are explicit.
- `VesselSnapshotFormatter.Format()` emits a separate immutable `VesselSnapshotDisplay` for UI code.
- No class in this slice stores a KSA vehicle, part tree, span, renderer handle, or source reference after `Read()` returns.
- `KsaVesselSource` uses the surveyed `.5949` `Program.ControlledVehicle`, `Program.CurrentAltitudeKm`, `Vehicle.GetVelocityEcl()`, `Vehicle.GetSurfaceSpeed()`, `Vehicle.OrbitalSpeed`, `Vehicle.AccelerationBody`, `Vehicle.TotalMass`, `Vehicle.PropellantMass`, `Vehicle.GetManualThrottle()`, inherited `Vehicle.Id`, `Vehicle.IsDisposed`, `Vehicle.Parts`, `PartTree.Parts`, `Part.DisplayName`, `Vehicle.Parent`, inherited parent `Id`, and `Vehicle.Situation` members. It copies primitive values into a new snapshot and never retains a KSA object. The `CurrentAltitudeKm` name supplies the altitude unit; velocity and other metrics remain KSA-native with scale or semantics still requiring runtime comparison (L-01, KSA `.5949`). ECL is the game's root inertial frame. Acceleration is labeled body-relative from the source member name.

## Evidence and limits

The target is KSA Windows x64 `2026.11.25.5949`; candidate metadata includes `KSA.Program.ControlledVehicle`, `Program.CurrentAltitudeKm`, and the listed `Vehicle` telemetry members (L-01, E-01). The isolated workbench rendered a live vessel snapshot in one active-world scenario. This confirms callback invocation for that scene only; editor/transition behavior, API units and wider lifetime semantics remain unverified. The adapter stays build-specific and owned by the host callback.

The build and deployment scripts use SDK `10.0.100`, StarMap/API `0.5.4` at `477f495d1c8d59910ad7f37071b77535f59f8c5d`, and Harmony `2.4.3`. Deployment requires the dedicated disposable profile or an explicit synthetic fixture under the OS temporary directory. See [Development](Development.md) for the supported commands.

## Checks

| Check | Expected | Actual | Game ran? |
|---|---|---|---|
| Input pins present | local dotnet, StarMap.API/Core and Harmony files | present under ignored `.local/` when inspected | No |
| Profile identity | absolute regular directory, exact marker and layout | enforced by `Test-IsolatedProfile.ps1`; existing disposable profile preflight passed | No by this workstream |
| Deployment safety | reject overlap/reparse and report isolated destination | integrated dry-run and synthetic apply/rollback passed, with byte-for-byte manifest restoration | No |
| Log bound | max 1,000 lines, optional run-ID filter | implemented in `Get-BoundedLog.ps1` | No |
| Pure inspection contract | copied data, immutable part list, unavailable and transition states, metric formatting and missing-field isolation | `tests/KsaDevPanel.Inspection.Tests`: 5 checks passed | No |
| Game-facing adapter | surveyed members compile with null/disposed guard; runtime phase/lifetime and metric semantics | adapter compiles against pinned `.5949` assemblies; live values were visible in a disposable freefall world. Stock HUD comparisons are suggestive; powered/coasting comparison, vessel switch and unit/frame confirmation remain pending | Yes, shared isolated session |

Sources: workspace `L-01`/`E-01` and `Documentation/Research/VersionCompatibility.md`; StarMap source revision and dependency pins are recorded in `Documentation/Research/Sources.md` (`M-02`, `D-STAR`).
