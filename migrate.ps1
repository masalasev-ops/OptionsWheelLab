<#
.SYNOPSIS
    Migrates the store, snapshotting first.

.DESCRIPTION
    The snapshot guarantee lives in the migration runner, not here, so running
    the migration by hand cannot skip it. This script is the operator entry
    point: it checks the store path is set and invokes the Worker's migrate
    verb.

    The instant is no longer supplied. It was, until 0.5, for want of a clock;
    the verb now reads the injected clock, which is a site D-W30 sanctions.
    Nothing outside the process can name the instant a row is stamped with.

.PARAMETER Configuration
    Build configuration. Release by default.

.EXAMPLE
    .\migrate.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$worker = Join-Path $PSScriptRoot 'src/OptionsWheelLab.Worker'

if (-not (Test-Path $worker)) {
    throw "Worker project not found at $worker"
}

if (-not $env:Storage__Path) {
    throw @'
Storage__Path is not set. Set it to the absolute directory holding the store,
for example:

    $env:Storage__Path = 'E:\OptionsWheelLabStore'

It is not committed because a committed absolute path would start on one
machine only.
'@
}

dotnet run --project $worker --configuration $Configuration -- migrate

if ($LASTEXITCODE -ne 0) {
    throw "Migration failed with exit code $LASTEXITCODE"
}
