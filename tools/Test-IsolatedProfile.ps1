[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Profile,
    [switch] $DryRun
)
. (Join-Path $PSScriptRoot 'KsaDevPanel.Common.ps1')

$contract = Assert-ProfileEnvironment $Profile
if ($DryRun) {
    [pscustomobject]@{ Status = 'PASS'; Mode = 'DryRun'; Profile = $contract.Profile; Mods = $contract.Mods; Settings = $contract.Settings }
    exit 0
}
throw 'Only -DryRun is permitted until an authorized disposable runtime exists.'
