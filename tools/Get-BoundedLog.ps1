[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Log,
    [int] $MaxLines = 120,
    [string] $RunId = ''
)
if ($MaxLines -lt 1 -or $MaxLines -gt 1000) { throw 'MaxLines must be between 1 and 1000' }
$path = Resolve-Path -LiteralPath $Log -ErrorAction Stop
$lines = @(Get-Content -LiteralPath $path -Tail $MaxLines)
if (-not [string]::IsNullOrWhiteSpace($RunId)) { $lines = @($lines | Where-Object { $_ -like "*$RunId*" }) }
$lines -join [Environment]::NewLine
