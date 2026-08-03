<#
.SYNOPSIS
    Source guards that are not unit tests.

.DESCRIPTION
    Checks that must hold over the source text rather than over behaviour, and
    that report even when nothing else can. CI calls this script rather than
    restating the checks, so there is one definition of each.

    "EVEN WHEN THE BUILD DOES NOT" WAS IMPRECISE, and 0.7 measured it. A Roslyn
    analyser DOES still report when the compilation has errors elsewhere: a
    probe with a violation in one file and a type error in another reported both.
    What it cannot survive is a failed restore, where no analyser runs at all and
    only the NuGet error appears. This script runs before restore, so the
    property it actually has is that it reports when restore does not succeed.

    Today it holds two: no floating point in the tree [CLAUDE.md 2, D-W29], and
    no ambient clock call outside the clock implementation [CLAUDE.md 2, D-W30].

    THE APPEND-ONLY CHECK IS NOT HERE, and this is structural rather than a
    choice. Remove-NonCode strips raw string literals, every SQL statement in
    this repository lives in one, and a pattern added below would therefore
    match nothing in the tree by construction -- the third self-test proves it,
    since its sample's BEFORE UPDATE ON sits inside a raw string and only the
    line after it survives. FX-NoRewriteOfAppendOnlyTables is a fixture for the
    same reason FX-NoDecimalOrderingInSql is.

    EACH CHECK IS NAMED, and the name is its row in FIXTURES.md, where its Kind
    is `guard`. FX-RegistryMatchesDisk asserts both directions: a check here with
    no row fails, and a row of Kind `guard` whose checkpoint has landed must have
    a check here.

    WHAT THIS CANNOT CATCH. It scans for tokens, so it sees declared intent and
    not inferred types. `values.Average()` over a sequence of floating-point
    numbers carries none of the tokens below and passes, as does System.Text.Json
    binding a vendor number into an untyped tree. Anyone reading a green run as
    proof that no floating point reaches a monetary path is wrong. The mechanism
    that would see those is a Roslyn analyser, and 0.7 made the comparison it was
    deferred for. Of the four checks this repository has, an analyser would gain
    this one and only this one: the two SQL checks are SQL-parsing problems where
    an analyser hands back the same string literal a fixture already gets, and
    the clock check gains only alias resolution. The guards therefore stay a text
    scan and a fixture [D-W33], which also records what would reopen it -- this
    gap becoming a live defect rather than a documented one.

    IT READS *.cs UNDER src AND tests, AND NOTHING ELSE. No .ps1 is scanned,
    including this one and migrate.ps1. That is correct rather than incidental:
    the operator entry point sits outside the determinism boundary, and
    migrate.ps1 no longer computes an instant in any case. It is stated because
    "the guard passes" would otherwise be read as covering the scripts.

    A CATCH-LIST, NOT AN EXEMPTION LIST, and the difference is the whole point.
    An incomplete catch-list still catches what is on it; an incomplete exemption
    list is a hole. So these lists may be added to freely and there is
    deliberately no way to opt a file out. The first legitimate floating-point
    value in this repository -- Phase 6 standard errors and rank correlations are
    the likely one -- should cost a recorded decision about where statistics end
    and money begins, rather than a line in a suppression file written in a
    hurry.

    THE ONE PERMITTED FILE IS NOT AN EXEMPTION MECHANISM. The clock rule is "no
    ambient clock call outside the clock implementation", so naming that
    implementation is part of stating the rule rather than an escape from it. It
    is one hardcoded path, not a list: adding a second means editing this script,
    which is changing the rule. It also has to earn its place -- scanning it must
    find an ambient call, or the carve-out is stale and this script throws.

.PARAMETER Configuration
    Unused today. Present so CI's invocation does not change when a guard that
    needs a build is added.

.EXAMPLE
    .\guards.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# --------------------------------------------------------------------------
# Self-test samples, one pair per check.
# --------------------------------------------------------------------------

$floatingPointMustFire = @'
public sealed class Probe
{
    private double _weight;
    private readonly float _ratio = 1e-8f;
    public double Mean(IEnumerable<double> values) => Math.Sqrt(Convert.ToDouble(values));
}
'@

