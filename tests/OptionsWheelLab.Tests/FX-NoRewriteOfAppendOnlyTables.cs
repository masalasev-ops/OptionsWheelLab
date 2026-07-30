using System.Text.RegularExpressions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-NoRewriteOfAppendOnlyTables: no statement in <c>src/</c> deletes from or
/// updates a table the append-only vocabulary covers.
/// </summary>
/// <remarks>
/// <b>A fixture rather than a guard, and the reason is structural.</b>
/// <c>guards.ps1</c> strips raw string literals before scanning, and every SQL
/// statement in this repository lives in one, so a pattern added to the script
/// would match nothing in the tree by construction. This also needs a vocabulary
/// and has to read statement shape rather than text, which is 0.4's criterion
/// for the same split.
/// <para>
/// <b>Three mechanisms exclude the statements already in the tree, and one would
/// not suffice.</b> Statement form excludes the trigger DDL, which is a
/// <c>BEFORE UPDATE ON</c> rather than an <c>UPDATE ... SET</c>. The vocabulary
/// excludes a statement against a table the rule does not cover. Scan scope
/// excludes the tests that prove the triggers work, because the rule governs what
/// the lab does to its own store and a test proving the guard works is not the
/// lab doing it.
/// </para>
/// <para>
/// None of the three is an exemption list. An exemption names a file to silence
/// a failure; these name what the check is about, each fixed once with a reason
/// rather than extended when something failed.
/// </para>
/// </remarks>
public sealed class FX_NoRewriteOfAppendOnlyTables
{
    [Fact]
    public void No_sql_in_the_codebase_rewrites_an_append_only_table()
    {
        var offences = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql
                .SqlIn(File.ReadAllText(file))
                .SelectMany(sql => AppendOnlyRewritesInSql.Offences(sql, AppendOnlyTables.All))
                .Select(offence => $"{Path.GetFileName(file)}: {offence}"))
            .OrderBy(offence => offence, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offences.Count == 0,
            "These statements rewrite a table that is never rewritten: "
            + $"{string.Join("; ", offences)}. A correction is a new row carrying its own stamp, "
            + "not an edit of the old one [D-W3, D-W8, D-W26, D-W32].");
    }

    /// <summary>
    /// A scan that found no SQL, or a vocabulary with no names, would pass the
    /// assertion above while testing nothing.
    /// </summary>
    [Fact]
    public void The_scan_finds_sql_to_inspect_and_tables_to_check()
    {
        var sql = SourceFiles()
            .SelectMany(file => DecimalOrderingInSql.SqlIn(File.ReadAllText(file)))
            .ToList();

        Assert.True(
            sql.Sum(DecimalOrderingInSql.StatementCount) > 0,
            $"No SQL literal was found under {RepoRoot.SourcePath} to inspect.");

        Assert.NotEmpty(AppendOnlyTables.All);

        // The extraction must reach the statements that carry the banned text,
        // or the check above passes by never seeing them.
        Assert.Contains(sql, statement => statement.Contains("BEFORE UPDATE ON", StringComparison.Ordinal));
        Assert.Contains(sql, statement => statement.Contains("BEFORE DELETE ON", StringComparison.Ordinal));
    }

    /// <summary>
    /// The vocabulary cannot name a table the schema does not have.
    /// </summary>
    [Fact]
    public void Every_append_only_table_appears_in_the_schema_document()
    {
        var schema = File.ReadAllText(RepoRoot.SchemaDocumentPath);

        var absent = AppendOnlyTables.All
            .Where(table => !Regex.IsMatch(schema, $@"\b{Regex.Escape(table)}\b"))
            .OrderBy(table => table, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            absent.Count == 0,
            $"These tables are declared append-only in {nameof(AppendOnlyTables)} but appear "
            + $"nowhere in {RepoRoot.SchemaDocumentPath}: {string.Join(", ", absent)}.");
    }

    [Fact]
    public void A_delete_against_a_vocabulary_table_is_reported()
    {
        const string Sql = "DELETE FROM contract_quotes WHERE snapshot_date = $date;";

        var offence = Assert.Single(AppendOnlyRewritesInSql.Offences(Sql, Vocabulary("contract_quotes")));

        Assert.Contains("DELETE FROM", offence, StringComparison.Ordinal);
        Assert.Contains("contract_quotes", offence, StringComparison.Ordinal);
    }

    [Fact]
    public void An_update_against_a_vocabulary_table_is_reported()
    {
        const string Sql = "UPDATE decisions SET chosen_candidate_id = $id WHERE decision_id = $d;";

        var offence = Assert.Single(AppendOnlyRewritesInSql.Offences(Sql, Vocabulary("decisions")));

        Assert.Contains("UPDATE", offence, StringComparison.Ordinal);
        Assert.Contains("decisions", offence, StringComparison.Ordinal);
    }

    /// <summary>
    /// SQLite admits four spellings of an identifier and tooling emits all of
    /// them, so a check that reported the bare form and missed the quoted one
    /// would be evaded by a paste from a database browser.
    /// </summary>
    [Theory]
    [InlineData("DELETE FROM config_rows;")]
    [InlineData("DELETE FROM \"config_rows\";")]
    [InlineData("DELETE FROM [config_rows];")]
    [InlineData("DELETE FROM `config_rows`;")]
    [InlineData("UPDATE config_rows SET value = '1';")]
    [InlineData("UPDATE \"config_rows\" SET value = '1';")]
    [InlineData("UPDATE [config_rows] SET value = '1';")]
    [InlineData("UPDATE `config_rows` SET value = '1';")]
    public void Every_quoting_style_of_a_vocabulary_table_is_reported(string sql)
    {
        Assert.NotEmpty(AppendOnlyRewritesInSql.Offences(sql, Vocabulary("config_rows")));
    }

    /// <summary>
    /// SQLite's conflict clause sits between the keyword and the table.
    /// </summary>
    [Fact]
    public void An_update_with_a_conflict_clause_is_reported()
    {
        const string Sql = "UPDATE OR ROLLBACK config_rows SET value = '1';";

        Assert.NotEmpty(AppendOnlyRewritesInSql.Offences(Sql, Vocabulary("config_rows")));
    }

    /// <summary>
    /// Mechanism one: statement form. The trigger DDL that creates the
    /// append-only property is not a rewrite, and is excluded because of what it
    /// is rather than because of where it lives.
    /// </summary>
    [Fact]
    public void The_trigger_ddl_is_not_a_rewrite()
    {
        const string Sql = """
            CREATE TRIGGER config_rows_no_update
            BEFORE UPDATE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only');
            END;

            CREATE TRIGGER config_rows_no_delete
            BEFORE DELETE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only');
            END;
            """;

        Assert.Empty(AppendOnlyRewritesInSql.Offences(Sql, AppendOnlyTables.All));
    }

    /// <summary>
    /// Mechanism two: the vocabulary. <c>SnapshotTests</c> updates a scaffold
    /// table it created inside the test, and the rule does not reach it.
    /// </summary>
    [Fact]
    public void A_table_outside_the_vocabulary_is_not_reported()
    {
        const string Sql = "UPDATE probe SET value = 'uncommitted';";

        Assert.Empty(AppendOnlyRewritesInSql.Offences(Sql, AppendOnlyTables.All));
    }

    /// <summary>
    /// Mechanism three: scan scope, and this is the one that needs stating,
    /// because these are real offences.
    /// </summary>
    /// <remarks>
    /// The three statements in <c>tests/</c> that carry the banned text against a
    /// vocabulary table ARE rewrites by every other measure, and they exist to
    /// assert the triggers reject them. Nothing about their form or their table
    /// excludes them; only the scope does. Asserted so the scope is a visible
    /// mechanism rather than an accident of which directory was scanned.
    /// </remarks>
    [Theory]
    [InlineData("UPDATE config_rows SET value = '99' WHERE key = 'Trial:MaxRolls';")]
    [InlineData("DELETE FROM config_rows WHERE key = 'Trial:MaxRolls';")]
    [InlineData("DELETE FROM config_rows;")]
    public void The_tests_that_prove_the_triggers_work_are_offences_and_are_out_of_scope(string sql)
    {
        Assert.NotEmpty(AppendOnlyRewritesInSql.Offences(sql, AppendOnlyTables.All));

        Assert.DoesNotContain(
            RepoRoot.SourceFilesUnder(RepoRoot.SourcePath),
            file => File.ReadAllText(file).Contains(sql, StringComparison.Ordinal));
    }

    /// <summary>
    /// The other known limit: a banned statement written in <c>tests/</c> by
    /// mistake, rather than to prove a trigger, is not caught.
    /// </summary>
    /// <remarks>
    /// The cost of mechanism three. Pinned so the gap is visible in the suite
    /// rather than only in prose. Closing it would mean distinguishing a test
    /// that asserts a rejection from one that does not, which is reading intent,
    /// and the alternative is an exemption list that 0.4 decided against.
    /// </remarks>
    [Fact]
    public void A_rewrite_written_in_the_test_project_is_a_known_miss()
    {
        var scanned = SourceFiles();

        Assert.NotEmpty(scanned);
        Assert.DoesNotContain(scanned, file => file.StartsWith(RepoRoot.TestProjectPath, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> SourceFiles() =>
        RepoRoot.SourceFilesUnder(RepoRoot.SourcePath);

    private static IReadOnlySet<string> Vocabulary(params string[] tables) =>
        tables.ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Finds SQL that deletes from or updates a named table.
/// </summary>
/// <remarks>
/// A pure function over text, so it is exercised on synthetic SQL as well as run
/// against the tree, following <see cref="DecimalOrderingInSql"/>. Extraction is
/// that type's <c>SqlIn</c> unchanged: its statement keywords already name both
/// <c>UPDATE</c> and <c>DELETE FROM</c>, so a literal carrying either is already
/// reached, including the trigger DDL.
/// <para>
/// <b>The statement form is what excludes the DDL.</b> <c>DELETE FROM</c>
/// requires the <c>FROM</c> and <c>UPDATE</c> requires the <c>SET</c>, so
/// <c>BEFORE UPDATE ON config_rows</c> and <c>BEFORE DELETE ON config_rows</c>
/// match neither. That is a property of what those statements are, not an
/// exemption granted to where they sit.
/// </para>
/// <para>
/// <b>Known limit: a table alias defeats it.</b>
/// <c>UPDATE config_rows AS c SET</c> puts a token between the name and
/// <c>SET</c>. Pinned as a test rather than papered over, and owed at Phase 1
/// alongside the decimal detector's alias miss, which is the same problem one
/// level down.
/// </para>
/// </remarks>
internal static class AppendOnlyRewritesInSql
{
    // An optional delimiter either side of the name. SQLite admits "x", [x], `x`
    // and bare x, and tooling emits all four; that is one statement spelled four
    // ways rather than four statements.
    private const string Open = @"[""\[`]?";
    private const string Close = @"[""\]`]?";

    private static readonly Regex Delete = new(
        $@"\bDELETE\s+FROM\s+{Open}(?<table>[A-Za-z_][A-Za-z0-9_]*){Close}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // The conflict clause is admitted between the keyword and the table:
    // SQLite allows UPDATE OR ROLLBACK, OR ABORT, OR REPLACE and so on.
    private static readonly Regex Update = new(
        $@"\bUPDATE\s+(?:OR\s+[A-Za-z]+\s+)?{Open}(?<table>[A-Za-z_][A-Za-z0-9_]*){Close}\s+SET\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static IReadOnlyList<string> Offences(string sql, IReadOnlySet<string> appendOnlyTables)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(appendOnlyTables);

        var offences = new List<string>();

        foreach (Match match in Delete.Matches(sql))
        {
            var table = match.Groups["table"].Value;

            if (appendOnlyTables.Contains(table))
            {
                offences.Add($"DELETE FROM {table}");
            }
        }

        foreach (Match match in Update.Matches(sql))
        {
            var table = match.Groups["table"].Value;

            if (appendOnlyTables.Contains(table))
            {
                offences.Add($"UPDATE {table}");
            }
        }

        return offences;
    }
}
