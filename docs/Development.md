# Development and deployment

Work from the mod repository with PowerShell 7 on Windows x64. See [Compatibility](Compatibility.md) for the .NET SDK, KSA, .NET runtime and StarMap pins and package fingerprints.

## Build and stage

Set `referenceGameDirectory` and `registeredGameDirectory` in the parent workspace's ignored `LocalPaths.local.json`. The tools read those paths to compile against the reference and protect both installs. Extract the pinned SDK to `.local/dotnet/`, the .NET 10.0.10 Windows x64 runtime to `.local/runtime/`, and the StarMap ZIP to `.local/starmap/`.

```powershell
pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -DryRun
pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage
```

The build uses offline restore with the checked-in package-source configuration. The stage command prints an ignored `.artifacts/stage-*/` directory containing only the mod DLL, dependency manifest, and descriptor. The loader supplies StarMap.API and Harmony. The game supplies KSA and BRUTAL assemblies.

## Shared development instance

Use one development install for all mods. The shared setup lives in the workspace root under `.development/game/`, `.development/loader/`, `.development/runtime/`, and `.development/profile/`. For a game update, stage a new version separately, verify it, and keep the last working copy until the update passes its smoke checks. Treat the registered installation and its saves as read-only.

The shared loader configuration lives at `.development/loader/StarMapConfig.json`. The launcher passes the development game, shared profile, and loader config explicitly. It checks the build fingerprints and creates a fresh profile marker. Launch once to initialize the profile, then reuse that copy across independent mod repositories.

```powershell
pwsh -NoProfile -File tools/Start-IsolatedKsa.ps1 -DryRun
pwsh -NoProfile -File tools/Start-IsolatedKsa.ps1
```

Close the game before staging changes to the profile. Launch and deployment share an exclusive lock. The launcher passes the isolated documents path on StarMap's command line and captures logs beneath `.development/`.

## Deploy and roll back

```powershell
$env:KSA_DEV_PANEL_PROFILE = (Resolve-Path ..\..\.development\profile).Path
$env:STARMAP_INSTANCE_PATH = $env:KSA_DEV_PANEL_PROFILE
$stage = (Get-ChildItem .artifacts -Directory -Filter 'stage-*' |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).FullName

pwsh -NoProfile -File tools/Deploy-KsaDevPanel.ps1 -Profile $env:KSA_DEV_PANEL_PROFILE -Payload $stage -DryRun
pwsh -NoProfile -File tools/Deploy-KsaDevPanel.ps1 -Profile $env:KSA_DEV_PANEL_PROFILE -Payload $stage
pwsh -NoProfile -File tools/Start-IsolatedKsa.ps1
```

The tool checks the profile, shared game build and loader pins, and staged file list before taking the lock or writing. The backup stores the prior KsaDevPanel directory and manifest entry. Rollback removes only this mod's enabled entry and restores its previous entry, preserving other mod entries and folders. After closing the game, roll back with:

```powershell
pwsh -NoProfile -File tools/Deploy-KsaDevPanel.ps1 -Profile $env:KSA_DEV_PANEL_PROFILE -Rollback
```

Rollback restores the prior mod folder and cleans up its transaction lock. A deployment with an existing rollback backup stops until that deployment is rolled back.

## Check a run

```powershell
pwsh -NoProfile -File tools/Test-InspectionTools.ps1
.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Diagnostics.Tests
.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Inspection.Tests
```

Use `tools/Get-BoundedLog.ps1 -Log <isolated-log-path> -MaxLines 120` to check the last 120 lines, optionally filtered by run ID. Match markers with the fresh launch log. See [Testing](Testing.md) for the game scenarios and [Verification](Verification.md) for results.

## Share the instance with another mod

Each mod project keeps its own source, build output, and deployment rollback folder. To connect another mod, point its compile references at the same `.development/game/`, use the shared StarMap loader under `.development/loader/`, and use `.development/profile/` with the same marker and command line shown above. Give the mod its own folder below `profile/mods/` and its own `profile/mod-rollbacks/<ModId>/` backup. Deployment must preserve the other manifest entries and hold the shared profile lock.