$floatingPointMustNotFire = @'
/// <summary>Delta is <see cref="decimal"/>, never <see cref="double"/>.</summary>
/* A block comment mentioning float and double. */
public sealed record PolicyBand(string Name, decimal DeltaMin, decimal DeltaMax);
// A line comment mentioning Math.Sqrt and Random.NextDouble.
'@

$ambientClockMustFire = @'
public sealed class Probe
{
    public DateTimeOffset Read() => DateTimeOffset.UtcNow;
    public DateTime Started { get; } = System.DateTime.Now;
    public DateOnly Session() => DateOnly.FromDateTime(DateTime.Today);
    private readonly TimeProvider _time = TimeProvider.System;
}
'@

# The two shapes most likely to be mistaken for violations, and they are the
# reason the patterns are anchored on the type name. `clock.UtcNow` is the
# sanctioned call, and DateOnly.FromDateTime over a supplied instant is what
# AsOfBoundaryTests and ConfigWriteTests already do three times.
$ambientClockMustNotFire = @'
/// <summary>Never call DateTime.UtcNow or DateTimeOffset.Now outside the clock.</summary>
/* A block comment mentioning TimeProvider.System and DateTime.Today. */
public sealed class Reader(IClock clock)
{
    public DateTimeOffset When => clock.UtcNow;
    public DateOnly Of(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);
}
// A line comment mentioning DateTime.Now and Stopwatch.GetTimestamp.
'@

# The three shapes 3.3 shipped and its review found, one per site: an assignment,
# a call-away and a forced close, each pricing an option from a share count.
$shareCountMustFire = @'
public static class Probe
{
    public static decimal Paid(ContractIdentity put) => put.Strike * put.DeliverableShares;
    public static decimal Proceeds(ContractIdentity call, TrialState s) => call.Strike * s.Shares;
    public static decimal Debit(decimal intrinsic, ContractIdentity c) => intrinsic * c.DeliverableShares;
}
'@

# Every legitimate multiplication by a share count in this repository. A dividend
# and a mark are per share and multiply by shares correctly; the aggregate
# exercise price multiplies by the multiplier; and a basis divides rather than
# multiplies. The comment is here because the doc that explains this rule names
# the forbidden shape.
$shareCountMustNotFire = @'
/// <summary>Never write Strike * DeliverableShares: an adjustment moves one and not the other.</summary>
/* A block comment mentioning call.Strike * state.Shares as the error. */
public static class Probe
{
    public static decimal Aggregate(ContractIdentity c) => c.Strike * StandardMultiplier;
    public static decimal Dividend(decimal perShare, int shares) => perShare * shares;
    public static decimal Marked(decimal close, int shares) => close * shares;
    public static decimal Basis(decimal paid, int deliverable) => paid / deliverable;
    public static int Delivered(ContractIdentity c) => c.DeliverableShares;
}
// A line comment mentioning put.Strike * put.DeliverableShares.
'@

# --------------------------------------------------------------------------
# The checks. Each Name is a row in FIXTURES.md of Kind `guard`.
# --------------------------------------------------------------------------

