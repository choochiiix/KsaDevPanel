[CmdletBinding()]
param()
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repo 'tests\KsaDevPanel.Inspection.Tests\KsaDevPanel.Inspection.Tests.csproj'
$dotnet = Join-Path $repo '.local\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$oldCliHome = $env:DOTNET_CLI_HOME
$oldFirstUse = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$oldAppData = $env:APPDATA
$oldLocalAppData = $env:LOCALAPPDATA
$env:DOTNET_CLI_HOME = Join-Path $repo '.local\dotnet-cli-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:APPDATA = Join-Path $repo '.local\appdata'
$env:LOCALAPPDATA = Join-Path $repo '.local\localappdata'
try {
    $assets = Join-Path (Split-Path $project -Parent) 'obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $assets -PathType Leaf)) {
        & $dotnet restore $project '--configfile' (Join-Path $repo 'NuGet.Config') '--ignore-failed-sources'
        if ($LASTEXITCODE -ne 0) { throw "Inspection contract restore failed with exit code $LASTEXITCODE" }
    }
    & $dotnet run --project $project '--no-restore' '--nologo'
    if ($LASTEXITCODE -ne 0) { throw "Inspection contract tests failed with exit code $LASTEXITCODE" }
}
finally {
    $env:DOTNET_CLI_HOME = $oldCliHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $oldFirstUse
    $env:APPDATA = $oldAppData
    $env:LOCALAPPDATA = $oldLocalAppData
}
