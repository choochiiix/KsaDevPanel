[CmdletBinding(DefaultParameterSetName='Deploy')]
param(
    [Parameter(Mandatory)] [string] $Profile,
    [Parameter(Mandatory, ParameterSetName='Deploy')] [string] $Payload,
    [Parameter(Mandatory, ParameterSetName='Rollback')] [switch] $Rollback,
    [switch] $DryRun,
    [switch] $ApplySynthetic
)

. (Join-Path $PSScriptRoot 'KsaDevPanel.Common.ps1')
$contract = Assert-ProfileEnvironment $Profile
$repo = Get-RepoRoot
$workspace = Split-Path (Split-Path $repo -Parent) -Parent
$pinnedGame = Join-Path $workspace '.development\game'
$pinnedLoader = Join-Path $workspace '.development\loader'
if (!(Test-Path -LiteralPath $pinnedGame -PathType Container) -or
    (Get-Item (Join-Path $pinnedGame 'KSA.dll')).VersionInfo.FileVersion -ne '2026.11.25.5949' -or
    (Get-FileHash (Join-Path $pinnedGame 'KSA.dll')).Hash -ine 'FA741419E287FB7F673FBA3212BE622B5FC27DB91FA4A6632828FB9DA7B15C7E' -or
    (Get-Item (Join-Path $pinnedLoader 'StarMap.dll')).VersionInfo.ProductVersion -ne '0.5.4+477f495d1c8d59910ad7f37071b77535f59f8c5d') {
    throw 'Shared development game or StarMap build does not match the pinned inputs.'
}
$target = $contract.Mods
$manifest = Join-Path $contract.Profile 'manifest.toml'
$rollbackRoot = Join-Path $contract.Profile 'mod-rollbacks'
$backup = Join-Path $rollbackRoot 'KsaDevPanel'
$marker = if ($ApplySynthetic) { 'synthetic' } else { 'disposable' }