$checks = @(
    @{
        Name = 'FX-NoFloatingPoint'
        Subject = 'floating point'
        PermittedFile = $null
        MustFire = $floatingPointMustFire
        MustNotFire = $floatingPointMustNotFire
        Patterns = @(
            @{ Pattern = '\b(double|float)\b';                  Meaning = 'a floating-point type' }
            @{ Pattern = '\bSystem\.(Double|Single)\b';          Meaning = 'a floating-point type by its CLR name' }
            @{ Pattern = '\bNextDouble\b';                       Meaning = 'Random.NextDouble' }
            @{ Pattern = '\bConvert\.To(Double|Single)\b';       Meaning = 'a conversion to floating point' }
            @{ Pattern = '\bGet(Double|Float)\b';                Meaning = 'a floating-point read from a data reader' }
            @{ Pattern = '\bMath\.(Sqrt|Pow|Exp|Log|Log10|Log2)\b'; Meaning = 'a Math function returning a double' }
            @{ Pattern = '(?<![\w.])\d+(\.\d+)?[eE][-+]?\d+(?![\w])'; Meaning = 'an exponent literal, which is a double' }
            @{ Pattern = '(?<![\w.])\d+(\.\d+)?[dDfF](?![\w])';  Meaning = 'a floating-point literal suffix' }
        )
        Explanation = @'
Floating point is not permitted in this repository [CLAUDE.md 2, D-W29]. Money is
decimal in TEXT, and a decimal path that admits a double loses cents silently
rather than failing.

There is no exemption mechanism, deliberately. If this value genuinely is not
money -- a standard error or a rank correlation, say -- that is a decision about
where statistics end and money begins, and it belongs in DECISIONS.md before it
belongs in the code.
'@
    }

    @{
        Name = 'FX-NoAmbientClock'
        Subject = 'an ambient clock call'
        PermittedFile = 'src/OptionsWheelLab.Core/Time/SystemClock.cs'
        MustFire = $ambientClockMustFire
        MustNotFire = $ambientClockMustNotFire
        Patterns = @(
            @{ Pattern = '\b(System\.)?DateTime\s*\.\s*(Now|UtcNow|Today)\b';  Meaning = 'an ambient DateTime read' }
            @{ Pattern = '\b(System\.)?DateTimeOffset\s*\.\s*(Now|UtcNow)\b';  Meaning = 'an ambient DateTimeOffset read' }
            @{ Pattern = '\bTimeProvider\b';                                    Meaning = 'a second time abstraction' }
        )
        Explanation = @'
The clock is injected, and it is read at composition and entry points only
[CLAUDE.md 2, D-W30]. Nothing below them reads a clock; they take instants as
parameters.

This is not only about determinism. The lab has two kinds of time -- when this
run is happening, and which day is being simulated -- and a component that wants
the second and reaches for the first gets an answer that is plausible, non-null
and wrong.

TimeProvider is on the list as a type rather than as TimeProvider.System,
because the sanctioned abstraction here is IClock and a second one in the tree is
the drift this exists to prevent. It is also why IClock was chosen over
TimeProvider: an ambient TimeProvider and an injected one are the same type,
separated only by which member is touched, and distinguishing them would need
type inference that a text scan cannot do.

Stopwatch and Environment.TickCount are deliberately absent. They read a
monotonic counter with no epoch, so no date can be derived from them and they
cannot commit the error above. A duration reaching a stored row would be a
determinism defect, but that is the row comparison's business and not this
guard's.
'@
    }

    @{
        Name = 'FX-NoShareCountInOptionCash'
        Subject = 'an option priced from a share count'
        Scope = 'src/'
        PermittedFile = $null
        MustFire = $shareCountMustFire
        MustNotFire = $shareCountMustNotFire
        Patterns = @(
            @{ Pattern = '\bStrike\b\s*\*\s*[\w.]*\b\w*[Ss]hares\b';   Meaning = 'a strike multiplied by a share count' }
            @{ Pattern = '\b[\w.]*\w*[Ss]hares\b\s*\*\s*[\w.]*\bStrike\b'; Meaning = 'a share count multiplied by a strike' }
            @{ Pattern = '\bDeliverableShares\b\s*\*';                  Meaning = 'the deliverable used as a money multiplier' }
            @{ Pattern = '\*\s*[\w.]*\bDeliverableShares\b';            Meaning = 'the deliverable used as a money multiplier' }
        )
        Explanation = @'
An adjustment moves the deliverable and leaves the strike and the aggregate
exercise price where it found them [D-W17], so cash from a contract multiplies by
the MULTIPLIER and the deliverable says only how many shares change hands. The
two are both one hundred for a standard contract, which is why this reads
correctly and is wrong for every adjusted one.

Use ContractTerms.AggregateExercisePrice for what exercising costs or realises,
and ContractTerms.CashFor for a per-share option price. Multiplying a share count
is right for a dividend and for marking shares at a close, because those are per
share; it is never right for an option.

This exists because 3.3 corrected exactly this error at one site, argued in that
type's own remarks that the quantity therefore sat in one place, and then made
the same error three more times in new code within the same checkpoint. Every
test passed throughout, because no contract in the suite had a deliverable
differing from its multiplier. The claim is held here now rather than by the
comment that made it.

It scans src only. A test may legitimately compute the wrong figure to assert
that the right one differs, which is what the adjusted-contract cases do.
'@
    }
)

