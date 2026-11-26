[CmdletBinding()]
param([switch] $DryRun)

. (Join-Path $PSScriptRoot 'KsaDevPanel.Common.ps1')
$repo = Get-RepoRoot
$workspace = Split-Path (Split-Path $repo -Parent) -Parent
$root = Join-Path $workspace '.development'
$game = Join-Path $root 'game'
$profile = Join-Path $root 'profile'
$loader = Join-Path $root 'loader'
$runtime = Join-Path $root 'runtime'
$marker = Join-Path $root 'DEVELOPMENT-INSTANCE'
$configFile = Join-Path $workspace 'LocalPaths.local.json'
if (!(Test-Path $configFile)) { throw 'LocalPaths.local.json is required.' }
$config = Get-Content $configFile -Raw | ConvertFrom-Json
if (!(Test-Path $marker -PathType Leaf) -or
    (Get-Content $marker -Raw).Trim() -ne 'KSA development instance 2026.11.25.5949') {
    throw 'Shared development instance is not marked for this KSA build.'
}
$protected = @($config.referenceGameDirectory, $config.registeredGameDirectory)
foreach ($path in @($game, $profile, $loader, $runtime) + $protected) {
    $resolved = Resolve-ExistingAbsolutePath $path 'development instance path'
    Assert-NoReparsePoints $resolved 'development instance path'
}
$gameDll = Join-Path $game 'KSA.dll'
$expectedGame = 'FA741419E287FB7F673FBA3212BE622B5FC27DB91FA4A6632828FB9DA7B15C7E'
if ((Get-Item $gameDll).VersionInfo.FileVersion -ne '2026.11.25.5949' -or
    (Get-FileHash $gameDll).Hash -ine $expectedGame) {
    throw 'Shared development game copy does not match the pinned KSA build.'
}
$expectedLoader = '0.5.4+477f495d1c8d59910ad7f37071b77535f59f8c5d'
foreach ($name in @('StarMap.dll', 'StarMap.Core.dll')) {
    if ((Get-Item (Join-Path $loader $name)).VersionInfo.ProductVersion -ne $expectedLoader) {
        throw "Pinned StarMap input mismatch: $name"
    }
}
$hashes = @{
    (Join-Path $loader 'StarMap.API.dll') = '8DEB69996FE35C8B0A31F28F4F5504746252F7BF0DB7BA08A00ADFE153D52B1D'
    (Join-Path $loader '0Harmony.dll') = 'F7D167B2500808C6C2AD6E38B6EFA2741A2B809F79B0E92C36E4A7079C38D9BB'
    (Join-Path $game 'KSA.exe') = 'BDE8E4F334A43884DF4AE6911A243F98EF751A16B12CB53A9C8E78274976E557'
}
foreach ($file in $hashes.Keys) {
    if ((Get-FileHash $file).Hash -ine $hashes[$file]) { throw "Pinned instance input mismatch: $(Split-Path $file -Leaf)" }
}
if ((Get-Item (Join-Path $loader '0Harmony.dll')).VersionInfo.ProductVersion -ne '2.4.3.0+b79f2f1ac971e5bc224cda3efedbbb4084efbfb6') {
    throw 'Pinned Harmony version mismatch.'
}
if (!(Test-Path (Join-Path $profile 'manifest.toml') -PathType Leaf) -or
    !(Test-Path (Join-Path $profile 'mods') -PathType Container)) { throw 'Shared StarMap profile is not initialized.' }
$runtimeLines = & (Join-Path $runtime 'dotnet.exe') --list-runtimes
if ($LASTEXITCODE -ne 0 -or $runtimeLines -notmatch '^Microsoft\.NETCore\.App 10\.0\.10 ') { throw 'Pinned runtime 10.0.10 is missing.' }
$loaderConfig = Join-Path $loader 'StarMapConfig.json'
$config = Get-Content -LiteralPath $loaderConfig -Raw | ConvertFrom-Json
if ($config.GameLocation -cne $gameDll) {
    $config.GameLocation = $gameDll
    $config.GameArguments = @()
    $candidate = $loaderConfig + '.tmp'
    [IO.File]::WriteAllText($candidate, ($config | ConvertTo-Json -Depth 4))
    if ((Get-Content -LiteralPath $candidate -Raw | ConvertFrom-Json).GameLocation -cne $gameDll) {
        Remove-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
        throw 'StarMapConfig.json candidate failed validation.'
    }
    Move-Item -LiteralPath $candidate -Destination $loaderConfig -Force
}
if ($DryRun) {
    [pscustomobject]@{ Status='PASS'; Game=$game; Profile=$profile; Loader=$loader; Runtime=$runtime }
    return
}

$logPath = Join-Path $root ('launch-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N') + '.log')
$start = [Diagnostics.ProcessStartInfo]::new((Join-Path $runtime 'dotnet.exe'))
$start.WorkingDirectory = $loader
$start.UseShellExecute = $false
$start.ArgumentList.Add((Join-Path $loader 'StarMap.dll'))
$start.ArgumentList.Add('-InstancePath')
$start.ArgumentList.Add($profile)
$start.ArgumentList.Add($gameDll)
$start.Environment['KSA_DEV_PANEL_PROFILE'] = $profile
$start.Environment['STARMAP_INSTANCE_PATH'] = $profile
$start.Environment['DOTNET_ROOT'] = $runtime
$start.Environment['DOTNET_ROOT_X64'] = $runtime
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true

$lock = [IO.File]::Open((Join-Path $profile '.ksa-dev-panel.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $stdoutFile = [IO.File]::Open($logPath, 'CreateNew', 'Write', 'Read')
    $stderrFile = [IO.File]::Open($logPath + '.stderr', 'CreateNew', 'Write', 'Read')
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.BaseStream.CopyToAsync($stdoutFile)
    $stderr = $process.StandardError.BaseStream.CopyToAsync($stderrFile)
    Write-Output "Isolated launch log: $logPath"
    $process.WaitForExit()
    $stdout.GetAwaiter().GetResult()
    $stderr.GetAwaiter().GetResult()
    exit $process.ExitCode
}
finally {
    foreach ($name in @('process','stderrFile','stdoutFile','lock')) {
        $value = Get-Variable $name -ValueOnly -ErrorAction SilentlyContinue
        if ($null -ne $value) { $value.Dispose() }
    }
}
