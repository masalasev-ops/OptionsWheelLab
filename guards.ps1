<#
.SYNOPSIS
    Source guards that are not unit tests.

.DESCRIPTION
    Checks that must hold over the source text rather than over behaviour, and
    that must fail even when the build does not. CI calls this script rather
    than restating the checks, so there is one definition of each.

    Today it holds one: no floating point in the tree [CLAUDE.md 2, D-W29].
    FX-NoAmbientClock at 0.5 and the append-only guards at 0.7 belong here too.

    WHAT THIS CANNOT CATCH. It scans for tokens, so it sees declared intent and
    not inferred types. `values.Average()` over a sequence of floating-point
    numbers carries none of the tokens below and passes, as does System.Text.Json
    binding a vendor number into an untyped tree. Anyone reading a green run as
    proof that no floating point reaches a monetary path is wrong. The mechanism
    that would see those is a Roslyn analyser, which is raised for 0.7, where
    three guards exist and one mechanism serving all of them can be compared
    concretely.

    A CATCH-LIST, NOT AN EXEMPTION LIST, and the difference is the whole point.
    An incomplete catch-list still catches what is on it; an incomplete exemption
    list is a hole. So this list may be added to freely and there is deliberately
    no way to opt a file out. The first legitimate floating-point value in this
    repository -- Phase 6 standard errors and rank correlations are the likely
    one -- should cost a recorded decision about where statistics end and money
    begins, rather than a line in a suppression file written in a hurry.

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

# Comments are stripped before scanning, so a doc comment may say "double" while
# explaining that a value is decimal and never double. PolicyBand.cs does.
# String literals are NOT stripped: no literal in this tree carries a token, and
# a stripper that handled every C# string form correctly would be a parser, whose
# failure mode is silently scanning the wrong bytes.
$forbidden = @(
    @{ Pattern = '\b(double|float)\b';                  Meaning = 'a floating-point type' }
    @{ Pattern = '\bSystem\.(Double|Single)\b';          Meaning = 'a floating-point type by its CLR name' }
    @{ Pattern = '\bNextDouble\b';                       Meaning = 'Random.NextDouble' }
    @{ Pattern = '\bConvert\.To(Double|Single)\b';       Meaning = 'a conversion to floating point' }
    @{ Pattern = '\bGet(Double|Float)\b';                Meaning = 'a floating-point read from a data reader' }
    @{ Pattern = '\bMath\.(Sqrt|Pow|Exp|Log|Log10|Log2)\b'; Meaning = 'a Math function returning a double' }
    @{ Pattern = '(?<![\w.])\d+(\.\d+)?[eE][-+]?\d+(?![\w])'; Meaning = 'an exponent literal, which is a double' }
    @{ Pattern = '(?<![\w.])\d+(\.\d+)?[dDfF](?![\w])';  Meaning = 'a floating-point literal suffix' }
)

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
    param([string]$Text, [string]$Label)

    $stripped = Remove-NonCode -Text $Text
    $found = @()

    foreach ($rule in $forbidden) {
        foreach ($match in [regex]::Matches($stripped, $rule.Pattern)) {
            $line = ($stripped.Substring(0, $match.Index) -split "`n").Count
            $found += "{0}:{1} has '{2}', {3}" -f $Label, $line, $match.Value, $rule.Meaning
        }
    }

    return $found
}

# Self-test before scanning anything real. A scan that matched nothing would
# report success while testing nothing, and this corpus has already had one edit
# match nothing, no-op, and be recorded as done. Two legs: the detector must fire
# on a known violation, and it must not fire on the comment that explains the
# rule.
$mustFire = @'
public sealed class Probe
{
    private double _weight;
    private readonly float _ratio = 1e-8f;
    public double Mean(IEnumerable<double> values) => Math.Sqrt(Convert.ToDouble(values));
}
'@

$mustNotFire = @'
/// <summary>Delta is <see cref="decimal"/>, never <see cref="double"/>.</summary>
/* A block comment mentioning float and double. */
public sealed record PolicyBand(string Name, decimal DeltaMin, decimal DeltaMax);
// A line comment mentioning Math.Sqrt and Random.NextDouble.
'@

# Stripping literals introduces a failure mode of its own: a stripper that
# desyncs on a raw string swallows the code after it and still reports success,
# having scanned the wrong bytes. This sample carries the shapes that would cause
# that -- a raw string holding SQL with single quotes and -- comments, then a
# verbatim string, then a violation AFTER both. If the violation is not found,
# the stripper has eaten live code.
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

if (@(Find-Offences -Text $mustFire -Label 'self-test').Count -eq 0) {
    throw 'The guard did not fire on a known violation, so it is not detecting anything.'
}

$falsePositives = @(Find-Offences -Text $mustNotFire -Label 'self-test')

if ($falsePositives.Count -ne 0) {
    throw "The guard fired on comments, which cannot be a monetary path: $($falsePositives -join '; ')"
}

$afterStrings = @(Find-Offences -Text $mustFireAfterAwkwardStrings -Label 'self-test')

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
# under pwsh on the runner.
$prefix = $PSScriptRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar

$offences = @()

foreach ($file in $files) {
    $relative = $file.FullName
    if ($relative.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        $relative = $relative.Substring($prefix.Length)
    }

    $offences += Find-Offences -Text (Get-Content -Raw -Path $file.FullName) -Label $relative
}

if ($offences.Count -ne 0) {
    throw @"
Floating point is not permitted in this repository [CLAUDE.md 2, D-W29]. Money is
decimal in TEXT, and a decimal path that admits a double loses cents silently
rather than failing.

$($offences -join "`n")

There is no exemption mechanism, deliberately. If this value genuinely is not
money -- a standard error or a rank correlation, say -- that is a decision about
where statistics end and money begins, and it belongs in DECISIONS.md before it
belongs in the code.
"@
}

Write-Host "Guards passed. $($files.Count) C# files scanned for floating point."
