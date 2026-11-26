[CmdletBinding()]
param([switch] $DryRun)

. (Join-Path $PSScriptRoot 'KsaDevPanel.Common.ps1')
$root = Join-Path (Get-RepoRoot) '.local'
$game = Join-Path $root 'game'
$profile = Join-Path $root 'profile'
$loader = Join-Path $root 'starmap'
$runtime = Join-Path $root 'runtime'
foreach ($path in @($game, $profile, $loader, $runtime)) {
    $resolved = Resolve-ExistingAbsolutePath $path 'isolated instance'
    Assert-NoProtectedOverlap $resolved 'isolated instance'
    Assert-NoReparsePoints $resolved 'isolated instance'
}

$gameDll = Join-Path $game 'KSA.dll'
$hashes = @{
    $gameDll = 'FA741419E287FB7F673FBA3212BE622B5FC27DB91FA4A6632828FB9DA7B15C7E'
    (Join-Path $loader 'StarMap.API.dll') = '8DEB69996FE35C8B0A31F28F4F5504746252F7BF0DB7BA08A00ADFE153D52B1D'
    (Join-Path $loader '0Harmony.dll') = 'F7D167B2500808C6C2AD6E38B6EFA2741A2B809F79B0E92C36E4A7079C38D9BB'
}
foreach ($file in $hashes.Keys) {
    if ((Get-FileHash -LiteralPath $file).Hash -ine $hashes[$file]) {
        throw "Pinned input mismatch: $(Split-Path $file -Leaf)"
    }
}
$expectedLoader = '0.5.4+477f495d1c8d59910ad7f37071b77535f59f8c5d'
foreach ($name in @('StarMap.dll', 'StarMap.Core.dll')) {
    if ((Get-Item -LiteralPath (Join-Path $loader $name)).VersionInfo.ProductVersion -ne $expectedLoader) {
        throw "Pinned loader mismatch: $name"
    }
}

$config = Get-Content -LiteralPath (Join-Path $loader 'StarMapConfig.json') -Raw | ConvertFrom-Json
if ($config.GameLocation -ine $gameDll -or @($config.GameArguments).Count -ne 0) {
    throw 'StarMapConfig.json must point to the disposable KSA.dll and have an empty GameArguments array.'
}
$marker = Join-Path $profile '.ksa-dev-panel-isolated'
if ((Get-Content -LiteralPath $marker -Raw).Trim() -ne 'KsaDevPanel 2026.11.25.5949') {
    throw 'The isolated profile marker is missing or has the wrong build.'
}

$dotnet = Join-Path $runtime 'dotnet.exe'
$runtimes = & $dotnet --list-runtimes
if ($LASTEXITCODE -ne 0 -or @($runtimes).Count -ne 1 -or $runtimes -notmatch '^Microsoft\.NETCore\.App 10\.0\.10 ') {
    throw 'The isolated launcher requires the dedicated .NET 10.0.10 runtime directory.'
}
if ($DryRun) {
    [pscustomobject]@{ Game = $game; Profile = $profile; Loader = $loader; Runtime = $runtime }
    return
}

$runLog = Join-Path $root ('launch-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N') + '.log')
$start = [Diagnostics.ProcessStartInfo]::new($dotnet)
$start.WorkingDirectory = $loader
$start.UseShellExecute = $false
$start.ArgumentList.Add((Join-Path $loader 'StarMap.dll'))
$start.Environment['KSA_DEV_PANEL_PROFILE'] = $profile
$start.Environment['STARMAP_INSTANCE_PATH'] = $profile
$start.Environment['DOTNET_ROOT'] = $runtime
$start.Environment['DOTNET_ROOT_X64'] = $runtime
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true

Write-Host "Isolated launch log: $runLog"
$lock = [IO.File]::Open((Join-Path $profile '.ksa-dev-panel.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $stdoutFile = [IO.File]::Open($runLog, 'CreateNew', 'Write', 'Read')
    $stderrFile = [IO.File]::Open($runLog + '.stderr', 'CreateNew', 'Write', 'Read')
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.BaseStream.CopyToAsync($stdoutFile)
    $stderr = $process.StandardError.BaseStream.CopyToAsync($stderrFile)
    $process.WaitForExit()
    $stdout.GetAwaiter().GetResult()
    $stderr.GetAwaiter().GetResult()
    exit $process.ExitCode
}
finally {
    foreach ($disposable in @('process', 'stderrFile', 'stdoutFile', 'lock')) {
        $value = Get-Variable -Name $disposable -ValueOnly -ErrorAction SilentlyContinue
        if ($null -ne $value) { $value.Dispose() }
    }
}
