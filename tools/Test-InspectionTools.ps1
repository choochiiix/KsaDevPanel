[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'KsaDevPanel.Common.ps1')

$fixture = Join-Path ([IO.Path]::GetTempPath()) ('KsaDevPanel-inspection-' + [Guid]::NewGuid().ToString('N'))
$profile = Join-Path $fixture 'profile'
$payload = Join-Path $fixture 'payload'
$log = Join-Path $fixture 'run.log'
try {
    New-Item -ItemType Directory -Path (Join-Path $profile 'mods\KsaDevPanel') -Force | Out-Null
    New-Item -ItemType Directory -Path $profile -Force | Out-Null
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $profile '.ksa-dev-panel-isolated') -Value 'KsaDevPanel 2026.11.25.5949' -NoNewline
    Set-Content -LiteralPath (Join-Path $profile 'KsaDevPanel.settings.json') -Value '{}'
    $manifest = Join-Path $profile 'manifest.toml'
    Set-Content -LiteralPath $manifest -Value "[[mods]]`nid = 'Core'`nenabled = true"
    $manifestHashBefore = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
    Set-Content -LiteralPath (Join-Path $profile 'mods\KsaDevPanel\prior.txt') -Value 'prior'
    Set-Content -LiteralPath (Join-Path $payload 'KsaDevPanel.dll') -Value 'synthetic'
    Set-Content -LiteralPath (Join-Path $payload 'KsaDevPanel.deps.json') -Value '{}'
    Set-Content -LiteralPath (Join-Path $payload 'mod.toml') -Value @('name = "KsaDevPanel"', '[StarMap]', 'EntryAssembly = "KsaDevPanel"')
    Set-Content -LiteralPath $log -Value @('RUN-TEST one', 'RUN-OTHER two', 'RUN-TEST three')
    $env:KSA_DEV_PANEL_PROFILE = $profile
    $env:STARMAP_INSTANCE_PATH = $profile

    & (Join-Path $PSScriptRoot 'Test-IsolatedProfile.ps1') -Profile $profile -DryRun | Out-Null
    $dryRun = & (Join-Path $PSScriptRoot 'Deploy-KsaDevPanel.ps1') -Profile $profile -Payload $payload -DryRun -ApplySynthetic
    if ($dryRun.Target -ne (Join-Path $profile 'mods\KsaDevPanel')) { throw 'Dry-run target assertion failed' }
    & (Join-Path $PSScriptRoot 'Deploy-KsaDevPanel.ps1') -Profile $profile -Rollback -DryRun -ApplySynthetic | Out-Null
    & (Join-Path $PSScriptRoot 'Deploy-KsaDevPanel.ps1') -Profile $profile -Payload $payload -ApplySynthetic | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $profile 'mods\KsaDevPanel\KsaDevPanel.dll') -PathType Leaf)) { throw 'Synthetic deploy assertion failed' }
    if ((Get-Content -LiteralPath $manifest -Raw) -notmatch 'id = "KsaDevPanel"') { throw 'Deployment did not enable the manifest entry' }
    & (Join-Path $PSScriptRoot 'Deploy-KsaDevPanel.ps1') -Profile $profile -Rollback -ApplySynthetic | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $profile 'mods\KsaDevPanel\prior.txt') -PathType Leaf)) { throw 'Synthetic rollback assertion failed' }
    $manifestHashAfter = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
    if ($manifestHashBefore -ne $manifestHashAfter) { throw 'Manifest changed during synthetic deploy/rollback' }
    $unsafePayload = Join-Path $profile 'unsafe-payload'
    New-Item -ItemType Directory -Path $unsafePayload -Force | Out-Null
    $unsafeRejected = $false
    try { & (Join-Path $PSScriptRoot 'Deploy-KsaDevPanel.ps1') -Profile $profile -Payload $unsafePayload -ApplySynthetic | Out-Null }
    catch { $unsafeRejected = $true }
    if (-not $unsafeRejected) { throw 'Payload overlap rejection assertion failed' }
    Set-Content -LiteralPath (Join-Path $profile '.ksa-dev-panel-isolated') -Value 'KsaDevPanel wrong-build'
    $wrongBuildRejected = $false
    try { & (Join-Path $PSScriptRoot 'Deploy-KsaDevPanel.ps1') -Profile $profile -Payload $payload -ApplySynthetic | Out-Null }
    catch { $wrongBuildRejected = $_.Exception.Message -match 'Invalid profile marker' }
    if (!$wrongBuildRejected -or (Get-FileHash -LiteralPath $manifest).Hash -ne $manifestHashBefore) {
        throw 'Wrong-build deployment did not stop before changing the manifest'
    }
    $bounded = & (Join-Path $PSScriptRoot 'Get-BoundedLog.ps1') -Log $log -MaxLines 2 -RunId 'RUN-TEST'
    if (@($bounded).Count -ne 1 -or $bounded -notlike '*three*') { throw 'Bounded log filtering assertion failed' }
    'PASS synthetic inspection tooling fixtures'
}
finally {
    $resolved = Resolve-Path -LiteralPath $fixture -ErrorAction SilentlyContinue
    $tempRoot = [IO.Path]::GetTempPath().TrimEnd('\') + '\'
    if ($resolved -and $resolved.Path.StartsWith($tempRoot + 'KsaDevPanel-inspection-', [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolved.Path -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item Env:KSA_DEV_PANEL_PROFILE -ErrorAction SilentlyContinue
    Remove-Item Env:STARMAP_INSTANCE_PATH -ErrorAction SilentlyContinue
}
