# Verification record

Windows x64 and PowerShell 7.6.9. See [Compatibility](Compatibility.md) for artifact fingerprints and upstream revisions. The mod version is `0.1.0-alpha.1`. The game ran only from the authorized disposable copy.

## Build and tooling

| Command or check | Expected | Result |
| --- | --- | --- |
| `pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage` twice | Two successful builds against the same inputs and an allowlisted payload. | **Passed.** Both builds reported zero warnings and errors. Both mod DLLs had SHA-256 `fd1851f61dccf9b37f73fad25e97ba8317701bbf4792a9e196b38b6e5f40ee28`. Each stage contains `KsaDevPanel.dll`, `KsaDevPanel.deps.json`, and `mod.toml`. The pinned host and loader supply all external runtime dependencies. |
| `tools/Deploy-KsaDevPanel.ps1 -Profile <isolated> -Payload <stage> -DryRun` | Resolve the dedicated target and perform no writes. | **Passed.** Resolved `.local/profile/mods/KsaDevPanel`. |
| `pwsh -NoProfile -File tools/Test-InspectionTools.ps1` | Reject a wrong build marker and profile/payload overlap before a write. Apply the synthetic stage, enable its entry, then restore the prior mod and exact manifest bytes. Verify bounded log filtering. | **Passed.** Synthetic fixture only. The production code paths share the same transaction after preflight. The recorded runtime used a manual isolated stage. |
| `.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Diagnostics.Tests` and the analogous inspection command | Diagnostics and inspection contract tests pass. | **Passed.** Five diagnostics checks and three inspection checks passed. |
| Git inventory and Markdown links | Source, manifests, tools and documentation only. Local relative links resolve. | **Passed.** No packages, generated payload, game binaries, saves, logs, or local path overrides appeared in the proposed Git inventory. |
| Changelog and release instructions | Unreleased describes this change and mod instructions preserve the release rules. | **Passed.** The initial preview remains under Unreleased. |

## Runtime observations

| Check | Expected | Result |
| --- | --- | --- |
| Mod-disabled baseline | KSA reaches configuration and F12 does not open the workbench. | **Passed.** A Core-only manifest reached the configuration screen, F12 produced no workbench, and closing the baseline returned exit 0. |
| Profile isolation | The game uses the disposable copy and writes startup state to the disposable profile. | **Passed for observed startup.** The loader was pointed at the copied KSA.dll. The logs reported the isolated settings and pipeline cache paths. The isolated profile gained settings, manifest, HUD layouts, logs, saves and Vehicles directories. |
| Startup handshake | One fresh marker per launch and different run IDs across two launches. | **Pending.** One enabled launch produced exactly one marker with build `2026.11.25.5949`, version `0.1.0-alpha.1` and a 32-character run ID. StarMap reported the mod loaded from the manifest. No second enabled launch was performed after computer control stopped. |
| Shortcut, window and stock input | Workbench toggles in supported scenes and stock controls work. | **Pending.** Pressing F12 in the initial configuration screen on the enabled launch did not display the panel. The gameplay draw callback and input behavior were not exercised. Initial configuration is outside the observed supported surface. |
| Vessel switching and world transitions | Change active vessel and leave the world. The snapshot updates and clears. | **Pending.** No disposable world was entered before computer control stopped. Adapter signatures compile, but the world lifetime, data semantics and callback timing remain unverified. Velocity is explicitly unavailable. |
| Restart persistence | Reopen with the changed hotkey and saved position and size. | **Pending.** The enabled run did not reach an observed workbench draw. |
| Clipboard | Copy a bounded report with a run ID and without private paths or save data. | **Pending in game.** The dependency-free diagnostics tests cover the formatter and bounds. |

The launch command used the workspace-local `.NET 10.0.10` host and `StarMap.dll`, from the StarMap working directory. Both profile variables were set to the disposable profile. No CLI instance override was passed. The loader's `DocumentsPathOverride.cs` at `477f495d1c8d59910ad7f37071b77535f59f8c5d` gives CLI overrides precedence and installs the documents getter prefix before manifest preparation. D-STAR, upstream source and observed local startup.

The user stopped computer control after the first enabled configuration-screen check. The running preview was left alone. The runtime payload came from an earlier build of this prerelease. Later scene and inspection changes have compile evidence only.

On first SDK use the CLI reported installing an ASP.NET development certificate. The build helper suppresses certificate generation on subsequent invocations.
