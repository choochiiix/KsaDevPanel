Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Resolve-ExistingAbsolutePath([string] $Path, [string] $Label) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw "$Label is required" }
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw "$Label must be absolute: $Path" }
    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    if ($resolved.Provider.Name -ne 'FileSystem') { throw "$Label is not a filesystem path" }
    return $resolved.Path
}

function Assert-NoReparsePoints([string] $Root, [string] $Label) {
    $rootItem = Get-Item -LiteralPath $Root -Force
    for ($parent = $rootItem; $null -ne $parent; $parent = $parent.Parent) {
        if ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "$Label has a reparse point at or above its root."
        }
    }
    Get-ChildItem -LiteralPath $Root -Force -Recurse -ErrorAction Stop | ForEach-Object {
        if (($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Label contains a reparse point: $($_.FullName)"
        }
    }
}

function Test-PathOverlap([string] $Left, [string] $Right) {
    $a = ([IO.Path]::GetFullPath($Left).TrimEnd('\') + '\').ToUpperInvariant()
    $b = ([IO.Path]::GetFullPath($Right).TrimEnd('\') + '\').ToUpperInvariant()
    return $a.StartsWith($b, [StringComparison]::Ordinal) -or $b.StartsWith($a, [StringComparison]::Ordinal)
}

function Get-ProtectedRoots {
    $workspace = Split-Path (Split-Path (Get-RepoRoot) -Parent) -Parent
    $localPaths = Join-Path $workspace 'LocalPaths.local.json'
    $roots = [System.Collections.Generic.List[string]]::new()
    if (Test-Path -LiteralPath $localPaths -PathType Leaf) {
        $config = Get-Content -LiteralPath $localPaths -Raw | ConvertFrom-Json
        foreach ($property in @('referenceGameDirectory', 'registeredGameDirectory')) {
            $candidate = $config.$property
            if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
                $roots.Add((Resolve-Path -LiteralPath $candidate).Path)
            }
        }
    }
    $normal = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)) 'My Games\Kitten Space Agency'
    $roots.Add($normal)
    return @($roots)
}

function Assert-NoProtectedOverlap([string] $Path, [string] $Label) {
    $resolved = [IO.Path]::GetFullPath($Path)
    foreach ($protected in Get-ProtectedRoots) {
        if (Test-PathOverlap $resolved $protected) { throw "$Label overlaps a protected production path: $protected" }
    }
}

function Get-ProfileContract([string] $ProfilePath) {
    $profile = Resolve-ExistingAbsolutePath $ProfilePath 'KSA_DEV_PANEL_PROFILE'
    $marker = Join-Path $profile '.ksa-dev-panel-isolated'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { throw "Missing profile marker: $marker" }
    $markerText = (Get-Content -LiteralPath $marker -Raw).Trim()
    if ($markerText -ne 'KsaDevPanel 2026.11.25.5949') { throw "Invalid profile marker: $markerText" }
    $mods = Join-Path $profile 'mods\KsaDevPanel'
    $modsRoot = Join-Path $profile 'mods'
    $settingsFile = Join-Path $profile 'KsaDevPanel.settings.json'
    $settingsDirectory = Join-Path $profile 'KsaDevPanel'
    $stockSettings = Join-Path $profile 'settings.toml'
    if (-not (Test-Path -LiteralPath $modsRoot -PathType Container)) { throw "Missing mods directory: $modsRoot" }
    if (-not (Test-Path -LiteralPath $settingsFile -PathType Leaf) -and
        -not (Test-Path -LiteralPath $settingsDirectory -PathType Container) -and
        -not (Test-Path -LiteralPath $stockSettings -PathType Leaf)) {
        throw "Missing settings path under profile root: $settingsFile"
    }
    Assert-NoReparsePoints $profile 'isolated profile'
    [pscustomobject]@{ Profile = $profile; Mods = $mods; Settings = $(if (Test-Path -LiteralPath $settingsFile -PathType Leaf) { $settingsFile } else { $settingsDirectory }); Marker = $marker }
}

function Assert-ProfileEnvironment([string] $ProfilePath) {
    $profile = Resolve-ExistingAbsolutePath $ProfilePath 'KSA_DEV_PANEL_PROFILE'
    foreach ($name in @('STARMAP_INSTANCE_PATH', 'KSA_DEV_PANEL_PROFILE')) {
        $setting = [Environment]::GetEnvironmentVariable($name, 'Process')
        $resolved = Resolve-ExistingAbsolutePath $setting $name
        if ($resolved.TrimEnd('\') -ine $profile.TrimEnd('\')) {
            throw "$name must match the requested isolated profile."
        }
    }
    Assert-NoProtectedOverlap $profile 'isolated profile'
    return Get-ProfileContract $profile
}