if ($ApplySynthetic) {
    $temp = [IO.Path]::GetTempPath().TrimEnd('\') + '\'
    if (!$contract.Profile.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The synthetic profile must be below the OS temporary directory.'
    }
} else {
    $expectedProfile = Join-Path (Split-Path (Split-Path $repo -Parent) -Parent) '.development\profile'
    if ($contract.Profile -ine $expectedProfile) { throw 'Deploy to the shared workspace development profile.' }
    & (Join-Path $PSScriptRoot 'Start-IsolatedKsa.ps1') -DryRun | Out-Null
}

if (!(Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'The isolated manifest is missing.' }
if ((Test-Path -LiteralPath $target) -and !(Test-Path -LiteralPath $target -PathType Container)) {
    throw 'The isolated mod target must be a directory.'
}

$files = @('KsaDevPanel.dll', 'KsaDevPanel.deps.json', 'mod.toml')
if (!$Rollback) {
    $payloadPath = Resolve-ExistingAbsolutePath $Payload 'Payload'
    Assert-NoReparsePoints $payloadPath 'payload'
    Assert-NoProtectedOverlap $payloadPath 'payload'
    if (Test-PathOverlap $contract.Profile $payloadPath) { throw 'The payload overlaps the profile.' }
    $items = @(Get-ChildItem -LiteralPath $payloadPath -Force)
    if ($items.Count -ne $files.Count -or @($items | Where-Object { $_.PSIsContainer -or $_.Name -cnotin $files }).Count) {
        throw 'Payload must contain only KsaDevPanel.dll, KsaDevPanel.deps.json and mod.toml.'
    }
    $descriptor = [IO.File]::ReadAllText((Join-Path $payloadPath 'mod.toml'))
    if ($descriptor -notmatch '(?m)^name\s*=\s*"KsaDevPanel"\s*$' -or
        $descriptor -notmatch '(?ms)^\[StarMap\]\s*EntryAssembly\s*=\s*"KsaDevPanel"\s*$') {
        throw 'The StarMap descriptor has the wrong ID or entry assembly.'
    }
}

if ($DryRun) {
    [pscustomobject]@{ Mode = 'DryRun'; Action = $PSCmdlet.ParameterSetName; Profile = $contract.Profile; Target = $target; Manifest = $manifest }
    return
}

$lock = [IO.File]::Open((Join-Path $contract.Profile '.ksa-dev-panel.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    if ($Rollback) {
        if (!(Test-Path -LiteralPath $backup -PathType Container) -or
            [IO.File]::ReadAllText((Join-Path $backup 'origin')) -cne $marker) {
            throw 'A matching rollback backup is required.'
        }

        $retired = $backup + '.restored-' + [guid]::NewGuid().ToString('N')
        $discard = Join-Path $backup ('discard-' + [guid]::NewGuid().ToString('N'))
        $previous = Join-Path $backup 'previous'
        $withoutEntry = Join-Path $backup 'manifest.without-entry'
        $priorEntry = [IO.File]::ReadAllText((Join-Path $backup 'manifest.entry.before'))
        $currentManifest = [IO.File]::ReadAllText($manifest)
        $currentEntry = [regex]::Match($currentManifest, '(?ms)^\[\[mods\]\][ \t]*\r?\nid[ \t]*=[ \t]*"KsaDevPanel"[ \t]*\r?\nenabled[ \t]*=[ \t]*true[ \t]*\r?\n?')
        if (!$currentEntry.Success) { throw 'The KsaDevPanel manifest entry was changed after deployment.' }
        $nextManifest = [regex]::Replace($currentManifest, '(?ms)^\[\[mods\]\][ \t]*\r?\nid[ \t]*=[ \t]*"KsaDevPanel"[ \t]*\r?\nenabled[ \t]*=[ \t]*true[ \t]*\r?\n?', '', 1)
        if ($priorEntry) {
            if ($nextManifest -and !$nextManifest.EndsWith("`n") -and !$nextManifest.EndsWith("`r")) { $nextManifest += "`r`n" }
            $nextManifest += $priorEntry
        }
        [IO.File]::WriteAllText($withoutEntry + '.restore', $nextManifest)
        if (Test-Path -LiteralPath $target) { [IO.Directory]::Move($target, $discard) }
        try {
            if (Test-Path -LiteralPath $previous) {
                Copy-Item -LiteralPath $previous -Destination $target -Recurse
            }
            [IO.File]::Move($withoutEntry + '.restore', $manifest, $true)
        }
        catch {
            if (Test-Path -LiteralPath $target) {
                [IO.Directory]::Move($target, (Join-Path $backup ('failed-' + [guid]::NewGuid().ToString('N'))))
            }
            if (Test-Path -LiteralPath $discard) { [IO.Directory]::Move($discard, $target) }
            throw
        }
        if (Test-Path -LiteralPath $discard) {
            Move-Item -LiteralPath $discard -Destination (Join-Path $rollbackRoot ('KsaDevPanel.replaced-' + [guid]::NewGuid().ToString('N')))
        }
        Remove-Item -LiteralPath $backup -Recurse -Force
    } else {
        if (Test-Path -LiteralPath $backup) { throw 'Rollback the previous deployment before applying another.' }
        $text = [IO.File]::ReadAllText($manifest)
        $entry = "[[mods]]`nid = `"KsaDevPanel`"`nenabled = true`n"
        $enabled = '(?m)^\[\[mods\]\][ \t]*\r?\n[ \t]*id[ \t]*=[ \t]*"KsaDevPanel"[ \t]*\r?\n[ \t]*enabled[ \t]*=[ \t]*true[ \t]*\r?$'
        if ($text -match 'KsaDevPanel' -and $text -cnotmatch $enabled) {
            throw 'The isolated manifest already mentions this mod. Resolve that entry before deployment.'
        }

        New-Item -ItemType Directory -Path $rollbackRoot -Force | Out-Null
        $pending = $backup + '.pending-' + [guid]::NewGuid().ToString('N')
        $stage = Join-Path $pending 'stage'
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $payloadPath $name) -Destination $stage }
        Copy-Item -LiteralPath $manifest -Destination (Join-Path $pending 'manifest.before')
        if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $pending 'previous') -Recurse }
        [IO.File]::WriteAllText((Join-Path $pending 'origin'), $marker)
        $oldEntryPattern = '(?ms)^\[\[mods\]\][ \t]*\r?\nid[ \t]*=[ \t]*"KsaDevPanel"[ \t]*\r?\nenabled[ \t]*=[ \t]*(?:true|false)[ \t]*\r?\n?'
        $oldEntry = [regex]::Match($text, $oldEntryPattern)
        if ($oldEntry.Success) {
            [IO.File]::WriteAllText((Join-Path $pending 'manifest.entry.before'), $oldEntry.Value)
            [IO.File]::WriteAllText((Join-Path $pending 'manifest.without-entry'), [regex]::Replace($text, $oldEntryPattern, '', 1))
        } else {
            [IO.File]::WriteAllText((Join-Path $pending 'manifest.entry.before'), '')
            [IO.File]::WriteAllText((Join-Path $pending 'manifest.without-entry'), $text)
        }
        $newManifest = Join-Path $pending 'manifest.after'
        $baseManifest = [IO.File]::ReadAllText((Join-Path $pending 'manifest.without-entry'))
        if ($baseManifest -and !$baseManifest.EndsWith("`n") -and !$baseManifest.EndsWith("`r")) {
            $baseManifest += "`r`n"
        }
        if ($text -cnotmatch $enabled) { $baseManifest += $entry }
        [IO.File]::WriteAllText($newManifest, $baseManifest)
        [IO.Directory]::Move($pending, $backup)
        $discard = Join-Path $backup 'replaced'
        if (Test-Path -LiteralPath $target) { [IO.Directory]::Move($target, $discard) }
        try {
            [IO.Directory]::Move((Join-Path $backup 'stage'), $target)
            [IO.File]::Move((Join-Path $backup 'manifest.after'), $manifest, $true)
        }
        catch {
            if (Test-Path -LiteralPath $target) { [IO.Directory]::Move($target, (Join-Path $backup 'failed')) }
            if (Test-Path -LiteralPath $discard) { [IO.Directory]::Move($discard, $target) }
            throw
        }
    }

    [pscustomobject]@{ Mode = $marker; Action = $PSCmdlet.ParameterSetName; Profile = $contract.Profile; Target = $target; Manifest = $manifest }
}
finally {
    $lock.Dispose()
}