# --------------------------------------------------------------------------
# Shared machinery.
# --------------------------------------------------------------------------

# Comments are stripped before scanning, so a doc comment may say "double" while
# explaining that a value is decimal and never double. PolicyBand.cs does, and so
# does every comment naming DateTime.UtcNow to say it is not called.
function Remove-NonCode {
    param([string]$Text)

    # Raw string literals first. Their bodies hold SQL containing quotes and --
    # sequences that the later patterns would misread, and a raw string left
    # unclosed would desync everything after it.
    $stripped = [regex]::Replace($Text, '"{3,}.*?"{3,}', '""', 'Singleline')

    # Block comments, then line comments. Line endings are LF by .gitattributes,
    # so no CRLF handling is needed.
    $stripped = [regex]::Replace($stripped, '/\*.*?\*/', '', 'Singleline')
    $stripped = [regex]::Replace($stripped, '//[^\n]*', '')

    # Ordinary and verbatim literals last, so a token inside quoted text is not
    # read as code. FX-MoneyRoundTrip asserts that ParseStored refuses "1e3",
    # and that string is data, not an exponent literal.
    $stripped = [regex]::Replace($stripped, '@?"(?:[^"\\\r\n]|\\.|"")*"', '""')

    return $stripped
}

function Find-Offences {
    param([string]$Text, [string]$Label, [object[]]$Patterns)

    $stripped = Remove-NonCode -Text $Text
    $found = @()

    foreach ($rule in $Patterns) {
        foreach ($match in [regex]::Matches($stripped, $rule.Pattern)) {
            $line = ($stripped.Substring(0, $match.Index) -split "`n").Count
            $found += "{0}:{1} has '{2}', {3}" -f $Label, $line, $match.Value, $rule.Meaning
        }
    }

    return $found
}

# --------------------------------------------------------------------------
# Self-test before scanning anything real. A scan that matched nothing would
# report success while testing nothing, and this corpus has already had one edit
# match nothing, no-op, and be recorded as done.
# --------------------------------------------------------------------------

foreach ($check in $checks) {
    if (@(Find-Offences -Text $check.MustFire -Label 'self-test' -Patterns $check.Patterns).Count -eq 0) {
        throw "$($check.Name) did not fire on a known violation, so it is not detecting anything."
    }

    $falsePositives = @(Find-Offences -Text $check.MustNotFire -Label 'self-test' -Patterns $check.Patterns)

    if ($falsePositives.Count -ne 0) {
        throw "$($check.Name) fired on something it must not: $($falsePositives -join '; ')"
    }
}

# Stripping literals introduces a failure mode of its own: a stripper that
# desyncs on a raw string swallows the code after it and still reports success,
# having scanned the wrong bytes. This sample carries the shapes that would cause
# that -- a raw string holding SQL with single quotes and -- comments, then a
# verbatim string, then a violation AFTER both. If the violation is not found,
# the stripper has eaten live code. Tested against one check's patterns because
# Remove-NonCode is shared, so proving it once proves it for all of them.
$mustFireAfterAwkwardStrings = @'
public static class Probe
{
    public const string Sql = """
        CREATE TRIGGER t BEFORE UPDATE ON config_rows
        BEGIN
            -- 1e9 and Math.Sqrt appear here as text, not as code.
            SELECT RAISE(ABORT, 'append-only: a change inserts version + 1');
        END;
        """;
    public const string Path = @"C:\a\double\path";
    public const string Quoted = "he said \"1e3\" and meant it";
    private double _slippedThrough;
}
'@

