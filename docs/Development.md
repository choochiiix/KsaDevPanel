# Development and deployment

Work from the mod repository with PowerShell 7 on Windows x64. See [Compatibility](Compatibility.md) for the .NET SDK, KSA, .NET runtime and StarMap pins and package fingerprints.

## Build and stage

Set `referenceGameDirectory` and `registeredGameDirectory` in the parent workspace's ignored `LocalPaths.local.json`. The tools read those paths to compile against the reference and protect both installs. Extract the pinned SDK to `.local/dotnet/`, the .NET 10.0.10 Windows x64 runtime to `.local/runtime/`, and the StarMap ZIP to `.local/starmap/`.

```powershell
pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -DryRun
pwsh -NoProfile -File tools/Build-KsaDevPanel.ps1 -Stage
```

The build uses offline restore with the checked-in package-source configuration. The stage command prints an ignored `.artifacts/stage-*/` directory containing only the mod DLL, dependency manifest, and descriptor. The loader supplies StarMap.API and Harmony. The game supplies KSA and BRUTAL assemblies.

## Create an isolated instance

Keep the disposable game copy at `.local/game/` and its profile at `.local/profile/`. Use a regular directory for each and copy only the desired pinned game distribution. Inside the profile, create a text file named `.ksa-dev-panel-isolated` containing `KsaDevPanel 2026.11.25.5949`.

Create `.local/starmap/StarMapConfig.json` with `GameLocation` set to the absolute path to the disposable `KSA.dll`, `GameArguments` set to an empty array, and `RepositoryLocation` set to an empty string. Preview the launch and then run the Core-only baseline to initialize the profile.

```powershell
pwsh -NoProfile -File tools/Start-IsolatedKsa.ps1 -DryRun
pwsh -NoProfile -File tools/Start-IsolatedKsa.ps1
```

Close the game before staging changes to that profile. Subsequent launches through this script share an exclusive lock with deployment. The launcher sets `STARMAP_INSTANCE_PATH` and `KSA_DEV_PANEL_PROFILE` for the child. It saves stdout and stderr under `.local/`.

## Deploy and roll back

```powershell
$env:KSA_DEV_PANEL_PROFILE = (Resolve-Path .local/profile).Path
$env:STARMAP_INSTANCE_PATH = $env:KSA_DEV_PANEL_PROFILE
$stage = (Get-ChildItem .artifacts -Directory -Filter 'stage-*' |
    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).FullName

pwsh -NoProfile -File tools/Deploy-KsaDevPanel.ps1 -Profile $env:KSA_DEV_PANEL_PROFILE -Payload $stage -DryRun
pwsh -NoProfile -File tools/Deploy-KsaDevPanel.ps1 -Profile $env:KSA_DEV_PANEL_PROFILE -Payload $stage
pwsh -NoProfile -File tools/Start-IsolatedKsa.ps1
```

The tool checks the profile, actual game build, loader directory, and staged file list before taking the lock or writing. It backs up the previous mod folder and manifest before applying the staged files and enabling the mod. After closing the game, roll back with:

```powershell
pwsh -NoProfile -File tools/Deploy-KsaDevPanel.ps1 -Profile $env:KSA_DEV_PANEL_PROFILE -Rollback
```

Rollback restores the prior manifest bytes and mod folder. The backup is retained under a `.restored-*` name for recovery. A deployment with an existing rollback backup stops until that deployment is rolled back.

## Check a run

```powershell
pwsh -NoProfile -File tools/Test-InspectionTools.ps1
.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Diagnostics.Tests
.local/dotnet/dotnet.exe run --project tests/KsaDevPanel.Inspection.Tests
```

Use `tools/Get-BoundedLog.ps1 -Log <isolated-log-path> -MaxLines 120` to check the last 120 lines, optionally filtered by run ID. Match markers with the fresh launch log. See [Testing](Testing.md) for the game scenarios and [Verification](Verification.md) for results.
