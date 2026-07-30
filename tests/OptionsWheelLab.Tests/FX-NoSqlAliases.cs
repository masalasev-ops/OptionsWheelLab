using System.Text.RegularExpressions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-NoSqlAliases: no SQL in <c>src/</c> aliases a table or a column.
/// </summary>
/// <remarks>
/// This discharges the alias obligation raised at 0.4 and widened at 0.7. Both SQL
/// detectors match names against a declared vocabulary and neither resolves an
/// alias, so <c>SELECT strike AS s FROM contracts ORDER BY s</c> and
/// <c>UPDATE config_rows AS c SET</c> both passed while being exactly what the
/// vocabulary forbids.
/// <para>
/// <b>The convention, not resolution.</b> Resolving an alias in
/// FX-NoDecimalOrderingInSql needs the detector to know which table a column
/// belongs to, because the vocabulary is unqualified column names. That is the
/// problem 1.1 declined when it kept <c>DecimalColumns</c> unqualified, and solving
/// it here would be solving it twice over. Forbidding the alias makes both
/// detectors sound without either of them learning any schema.
/// </para>
/// <para>
/// <b>What it costs, recorded rather than discovered later.</b> A self-join becomes
/// unexpressible in <c>src/</c>, and comparing two observations of one bar is a
/// plausible self-join now that a correction appends. If a phase needs one, this
/// convention is what gets revisited, and the obligation's other answer, real alias
/// resolution, is still available.
/// </para>
/// <para>
/// The two known-miss tests are deleted with this, per the obligation: they pinned
/// the gap so it was visible in the suite, and the gap is closed.
/// </para>
/// </remarks>
public sealed class FX_NoSqlAliases
{
    [Fact]
    public void No_sql_in_src_aliases_a_table_or_a_column()
    {
        var offences = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql
                .SqlIn(File.ReadAllText(file))
                .SelectMany(SqlAliases.Offences)
                .Select(offence => $"{Path.GetFileName(file)}: {offence}"))
            .OrderBy(offence => offence, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offences.Count == 0,
            "These statements alias a table or a column, which neither SQL detector can "
            + "resolve, so the name they check is not the name the query uses: "
            + $"{string.Join("; ", offences)}. Write the column or table out in full.");
    }

    /// <summary>
    /// A scan that found no SQL would pass the assertion above without testing
    /// anything.
    /// </summary>
    [Fact]
    public void The_scan_finds_sql_to_inspect()
    {
        var sql = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql.SqlIn(File.ReadAllText(file)))
            .ToList();

        Assert.NotEmpty(sql);
        Assert.True(sql.Sum(DecimalOrderingInSql.StatementCount) > 0);
    }

    [Fact]
    public void A_column_alias_is_reported()
    {
        const string Sql = "SELECT strike AS s FROM contracts ORDER BY s DESC;";

        var offence = Assert.Single(SqlAliases.Offences(Sql));

        Assert.Contains("strike", offence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_table_alias_written_with_as_is_reported()
    {
        const string Sql = "UPDATE config_rows AS c SET value = '1';";

        Assert.Single(SqlAliases.Offences(Sql));
    }

    [Fact]
    public void A_bare_table_alias_is_reported()
    {
        const string Sql = "SELECT c.strike FROM contracts c ORDER BY c.strike;";

        var offence = Assert.Single(SqlAliases.Offences(Sql));

        Assert.Contains("contracts", offence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_join_alias_is_reported()
    {
        const string Sql =
            "SELECT * FROM contracts JOIN contract_quotes q ON q.contract_id = contracts.contract_id;";

        Assert.Single(SqlAliases.Offences(Sql));
    }

    /// <summary>
    /// The real <c>INSERT</c>, which has the shape a bare table alias has.
    /// </summary>
    /// <remarks>
    /// <c>INSERT INTO config_rows (key, ...)</c> is a table name followed by a
    /// token, which is what the bare-alias rule looks for. It is the only
    /// <c>INSERT</c> in the codebase, so this uses the statement itself rather than
    /// a synthetic stand-in: a convention whose first run flags a legitimate
    /// statement gets narrowed rather than believed.
    /// </remarks>
    [Fact]
    public void The_column_list_of_an_insert_is_not_a_table_alias()
    {
        const string Sql =
            """
            INSERT INTO config_rows (key, version, value, set_at, note)
            SELECT $key,
                   COALESCE(MAX(version), 0) + 1,
                   $value,
                   $setAt,
                   $note
            FROM config_rows
            WHERE key = $key
            RETURNING version;
            """;

        Assert.Empty(SqlAliases.Offences(Sql));
    }

    /// <summary>
    /// The real trigger bodies, which have the same shape with no punctuation
    /// between the tokens.
    /// </summary>
    /// <remarks>
    /// <c>BEFORE UPDATE ON config_rows BEGIN</c> is a table name followed by a bare
    /// keyword, and migration 3 adds twelve more of them. This is the likelier false
    /// positive of the two, because nothing separates the tokens.
    /// </remarks>
    [Fact]
    public void A_trigger_body_is_not_a_table_alias()
    {
        const string Sql =
            """
            CREATE TRIGGER config_rows_no_update
            BEFORE UPDATE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only');
            END;

            CREATE TRIGGER config_rows_set_at_not_earlier
            BEFORE INSERT ON config_rows
            WHEN NEW.set_at < (SELECT MAX(set_at) FROM config_rows WHERE key = NEW.key)
            BEGIN
                SELECT RAISE(ABORT, 'set_at moves forward');
            END;
            """;

        Assert.Empty(SqlAliases.Offences(Sql));
    }

    /// <summary>
    /// A qualified column is not an alias, and neither is a table named in full.
    /// </summary>
    [Fact]
    public void Fully_written_sql_is_not_reported()
    {
        const string Sql =
            """
            SELECT contract_quotes.bid
            FROM contract_quotes
            WHERE contract_quotes.contract_id = $id
            ORDER BY contract_quotes.snapshot_date DESC
            LIMIT 1;
            """;

        Assert.Empty(SqlAliases.Offences(Sql));
    }

    private static IReadOnlyList<string> SourceFiles() =>
        RepoRoot.SourceFilesUnder(RepoRoot.SourcePath);
}

/// <summary>
/// Finds SQL that aliases a table or a column.
/// </summary>
/// <remarks>
/// A pure function over text, exercised on synthetic SQL and on the real statements
/// that have an alias's shape without being one.
/// <para>
/// <b>The keyword list is what makes the bare-table rule safe.</b> A table name
/// followed by a word is an alias only when that word is not SQL. Every token that
/// can legally follow a table reference is excluded by name, so the rule reports a
/// chosen identifier and nothing else.
/// </para>
/// </remarks>
internal static class SqlAliases
{
    // A column alias: an expression, AS, a name. Anchored on AS rather than on the
    // expression, because the expression can be anything.
    private static readonly Regex ColumnAlias = new(
        @"\b(?<source>[A-Za-z_][A-Za-z0-9_.]*)\s+AS\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // A table reference: FROM, JOIN, INTO or UPDATE, the table, then a token that
    // might be an alias.
    //
    // The trailing token is a LOOKAHEAD so the match does not consume it. Consuming
    // it swallowed the next clause keyword, so in
    // `FROM contracts JOIN contract_quotes q` the JOIN was eaten by the first match
    // and the real alias on the second table was never scanned. A detector that
    // misses the second table in every join would have been worse than no detector,
    // because the suite would have said the convention held.
    private static readonly Regex TableReference = new(
        @"\b(?<clause>FROM|JOIN|INTO|UPDATE)\s+(?<table>[A-Za-z_][A-Za-z0-9_]*)(?=\s+(?<next>[A-Za-z_][A-Za-z0-9_]*))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Every token that can legally follow a table reference without being an alias.
    /// </summary>
    /// <remarks>
    /// A catch-list inverted: an unknown token here is reported, which is the safe
    /// direction. A missing entry is a false positive that a build fixes by adding
    /// the keyword, where a missing alias form would be a silent pass.
    /// </remarks>
    private static readonly string[] NotAnAlias =
    [
        "WHERE", "SET", "VALUES", "SELECT", "ORDER", "GROUP", "HAVING", "LIMIT",
        "OFFSET", "ON", "USING", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "CROSS",
        "NATURAL", "UNION", "EXCEPT", "INTERSECT", "RETURNING", "BEGIN", "WHEN",
        "AS", "DEFAULT", "NOT", "AND", "OR", "END", "FOR", "EACH", "ROW",
    ];

    internal static IReadOnlyList<string> Offences(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var offences = new List<string>();

        foreach (Match match in ColumnAlias.Matches(sql))
        {
            // AS in a CREATE TABLE ... AS SELECT is not a column alias, and neither
            // is a cast. Neither form appears here, so the plain rule stands.
            offences.Add(
                $"column alias {match.Groups["source"].Value} AS {match.Groups["alias"].Value}");
        }

        foreach (Match match in TableReference.Matches(sql))
        {
            var table = match.Groups["table"].Value;
            var next = match.Groups["next"].Value;

            // `BEFORE UPDATE ON config_rows BEGIN` matches on UPDATE, and reads ON
            // as the table and the real table as its alias. When the table position
            // holds a keyword this is not a table reference at all. Migration 3 adds
            // twelve more trigger bodies of exactly this shape.
            if (NotAnAlias.Contains(table, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (NotAnAlias.Contains(next, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            offences.Add($"table alias {table} {next}");
        }

        return offences;
    }
}
