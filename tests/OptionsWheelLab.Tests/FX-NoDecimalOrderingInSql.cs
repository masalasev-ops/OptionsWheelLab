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

    /// <summary>
    /// <c>last</c> is a decimal column, not the <c>NULLS LAST</c> keyword.
    /// </summary>
    /// <remarks>
    /// The order-keyword filter ran before the vocabulary was consulted and skipped
    /// <c>LAST</c> unconditionally, so an ordering over <c>contract_quotes.last</c>
    /// was dropped: a real ordering over a canonical decimal, unreported. The defect
    /// did not exist until 1.1 made <c>last</c> a decimal column, and it was found
    /// by measuring the extended vocabulary against the detector rather than by
    /// running it.
    /// </remarks>
    [Fact]
    public void Ordering_by_the_last_column_is_reported()
    {
        const string Sql = "SELECT contract_id FROM contract_quotes ORDER BY last DESC;";

        var offence = Assert.Single(DecimalOrderingInSql.Offences(Sql, Vocabulary("last")));

        Assert.Contains("ORDER BY", offence, StringComparison.Ordinal);
        Assert.Contains("last", offence, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>NULLS LAST</c> is still an order keyword and is not read as the column.
    /// </summary>
    /// <remarks>
    /// The pair is what SQLite gives the keyword meaning to, so it is removed as a
    /// pair. Without this the fix would trade a false negative for a false positive.
    /// </remarks>
    [Fact]
    public void Nulls_last_is_not_read_as_the_last_column()
    {
        const string Sql = "SELECT contract_id FROM contract_quotes ORDER BY snapshot_date NULLS LAST;";

        Assert.Empty(DecimalOrderingInSql.Offences(Sql, Vocabulary("last")));
    }

    /// <summary>
    /// Both together, which is the case that would have hidden the defect.
    /// </summary>
    [Fact]
    public void The_last_column_is_reported_even_beside_a_nulls_last_clause()
    {
        const string Sql = "SELECT contract_id FROM contract_quotes ORDER BY last DESC NULLS LAST;";

        Assert.Single(DecimalOrderingInSql.Offences(Sql, Vocabulary("last")));
    }

    /// <summary>
    /// A comment describing an ordering is not an ordering.
    /// </summary>
    /// <remarks>
    /// The migrations carry their reasoning in <c>--</c> comments, and a comment
    /// explaining why something is NOT ordered would have been reported as
    /// ordering it. Nothing in the tree had collided before 3.3, so this was
    /// invisible rather than absent.
    /// </remarks>
    [Fact]
    public void A_comment_naming_a_decimal_column_is_not_an_offence()
    {
        const string Sql =
            """
            -- Never ORDER BY strike: the canonical form is not order-preserving.
            SELECT contract_id FROM contracts WHERE strike = $strike;
            """;

        Assert.Empty(
            DecimalOrderingInSql.Offences(
                DecimalOrderingInSql.WithoutComments(Sql), Vocabulary("strike")));
    }

    /// <summary>
    /// A dash pair inside a quoted string is text, and the statement around it
    /// survives.
    /// </summary>
    /// <remarks>
    /// The case a regex gets wrong. Deleting from <c>--</c> to end of line would
    /// truncate the string and take the rest of the statement with it, so a real
    /// ordering after the message would go unreported: the false-negative
    /// direction, which is the one this codebase treats as unrecoverable.
    /// </remarks>
    [Fact]
    public void A_dash_pair_inside_a_string_is_not_a_comment()
    {
        const string Sql =
            """
            CREATE TRIGGER t BEFORE UPDATE ON contracts
            BEGIN
                SELECT RAISE(ABORT, 'append-only -- never rewritten');
            END;
            SELECT contract_id FROM contracts ORDER BY strike;
            """;

        var stripped = DecimalOrderingInSql.WithoutComments(Sql);

        Assert.Contains("never rewritten", stripped, StringComparison.Ordinal);
        Assert.Single(DecimalOrderingInSql.Offences(stripped, Vocabulary("strike")));
    }

    /// <summary>An escaped quote does not end the string it is inside.</summary>
    [Fact]
    public void A_doubled_quote_does_not_close_the_string()
    {
        const string Sql = "SELECT RAISE(ABORT, 'it''s -- fine') FROM contracts;";

        Assert.Contains("-- fine", DecimalOrderingInSql.WithoutComments(Sql), StringComparison.Ordinal);
    }

    /// <summary>
    /// The stripping is live on the tree, not only on synthetic input.
    /// </summary>
    /// <remarks>
    /// Migration 6 onward carry comments holding SQL keywords, so a scan that
    /// stopped stripping would show it here rather than in whichever detector
    /// happened to collide next.
    /// </remarks>
    [Fact]
    public void The_scanned_sql_carries_no_comments()
    {
        var withComments = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql.SqlIn(File.ReadAllText(file)))
            .Where(sql => sql.Contains("--", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(withComments);
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
/// <para>
/// <b>Known limit: an alias defeats it, and this is the direction that fails
/// quietly.</b> Column names are matched against the declared vocabulary and
/// nothing resolves aliases, so
/// <c>SELECT strike AS s FROM contracts ORDER BY s</c> passes: the ordering is
/// over a decimal and the token being ordered is not in the list. The
/// over-reach note on <see cref="DecimalColumns"/> defends the false-POSITIVE
/// direction, where a flagged integer key is recoverable. This is the
/// false-negative one, where a real ordering goes unreported.
/// </para>
/// <para>
/// Not live at 0.4: one table, one column, no aliases and no joins. It becomes
/// live at Phase 1, where queries over bars and quotes alias and join as a
/// matter of course and the columns being ordered are the ones that matter.
/// Phase 1 has to choose between resolving aliases, which is correct, and a
/// convention that a decimal column is never aliased, which is cheaper and
/// checkable. That choice is not 0.4's to make.
/// </para>
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

    private static readonly string[] OrderKeywords = ["ASC", "DESC", "COLLATE", "NULLS"];

    // FIRST and LAST are order keywords only after NULLS, and `last` is a decimal
    // column from 1.1. Skipping them unconditionally, as this list did, meant
    // `ORDER BY last` was filtered out before the vocabulary was consulted: a real
    // ordering over a canonical decimal, silently unreported. Matched as a pair so
    // the keyword is excluded in the one context SQLite gives it and nowhere else.
    private static readonly Regex NullsOrdering = new(
        @"\bNULLS\s+(FIRST|LAST)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static IReadOnlyList<string> Offences(string sql, IReadOnlySet<string> decimalColumns)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(decimalColumns);

        var offences = new List<string>();

        foreach (Match clause in OrderByClause.Matches(sql))
        {
            // NULLS FIRST and NULLS LAST are removed as a pair before the
            // identifiers are read, so `last` is a column everywhere else.
            var columns = NullsOrdering.Replace(clause.Groups["columns"].Value, " ");

            foreach (Match identifier in Identifier.Matches(columns))
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
    /// <para>
    /// <b>Comments are stripped, because both detectors were reading them as
    /// SQL.</b> The migrations carry their reasoning in <c>--</c> comments, and
    /// at 3.3 two sentences of ordinary English matched the table-alias pattern:
    /// "cannot tell a market holiday from a name that did not trade" reads as
    /// <c>FROM a name</c>, and "rebuilt from this table" as <c>FROM this
    /// table</c>. Nothing had collided before, so the flaw was invisible rather
    /// than absent. Leaving it would put a standing pressure on every future
    /// migration comment to avoid a regex, which is the wrong thing to optimise a
    /// comment for in a codebase whose comments carry its arguments.
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

        return
        [
            .. literals
                .Select(WithoutComments)
                .Where(literal => Statement.IsMatch(literal)),
        ];
    }

    /// <summary>
    /// The SQL with its <c>--</c> comments removed.
    /// </summary>
    /// <remarks>
    /// <b>A scanner rather than a regex, because of the one case a regex gets
    /// wrong.</b> A <c>--</c> inside a single-quoted string is text, not a
    /// comment, and this store's triggers put prose inside
    /// <c>RAISE(ABORT, '...')</c> where a dash pair is entirely plausible. A
    /// pattern deleting from <c>--</c> to end of line would truncate such a
    /// statement mid-string and could hide the rest of it, which is the
    /// false-negative direction.
    /// <para>
    /// The line itself is kept, replaced by nothing rather than removed, so
    /// nothing on either side of a stripped comment joins up into a token pair
    /// that was never written.
    /// </para>
    /// </remarks>
    internal static string WithoutComments(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var kept = new System.Text.StringBuilder(sql.Length);
        var inString = false;

        for (var index = 0; index < sql.Length; index++)
        {
            var character = sql[index];

            if (inString)
            {
                kept.Append(character);

                if (character == '\'')
                {
                    // A doubled quote is an escaped quote and does not close the
                    // string, so consume its partner here rather than letting the
                    // next iteration read it as an opener.
                    if (index + 1 < sql.Length && sql[index + 1] == '\'')
                    {
                        kept.Append(sql[++index]);
                    }
                    else
                    {
                        inString = false;
                    }
                }

                continue;
            }

            if (character == '\'')
            {
                inString = true;
                kept.Append(character);
                continue;
            }

            if (character == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                while (index < sql.Length && sql[index] is not ('\n' or '\r'))
                {
                    index++;
                }

                if (index < sql.Length)
                {
                    kept.Append(sql[index]);
                }

                continue;
            }

            kept.Append(character);
        }

        return kept.ToString();
    }

    /// <summary>
    /// SQL statements the text contains, for the vacuity guard.
    /// </summary>
    internal static int StatementCount(string sql) => Statement.Matches(sql).Count;
}
