[CmdletBinding()]
param(
    [string] $Dotnet = '',
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [switch] $Stage,
    [switch] $DryRun
)
. (Join-Path $PSScriptRoot 'KsaDevPanel.Common.ps1')
$repo = Get-RepoRoot
$project = Join-Path $repo 'src\KsaDevPanel\KsaDevPanel.csproj'
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Project not found: $project" }
$pathsFile = Join-Path (Split-Path $repo -Parent | Split-Path -Parent) 'LocalPaths.local.json'
if (-not (Test-Path -LiteralPath $pathsFile -PathType Leaf)) { throw "Missing ignored local path file: $pathsFile" }
$paths = Get-Content -LiteralPath $pathsFile -Raw | ConvertFrom-Json
$game = Resolve-ExistingAbsolutePath $paths.referenceGameDirectory 'referenceGameDirectory'
$dll = Join-Path $game 'KSA.dll'
$version = (Get-Item -LiteralPath $dll).VersionInfo.FileVersion
if ($version -ne '2026.11.25.5949' -or
    (Get-FileHash -LiteralPath $dll).Hash -ine 'FA741419E287FB7F673FBA3212BE622B5FC27DB91FA4A6632828FB9DA7B15C7E') {
    throw "KSA.dll does not match the pinned build: $version"
}
$localDotnet = Join-Path $repo '.local\dotnet\dotnet.exe'
$dotnetPath = if ([string]::IsNullOrWhiteSpace($Dotnet)) {
    if (Test-Path -LiteralPath $localDotnet -PathType Leaf) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
} else { Resolve-ExistingAbsolutePath $Dotnet 'dotnet' }
$starMap = Join-Path $repo '.local\starmap'
$expectedStarMap = '0.5.4+477f495d1c8d59910ad7f37071b77535f59f8c5d'
foreach ($required in @('StarMap.dll', 'StarMap.API.dll', 'StarMap.Core.dll')) {
    if ((Get-Item -LiteralPath (Join-Path $starMap $required)).VersionInfo.ProductVersion -ne $expectedStarMap) {
        throw "Pinned StarMap input mismatch: $required"
    }
}
if ((Get-Item -LiteralPath (Join-Path $starMap '0Harmony.dll')).VersionInfo.ProductVersion -ne
    '2.4.3.0+b79f2f1ac971e5bc224cda3efedbbb4084efbfb6') {
    throw 'Pinned Harmony input mismatch.'
}
if ($DryRun) {
    [pscustomobject]@{ Status = 'PASS'; Mode = 'DryRun'; Dotnet = $dotnetPath; Project = $project; Game = $game; GameBuild = $version; StarMap = $starMap; StarMapVersion = '0.5.4'; HarmonyVersion = '2.4.3'; Configuration = $Configuration }
    exit 0
}
$oldCliHome = $env:DOTNET_CLI_HOME
$oldFirstUse = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$oldAppData = $env:APPDATA
$oldLocalAppData = $env:LOCALAPPDATA
$oldAspNetCert = $env:DOTNET_GENERATE_ASPNET_CERTIFICATE
$env:DOTNET_CLI_HOME = Join-Path $repo '.local\dotnet-cli-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:APPDATA = Join-Path $repo '.local\appdata'
$env:LOCALAPPDATA = Join-Path $repo '.local\localappdata'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
try {
    $assets = Join-Path (Split-Path $project -Parent) 'obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $assets -PathType Leaf)) {
        & $dotnetPath restore $project '--configfile' (Join-Path $repo 'NuGet.Config') '--ignore-failed-sources'
        if ($LASTEXITCODE -ne 0) { throw "Offline restore failed with exit code $LASTEXITCODE" }
    }
    & $dotnetPath build $project '--no-restore' '--disable-build-servers' "-p:KsaGameDirectory=$game" "-p:StarMapDirectory=$starMap" '-c' $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
    if ($Stage) {
        $output = Join-Path (Split-Path $project -Parent) "bin\$Configuration\net10.0"
        $staged = Join-Path $repo ('.artifacts\stage-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $staged -Force | Out-Null
        foreach ($file in @('KsaDevPanel.dll', 'KsaDevPanel.deps.json', 'mod.toml')) {
            Copy-Item -LiteralPath (Join-Path $output $file) -Destination $staged
        }
        Write-Output $staged
    }
}
finally {
    $env:DOTNET_CLI_HOME = $oldCliHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $oldFirstUse
    $env:APPDATA = $oldAppData
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = $oldAspNetCert
}
