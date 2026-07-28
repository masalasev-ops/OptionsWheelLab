<#
.SYNOPSIS
    Migrates the store, snapshotting first.

.DESCRIPTION
    The snapshot guarantee lives in the migration runner, not here, so running
    the migration by hand cannot skip it. This script is the operator entry
    point: it supplies the instant and invokes the Worker's migrate verb.

    The instant is supplied rather than read inside the process because the
    clock abstraction lands at 0.5, and a DateTime.UtcNow in the runner would
    be a call 0.5 has to remove.

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

# The stored timestamp form, per DATA_AND_SCHEMA.md. The runner renders the same
# instant without separators for the snapshot directory name, because a colon is
# illegal in a Windows path.
$instant = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')

Write-Host "Migrating with instant $instant"

dotnet run --project $worker --configuration $Configuration -- migrate --at $instant

if ($LASTEXITCODE -ne 0) {
    throw "Migration failed with exit code $LASTEXITCODE"
}
