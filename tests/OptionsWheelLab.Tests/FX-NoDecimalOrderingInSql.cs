using System.Text.RegularExpressions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-NoDecimalOrderingInSql: no SQL orders, ranges over, or aggregates a
/// decimal column.
/// </summary>
/// <remarks>
/// The canonical decimal form is not order-preserving: the integer part is
/// variable width, so <c>"9.00000000"</c> sorts above <c>"10.00000000"</c> under
/// SQLite's BINARY collation, and negatives invert again. Comparison and
/// arithmetic therefore happen in code after parsing [D-W29].
/// <para>
/// <b>A test rather than a grep.</b> It has to know which columns are decimal
/// and read SQL structure rather than text, and its detector wants exercising on
/// synthetic input, which a PowerShell regex could not offer. The
/// <c>double</c>/<c>float</c> guard is a script instead, because that one must
/// fail even when the build is broken.
/// </para>
/// <para>
/// Phase 5 ranks candidates and Phase 6 aggregates outcomes. Neither may do so
/// in SQL over a decimal column, and this is here now so that constraint is
/// inherited rather than rediscovered.
/// </para>
/// </remarks>
public sealed class FX_NoDecimalOrderingInSql
{
    [Fact]
    public void No_sql_in_the_codebase_orders_or_aggregates_a_decimal_column()
    {
        var offences = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql
                .SqlIn(File.ReadAllText(file))
                .SelectMany(sql => DecimalOrderingInSql.Offences(sql, DecimalColumns.All))
                .Select(offence => $"{Path.GetFileName(file)}: {offence}"))
            .OrderBy(offence => offence, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offences.Count == 0,
            "These statements order, range over, or aggregate a column holding the canonical "
            + $"decimal form, which is not order-preserving: {string.Join("; ", offences)}. "
            + "Compare and aggregate in code after parsing [D-W29].");
    }

    /// <summary>
    /// A scan that found no SQL would pass the assertion above while testing
    /// nothing, and that failure has already happened once in this corpus.
    /// </summary>
    [Fact]
    public void The_scan_finds_sql_to_inspect_and_columns_to_check()
    {
        var sql = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql.SqlIn(File.ReadAllText(file)))
            .ToList();

        var statements = sql.Sum(DecimalOrderingInSql.StatementCount);

        Assert.True(
            statements > 0,
            $"No SQL literal was found under {RepoRoot.SourcePath} to inspect, so the scan above "
            + "asserted over nothing.");

        Assert.NotEmpty(DecimalColumns.All);

        // The extraction must find the SQL that is actually there, not merely
        // find something. A raw string it failed to open would leave the count
        // short while every assertion still passed.
        Assert.Contains(sql, statement => statement.Contains("config_rows", StringComparison.Ordinal));
        Assert.Contains(sql, statement => statement.Contains("schema_migrations", StringComparison.Ordinal));
    }

    /// <summary>
    /// The vocabulary cannot name a column the schema does not have.
    /// </summary>
    /// <remarks>
    /// The direction that can be enforced today. The reverse, that every decimal
    /// column in the schema appears in the vocabulary, cannot be standing while
    /// §4 is mostly specification for phases not yet built, so it is a definition
    /// of done on the checkpoint that adds each table.
    /// </remarks>
    [Fact]
    public void Every_declared_decimal_column_appears_in_the_schema_document()
    {
        var schema = File.ReadAllText(RepoRoot.SchemaDocumentPath);

        var absent = DecimalColumns.All
            .Where(column => !Regex.IsMatch(schema, $@"\b{Regex.Escape(column)}\b"))
            .OrderBy(column => column, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            absent.Count == 0,
            $"These columns are declared decimal in {nameof(DecimalColumns)} but appear nowhere in "
            + $"{RepoRoot.SchemaDocumentPath}: {string.Join(", ", absent)}.");
    }

    /// <summary>
    /// The two statements already in the tree that this must NOT fire on, which
    /// is what makes the check worth more than its synthetic cases.
    /// </summary>
    /// <remarks>
    /// <c>version</c> is an integer. <c>set_at</c> is a fixed-width UTC timestamp
    /// whose string order IS its time order by construction, which is exactly the
    /// property the decimal form lacks.
    /// </remarks>
    [Fact]
    public void Ordering_a_version_or_aggregating_a_timestamp_is_not_an_offence()
    {
        const string Sql = """
            SELECT value FROM config_rows
            WHERE key = $key AND set_at <= $upperBound
            ORDER BY version DESC LIMIT 1;
            SELECT MAX(set_at) FROM config_rows WHERE key = $key;
            SELECT COALESCE(MAX(version), 0) + 1 FROM config_rows;
            """;

        Assert.Empty(DecimalOrderingInSql.Offences(Sql, DecimalColumns.All));
    }

    [Fact]
    public void Ordering_by_a_decimal_column_is_reported()
    {
        const string Sql = "SELECT contract_id FROM contract_quotes ORDER BY delta DESC;";

        var offence = Assert.Single(DecimalOrderingInSql.Offences(Sql, Vocabulary("delta")));

        Assert.Contains("ORDER BY", offence, StringComparison.Ordinal);
        Assert.Contains("delta", offence, StringComparison.Ordinal);
    }

    [Fact]
    public void Ordering_by_a_decimal_column_among_several_is_reported()
    {
        const string Sql = "SELECT * FROM contract_quotes ORDER BY snapshot_date, delta DESC;";

        Assert.Single(DecimalOrderingInSql.Offences(Sql, Vocabulary("delta")));
    }

    [Fact]
    public void Aggregating_a_decimal_column_is_reported()
    {
        const string Sql = "SELECT SUM(amount), AVG(credit), MIN(strike) FROM ledger_entries;";

        var offences = DecimalOrderingInSql.Offences(Sql, Vocabulary("amount", "credit", "strike"));

        Assert.Equal(3, offences.Count);
    }

    [Fact]
    public void Ranging_over_a_decimal_column_is_reported()
    {
        const string Sql = "SELECT * FROM contract_quotes WHERE delta > $ceiling;";

        var offence = Assert.Single(DecimalOrderingInSql.Offences(Sql, Vocabulary("delta")));

        Assert.Contains("range", offence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_between_over_a_decimal_column_is_reported()
    {
        const string Sql = "SELECT * FROM contract_quotes WHERE delta BETWEEN $low AND $high;";

        Assert.Single(DecimalOrderingInSql.Offences(Sql, Vocabulary("delta")));
    }

    /// <summary>
    /// Equality is not an offence. It reads the canonical form, which is exactly
    /// what the canonical form is for.
    /// </summary>
    [Fact]
    public void An_equality_on_a_decimal_column_is_not_an_offence()
    {
        const string Sql = "SELECT contract_id FROM contracts WHERE strike = $strike;";

        Assert.Empty(DecimalOrderingInSql.Offences(Sql, Vocabulary("strike")));
    }

    /// <summary>
    /// A column not in the vocabulary is not this check's business, so a scan
    /// that silently matched every identifier would be caught here.
    /// </summary>
    [Fact]
    public void A_column_outside_the_vocabulary_is_not_reported()
    {
        const string Sql = "SELECT * FROM underlying_bars ORDER BY session_date DESC;";

        Assert.Empty(DecimalOrderingInSql.Offences(Sql, Vocabulary("close")));
    }

    private static IReadOnlyList<string> SourceFiles() =>
        RepoRoot.SourceFilesUnder(RepoRoot.SourcePath);

    private static IReadOnlySet<string> Vocabulary(params string[] columns) =>
        columns.ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Finds SQL that orders, ranges over, or aggregates a named column.
/// </summary>
/// <remarks>
/// A pure function over text, so it is exercised on synthetic SQL as well as run
/// against the tree. The tree currently yields nothing, which is the point: the
/// constraint lands before the columns it guards, and the synthetic cases are
/// what prove it would fire.
/// </remarks>
internal static class DecimalOrderingInSql
{
    private static readonly Regex OrderByClause = new(
        @"\bORDER\s+BY\b(?<columns>[^;)]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Aggregate = new(
        @"\b(?<function>MIN|MAX|SUM|AVG|TOTAL)\s*\(\s*(?<column>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // A range comparison, deliberately excluding = and !=. Equality reads the
    // canonical form and is what the canonical form is for.
    private static readonly Regex RangeComparison = new(
        @"\b(?<column>[A-Za-z_][A-Za-z0-9_]*)\s*(?<operator><=|>=|<>|<|>)\s*[$:@?A-Za-z0-9_'""]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Between = new(
        @"\b(?<column>[A-Za-z_][A-Za-z0-9_]*)\s+BETWEEN\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Statement = new(
        @"\b(SELECT|INSERT\s+INTO|UPDATE|DELETE\s+FROM|CREATE\s+TABLE)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Identifier = new(
        @"[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Raw string literals, which is where every SQL statement in this repository
    // lives. Matched first, because their contents can hold anything a
    // single-line literal pattern would misread.
    private static readonly Regex RawStringLiteral = new(
        @"""{3,}(?<body>.*?)""{3,}",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex SingleLineStringLiteral = new(
        @"""(?<body>(?:[^""\\\r\n]|\\.)*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] OrderKeywords = ["ASC", "DESC", "COLLATE", "NULLS", "FIRST", "LAST"];

    internal static IReadOnlyList<string> Offences(string sql, IReadOnlySet<string> decimalColumns)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(decimalColumns);

        var offences = new List<string>();

        foreach (Match clause in OrderByClause.Matches(sql))
        {
            foreach (Match identifier in Identifier.Matches(clause.Groups["columns"].Value))
            {
                if (OrderKeywords.Contains(identifier.Value, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (decimalColumns.Contains(identifier.Value))
                {
                    offences.Add($"ORDER BY {identifier.Value}");
                }
            }
        }

        foreach (Match match in Aggregate.Matches(sql))
        {
            var column = match.Groups["column"].Value;

            if (decimalColumns.Contains(column))
            {
                offences.Add($"aggregate {match.Groups["function"].Value.ToUpperInvariant()}({column})");
            }
        }

        foreach (Match match in RangeComparison.Matches(sql))
        {
            var column = match.Groups["column"].Value;

            if (decimalColumns.Contains(column))
            {
                offences.Add($"range comparison {column} {match.Groups["operator"].Value}");
            }
        }

        foreach (Match match in Between.Matches(sql))
        {
            var column = match.Groups["column"].Value;

            if (decimalColumns.Contains(column))
            {
                offences.Add($"range comparison {column} BETWEEN");
            }
        }

        return offences;
    }

    /// <summary>
    /// The SQL a C# source file contains.
    /// </summary>
    /// <remarks>
    /// <b>String literals only, and only those that look like SQL.</b> Scanning
    /// whole source text does not work: a C# parameter named <c>value</c>
    /// compared with <c>&gt;=</c> reads exactly like a range comparison over the
    /// <c>value</c> column, and <c>StoreDecimal</c> has one. Restricting to
    /// literals that contain a statement keyword removes every such collision,
    /// and costs nothing, because SQL cannot reach the database except through a
    /// literal.
    /// <para>
    /// Raw strings are matched first and removed. Every SQL statement in this
    /// repository is in one, and their bodies contain quotes and <c>--</c>
    /// comments that a single-line literal pattern would misread.
    /// </para>
    /// </remarks>
    internal static IReadOnlyList<string> SqlIn(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var literals = new List<string>();

        var withoutRawStrings = RawStringLiteral.Replace(
            source,
            match =>
            {
                literals.Add(match.Groups["body"].Value);
                return string.Empty;
            });

        literals.AddRange(SingleLineStringLiteral
            .Matches(withoutRawStrings)
            .Select(match => match.Groups["body"].Value));

        return [.. literals.Where(literal => Statement.IsMatch(literal))];
    }

    /// <summary>
    /// SQL statements the text contains, for the vacuity guard.
    /// </summary>
    internal static int StatementCount(string sql) => Statement.Matches(sql).Count;
}
