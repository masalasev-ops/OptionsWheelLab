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
/// <b>The convention, not resolution.</b> Resolution is the obligation's other
/// answer and remains available; the convention is cheaper and needs no detector to
/// learn any schema. It is worth being accurate about how much cheaper, because 1.5
/// weighed the two again and kept the convention: column-alias resolution would be
/// enough for FX-NoDecimalOrderingInSql, since mapping <c>s</c> back to
/// <c>strike</c> answers an unqualified vocabulary without knowing which table
/// anything belongs to. It is a QUALIFIED vocabulary that would need table
/// resolution, and 1.1 declined that separately.
/// </para>
/// <para>
/// <b>What it costs, and the dated case is resolved.</b> A self-join must alias at
/// least one side, so the convention forbids one. 1.5's definition of done requires
/// a historical join across a split to resolve both contracts, and 1.1 added the
/// index on <c>predecessor_contract_id</c> that serves it. So the convention
/// adopted here and the join it appears to forbid arrived in the same migration,
/// four checkpoints apart, and at 1.5 the join shipped without a self-join:
/// <c>ContractLineage</c> walks the link as a recursive CTE.
/// </para>
/// <para>
/// <b>Measured, and the collision dissolved as predicted.</b> The walk is
/// expressible without an alias, as a recursive CTE: a CTE names the working set
/// rather than renaming the table, so nothing has two names. It was run against
/// migration 3's schema over a three-generation chain and returned all three, and
/// 1.5 made exactly that shape production. The convention stands; resolution was
/// never needed.
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

    /// <summary>
    /// The primary widened case: an aggregate given a second name, which is the
    /// form a naive chain read would write this checkpoint.
    /// </summary>
    /// <remarks>
    /// The first pattern was blind to every parenthesised expression, because its
    /// source arm was an identifier class and the character before <c>AS</c> here
    /// is <c>)</c>. The comment claimed the pattern was anchored on AS so the
    /// expression could be anything; the pattern did not do what the comment said.
    /// </remarks>
    [Fact]
    public void An_aggregate_given_a_second_name_is_reported()
    {
        const string Sql =
            "SELECT contract_id, MAX(observed_at) AS latest FROM contract_quotes GROUP BY contract_id;";

        var offence = Assert.Single(SqlAliases.Offences(Sql));

        Assert.Contains("MAX(observed_at)", offence, StringComparison.Ordinal);
        Assert.Contains("latest", offence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_count_star_given_a_second_name_is_reported()
    {
        const string Sql = "SELECT COUNT(*) AS n FROM contracts;";

        Assert.Single(SqlAliases.Offences(Sql));
    }

    [Fact]
    public void A_window_function_given_a_second_name_is_reported()
    {
        const string Sql =
            "SELECT ROW_NUMBER() OVER (ORDER BY observed_at) AS rn FROM underlying_bars;";

        Assert.Single(SqlAliases.Offences(Sql));
    }

    /// <summary>
    /// A CTE with declared column names is not an alias, and it is the chain
    /// read's own shape: the aggregate lands in a declared column, so nothing
    /// acquires a second name and no alias is needed at all.
    /// </summary>
    [Fact]
    public void A_cte_with_declared_column_names_is_not_reported()
    {
        const string Sql =
            """
            WITH latest(contract_id, observed_at) AS (
                SELECT contract_id, MAX(observed_at)
                FROM contract_quotes
                GROUP BY contract_id
            )
            SELECT contract_id FROM latest;
            """;

        Assert.Empty(SqlAliases.Offences(Sql));
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

    /// <summary>
    /// The predecessor walk 1.5 needed, written as a recursive CTE, is not an alias.
    /// </summary>
    /// <remarks>
    /// 1.5's definition of done requires a historical join across a split to resolve
    /// both contracts, and 1.1 added the index that serves it. A self-join must
    /// alias at least one side, so the convention appeared to forbid the one query a
    /// definition of done four checkpoints ahead required.
    /// <para>
    /// <b>It did not.</b> A recursive CTE names the working set instead of aliasing
    /// the table, and a CTE name is a declaration rather than a rename: nothing has
    /// two names. 1.5 found exactly that here and shipped the walk as
    /// <c>ContractLineage</c>'s CTE with declared column names; this pin now guards
    /// the production shape rather than predicting it.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_predecessor_walk_as_a_recursive_cte_is_not_an_alias()
    {
        const string Sql =
            """
            WITH RECURSIVE chain AS (
                SELECT contract_id, predecessor_contract_id
                FROM contracts
                WHERE contract_id = $id
                UNION ALL
                SELECT contracts.contract_id, contracts.predecessor_contract_id
                FROM contracts
                JOIN chain ON contracts.contract_id = chain.predecessor_contract_id
            )
            SELECT contract_id FROM chain;
            """;

        Assert.Empty(SqlAliases.Offences(Sql));
    }

    /// <summary>
    /// A self-join written the ordinary way is still reported, which is the half of
    /// the collision that is real.
    /// </summary>
    [Fact]
    public void A_self_join_written_with_aliases_is_reported()
    {
        const string Sql =
            "SELECT a.contract_id FROM contracts a JOIN contracts b ON a.predecessor_contract_id = b.contract_id;";

        Assert.Equal(2, SqlAliases.Offences(Sql).Count);
    }

    /// <summary>
    /// English prose in a comment is not a table alias, which is what found the
    /// scan was reading comments at all.
    /// </summary>
    /// <remarks>
    /// Both sentences are real, from migrations 7 and 8. "cannot tell a market
    /// holiday from a name that did not trade" reads as <c>FROM a name</c> and
    /// "rebuilt from this table" as <c>FROM this table</c>, so the detector
    /// reported two offences in documentation. The fix is in
    /// <see cref="DecimalOrderingInSql.WithoutCommentsOrLiterals"/>, shared by both
    /// detectors, rather than a keyword added here: <c>a</c> and <c>this</c> are
    /// not SQL keywords and adding them would blind the rule to a real alias
    /// named <c>a</c>.
    /// </remarks>
    [Fact]
    public void Prose_in_a_comment_is_not_a_table_alias()
    {
        const string Sql =
            """
            -- underlying_bars.session_date cannot tell a market holiday from a
            -- name that did not trade, and a projection rebuilt from this table
            -- has to reproduce what was known when.
            SELECT session_date FROM market_sessions;
            """;

        Assert.Empty(SqlAliases.Offences(DecimalOrderingInSql.WithoutCommentsOrLiterals(Sql)));
    }

    /// <summary>
    /// The statement beside the comment is still scanned, so the fix removed
    /// noise rather than coverage.
    /// </summary>
    [Fact]
    public void An_alias_beside_a_comment_is_still_reported()
    {
        const string Sql =
            """
            -- A note mentioning contracts and nothing else.
            SELECT c.strike FROM contracts c ORDER BY c.strike;
            """;

        Assert.Single(SqlAliases.Offences(DecimalOrderingInSql.WithoutCommentsOrLiterals(Sql)));
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
    //
    // Three source arms. A call with a simple argument list, so the offence can
    // name MAX(observed_at) rather than a bare parenthesis; an identifier run; and
    // a lone closing parenthesis, the fallback that makes any parenthesised
    // expression a source. The first pattern held only the identifier arm, so
    // MAX(observed_at) AS latest, COUNT(*) AS n and ROW_NUMBER() OVER (...) AS rn
    // all passed: the character before AS is `)`, which an identifier class cannot
    // match. Found at 1.2, whose chain read is exactly the aggregate case.
    //
    // What keeps a CTE clean is the ALIAS group, not the source: in
    // `WITH latest(a, b) AS (` and `WITH RECURSIVE chain AS (` the token after AS
    // is `(`, which no identifier can match, so neither arm binds. That is the
    // right distinguisher rather than an exemption, because a CTE names its result
    // instead of renaming anything.
    private static readonly Regex ColumnAlias = new(
        @"(?<source>[A-Za-z_][A-Za-z0-9_.]*\s*\([^()]*\)|\b[A-Za-z_][A-Za-z0-9_.]*|\))\s+AS\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)",
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
            // AS in a CREATE TABLE ... AS SELECT and AS in a cast are not column
            // aliases, and BOTH WOULD BE REPORTED: the cast by the identifier arm
            // (CAST(x AS TEXT) binds source x, alias TEXT), the CREATE by the
            // parenthesis arm after its column list. Neither appears in the tree,
            // so the rule stands on absence rather than on being narrow. If either
            // ever lands, this fires loudly, which is the recoverable direction
            // and the reason to leave it: an unreachable false positive named here
            // costs nothing, and the same one discovered by a failing build under
            // a comment claiming it cannot happen costs an hour.
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
