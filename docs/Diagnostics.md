# Session diagnostics

`KsaDevPanel.Diagnostics.SessionDiagnostics` is the dependency-free boundary between
the StarMap lifecycle adapter and the Runtime Console. It records only facts
that the host explicitly observes; it does not inspect game objects, install hooks,
read saves, write files, access the clipboard, or claim game-wide exception coverage.

## Contract

```csharp
var diagnostics = new SessionDiagnostics();
diagnostics.MarkStartup("0.2.0-alpha.1", "2026.11.25.5949");
diagnostics.MarkFirstGui();
diagnostics.ObserveWorld(worldReady);
var text = diagnostics.CopySummary();
```

`RunId` is a fresh 32-character GUID string for the lifetime of the instance. It is
not derived from a machine path, user name, save name, or process arguments.

`MarkStartup` is idempotent and emits exactly one stdout line for the instance:

```text
[KsaDevPanel] startup version=... build=... run=...
```

Version and build values are trimmed, control characters are replaced with spaces,
and each value is capped at 96 characters. A second call is ignored, including its
values, so a callback mistake cannot produce a stream of startup markers.

`MarkFirstGui` records the first GUI observation once. `ObserveWorld` keeps the
latest readiness value and records only transitions (including the first
observation); repeated per-frame observations are coalesced.

`CopySummary` returns text for a later UI copy action. It does not copy to the
clipboard itself. The in-memory event buffer holds at most 16 entries and the
returned text is capped at 2,048 characters. The summary contains the run ID,
startup identity, GUI/world states, and bounded lifecycle event names only.

## Integration boundary

The main mod entry point owns StarMap callbacks and should call these methods from
the callback where each fact is actually observed. A StarMap callback name alone is
not evidence that a world is ready; the adapter must pass `true` only after its
build-specific readiness check. Likewise, this component should not be expanded
into a general logger or exception collector without a separate privacy and
retention decision.

## Evidence and verification

- Target baseline: KSA Windows x64 `2026.11.25.5949` (`VersionCompatibility.md`,
  L-01 in the parent workspace source catalog).
- Candidate host lifecycle: StarMap `0.5.4`, revision
  `477f495d1c8d59910ad7f37071b77535f59f8c5d` (D-STAR in the parent workspace source catalog);
  the host adapter remains separate from this class.
- This class has no game or loader references. The current isolated game run
  displayed its run ID and bounded lifecycle summary; see [Verification](Verification.md).

The diagnostics test project passes five checks: fresh non-empty run IDs; exact
single-line startup output; repeated startup idempotence; control/newline
normalization; GUI and world transition coalescing; summary privacy; and event and
summary bounds. Cross-scene callback and world-lifetime behavior remains pending
additional isolated runtime checks.
