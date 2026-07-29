<#
.SYNOPSIS
    Writes the initial version of every configuration key Phase 0.8 sets.

.DESCRIPTION
    Run once after migrate.ps1, against a migrated store. A second run is a
    no-op: the verb writes the first version of each key that has none and
    skips any key that already has one, reporting both counts.

    It exists for the same reason migrate.ps1 does, and not for symmetry. With
    Storage__Path unset the verb throws from StoreLocation, which carries the
    right words but arrives under a stack trace; the check here reports the same
    thing the same way migrate.ps1 does. These are the two steps of setting up a
    store, run in sequence by one person, and the second failing worse than the
    first for the identical mistake is the whole defect.

    A refusal is a different outcome and the verb reports it itself: a value
    contradicting one already stored fails the cross-key invariants and no row is
    written [D-W23, D-W24, D-W34].

    The instant is read from the injected clock inside the verb, which is a site
    D-W30 sanctions. Nothing outside the process can name the instant a row is
    stamped with.

.PARAMETER Configuration
    Build configuration. Release by default.

.EXAMPLE
    .\seed.ps1
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

dotnet run --project $worker --configuration $Configuration -- seed

if ($LASTEXITCODE -ne 0) {
    throw "Seeding failed with exit code $LASTEXITCODE"
}
