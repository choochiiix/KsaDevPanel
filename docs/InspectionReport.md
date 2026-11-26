# Inspection handoff

## Contract delivered

- `VesselReader.Read()` converts the host adapter's primitive `VesselReadData` into `VesselSnapshot`.
- `VesselSnapshot` copies and seals part data; unavailable, no-vessel, and world-transition states are explicit.
- `VesselSnapshotFormatter.Format()` emits a separate immutable `VesselSnapshotDisplay` for UI code.
- No class in this slice stores a KSA vehicle, part tree, span, renderer handle, or source reference after `Read()` returns.
- `KsaVesselSource` uses only the surveyed `.5949` `Program.ControlledVehicle`, inherited `Vehicle.Id`, `Vehicle.IsDisposed`, `Vehicle.Parts`, `PartTree.Parts`, `Part.DisplayName`, `Vehicle.Parent`, inherited parent `Id`, and `Vehicle.Situation` members. It copies part display names into a new list; velocity remains explicitly unavailable until its frame and units are verified.

## Evidence and limits

The target is KSA Windows x64 `2026.11.25.5949`; candidate metadata includes `KSA.Program.ControlledVehicle`, `PartTree.Parts`, and `Part.DisplayName` (L-01, E-01). The source inventory does not prove draw-thread ownership, object lifetime, velocity frame/units, or transition behavior. The adapter must therefore remain a build-specific integration owned by the host callback.

The build and deployment scripts use SDK `10.0.100`, StarMap/API `0.5.4` at `477f495d1c8d59910ad7f37071b77535f59f8c5d`, and Harmony `2.4.3`. Deployment requires the dedicated disposable profile or an explicit synthetic fixture under the OS temporary directory. See [Development](Development.md) for the supported commands.

## Checks

| Check | Expected | Actual | Game ran? |
|---|---|---|---|
| Input pins present | local dotnet, StarMap.API/Core and Harmony files | present under ignored `.local/` when inspected | No |
| Profile identity | absolute regular directory, exact marker and layout | enforced by `Test-IsolatedProfile.ps1`; existing disposable profile preflight passed | No by this workstream |
| Deployment safety | reject overlap/reparse and report isolated destination | integrated dry-run and synthetic apply/rollback passed, with byte-for-byte manifest restoration | No |
| Log bound | max 1,000 lines, optional run-ID filter | implemented in `Get-BoundedLog.ps1` | No |
| Pure inspection contract | copied data, immutable part list, unavailable and transition states | `Test-InspectionContracts.ps1`: 3 tests passed | No |
| Game-facing adapter | surveyed members compile with null/disposed guard; runtime phase/lifetime semantics | static adapter added; draw-thread and field semantics pending isolated runtime probe | No |

Sources: workspace `L-01`/`E-01` and `Documentation/Research/VersionCompatibility.md`; StarMap source revision and dependency pins are recorded in `Documentation/Research/Sources.md` (`M-02`, `D-STAR`).
