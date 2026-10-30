#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SnapshotPath,
    [string] $ConnectionString = $env:ConnectionStrings__Progress
)
$ErrorActionPreference = "Stop"
$previous = $env:ConnectionStrings__Progress
try {
    $env:ConnectionStrings__Progress = $ConnectionString
    $project = Join-Path $PSScriptRoot "../../src/Services/Progress/Progress.Api"
    dotnet run --project $project --no-launch-profile -- --reconcile-meal-snapshot $SnapshotPath
    $result = $LASTEXITCODE
} finally {
    $env:ConnectionStrings__Progress = $previous
}
exit $result