$afterStrings = @(Find-Offences `
    -Text $mustFireAfterAwkwardStrings `
    -Label 'self-test' `
    -Patterns $checks[0].Patterns)

if ($afterStrings.Count -ne 1) {
    throw @"
The literal stripper is not keeping its place. It should have found exactly one
violation after a raw string, a verbatim string and an escaped quote, and found
$($afterStrings.Count): $($afterStrings -join '; ')

A stripper that desyncs still scans every file and still reports success, so
this is the only thing standing between a green run and a guard that is reading
the wrong bytes.
"@
}

# --------------------------------------------------------------------------
# The scan.
# --------------------------------------------------------------------------

$roots = @('src', 'tests') | ForEach-Object { Join-Path $PSScriptRoot $_ }

foreach ($root in $roots) {
    if (-not (Test-Path $root)) {
        throw "Expected to scan $root and it does not exist."
    }
}

$files = @(Get-ChildItem -Path $roots -Filter *.cs -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })

if ($files.Count -eq 0) {
    throw "No C# files were found under $($roots -join ', '), so the scan asserted over nothing."
}

# Trimmed by hand rather than with [IO.Path]::GetRelativePath, which does not
# exist in Windows PowerShell 5.1. The script has to run under that locally and
# under pwsh on the runner. Separators are normalised to forward slashes so a
# permitted path is written one way and matches on both.
$prefix = $PSScriptRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar

$scanned = @()

foreach ($file in $files) {
    $relative = $file.FullName
    if ($relative.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        $relative = $relative.Substring($prefix.Length)
    }

    $scanned += @{
        Relative = $relative.Replace('\', '/')
        Text = (Get-Content -Raw -Path $file.FullName)
    }
}

foreach ($check in $checks) {
    # A permitted file has to earn its place. If scanning it finds nothing, the
    # path is stale or the implementation stopped calling the ambient API, and
    # the carve-out is silently covering nothing. This is the vacuity guard every
    # scanning check here carries, applied to the exemption itself.
    if ($null -ne $check.PermittedFile) {
        $permitted = @($scanned | Where-Object { $_.Relative -eq $check.PermittedFile })

        if ($permitted.Count -ne 1) {
            throw @"
$($check.Name) permits $($check.PermittedFile) and the scan found $($permitted.Count)
files at that path. The path is wrong, or the file moved and the exemption did
not move with it.
"@
        }

        $inPermitted = @(Find-Offences `
            -Text $permitted[0].Text `
            -Label $check.PermittedFile `
            -Patterns $check.Patterns)

        if ($inPermitted.Count -eq 0) {
            throw @"
$($check.Name) permits $($check.PermittedFile) and found nothing there to permit.
Either the implementation no longer reads the ambient clock, in which case this
carve-out covers nothing and should go, or it moved and this is now excusing a
file that never needed excusing.
"@
        }
    }

    # A scope states which tree the rule governs, which is not an exemption
    # mechanism either: it is part of stating the rule, as the permitted file is.
    # A scope that matched nothing would make the check assert over an empty set,
    # so it earns its place the same way.
    $subject = @($scanned | Where-Object {
        $null -eq $check.Scope -or
        $_.Relative.StartsWith($check.Scope, [StringComparison]::OrdinalIgnoreCase)
    })

    if ($subject.Count -eq 0) {
        throw @"
$($check.Name) is scoped to $($check.Scope) and the scan found no files there, so
it asserted over nothing. The scope is wrong, or the tree moved and it did not
move with it.
"@
    }

    $offences = @()

    foreach ($file in $subject) {
        if ($file.Relative -eq $check.PermittedFile) {
            continue
        }

        $offences += Find-Offences -Text $file.Text -Label $file.Relative -Patterns $check.Patterns
    }

    if ($offences.Count -ne 0) {
        throw @"
$($check.Name)

$($check.Explanation.Trim())

$($offences -join "`n")
"@
    }
}

Write-Host "Guards passed. $($files.Count) C# files scanned by $($checks.Count) checks: $(($checks | ForEach-Object { $_.Name }) -join ', ')."
