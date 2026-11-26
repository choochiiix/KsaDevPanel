# KsaDevPanel

KsaDevPanel is a private developer workbench for Kitten Space Agency. Open it with **F12**, the default configurable shortcut. The Runtime Console shows the run ID and recent lifecycle diagnostics and can copy a bounded report. The Vessel Inspector shows a read-only snapshot of the current active craft, with unavailable fields labeled.

The first preview targets Windows x64 KSA `2026.11.25.5949`, StarMap loader and API `0.5.4`, and loader-provided Harmony `2.4.3`. Build with the Windows x64 .NET SDK `10.0.100`. See [Compatibility](docs/Compatibility.md) for exact inputs and [Verification](docs/Verification.md) for what has been exercised.

The isolated loader startup and one mod handshake were observed on that build. Gameplay drawing, shortcut conflicts, vessel switching, and restart persistence await runtime verification. The inspector labels velocity unavailable while its frame and units are unresolved.

Use [Development and deployment](docs/Development.md) to compile, stage, and install into a disposable game copy and isolated profile. If the workbench does not open, check the startup marker, effective profile, manifest enablement, and the local shortcut settings. See [Testing](docs/Testing.md) for the corresponding checks.

Source and the loader manifest are in `src/KsaDevPanel/`. Deployment tools are in `tools/`, portable checks in `tests/`, and technical notes in `docs/`. See [Architecture](docs/Architecture.md), [Changelog](CHANGELOG.md), and [Roadmap](docs/Roadmap.md). Universe Inspector and the remaining workbench tools are planned.
