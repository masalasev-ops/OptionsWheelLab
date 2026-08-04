using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-NoNondeterministicSql: no SQL under <c>src/</c> calls a function whose
/// value varies between runs, and the barred list is checked against the bundled
/// binary [D-W51].
/// </summary>
/// <remarks>
/// The sibling of FX-ClockIsNotADateSource, raised by the same enumeration. That
/// one found seven functions reading the wall clock; the same pass showed
/// <c>random()</c> and <c>randomblob()</c> beside them, outside it by name and
/// able to break a byte-identical run just as surely.
/// <para>
/// <b>The rule is narrower than barring randomness, which the lab requires</b>
/// [D-W51]. One of the three makers is a random-within-band control and its seed
/// is a config row. What is barred is randomness whose source is the store,
/// because a seeded run must reproduce and <c>random()</c> cannot be seeded.
/// </para>
/// <para>
/// <b>Three classes are not covered, and naming them is half the definition of
/// done.</b> They are named here as well as in the register, so a reader of the
/// check finds the limit without going to look it up. Each has a case below
/// asserting that it stays uncovered, so a later widening fails here and returns
/// to the decision rather than passing quietly.
/// </para>
/// </remarks>
public sealed class FX_NoNondeterministicSql
{
    /// <summary>
    /// The barred calls. A call, not the word: <c>Policy:Random:Seed</c> and the
    /// prose around it name the random maker legitimately and often.
    /// </summary>
    /// <remarks>
    /// A catch-list, not an exemption list, exactly as the clock fixture's is: an
    /// incomplete one still catches what is on it, so adding a form never needs a
    /// decision.
    /// </remarks>
    private static readonly (string Pattern, string Form)[] BarredCalls =
    [
        (@"\brandom\s*\(", "random(), whose value varies between two runs"),
        (@"\brandomblob\s*\(", "randomblob(), whose value varies between two runs"),
    ];

    /// <summary>The names behind the patterns, checked against the binary.</summary>
    private static readonly string[] BarredNames = ["random", "randomblob"];

    /// <summary>
    /// Deterministic given an identical insertion history, so barring them would
    /// fail a run that already reproduces [D-W51]. Uncovered class two.
    /// </summary>
    private static readonly string[] ConnectionStateFunctions =
        ["last_insert_rowid", "changes", "total_changes"];

    /// <summary>
    /// These vary by binary rather than by run, which is build determinism and a
    /// different property [D-W51]. Uncovered class three.
    /// </summary>
    private static readonly string[] VersionFunctions =
        ["sqlite_version", "sqlite_source_id"];

    [Fact]
    public void No_sql_under_src_calls_a_barred_function()
    {
        var statements = RepoRoot
            .SourceFilesUnder(RepoRoot.SourcePath)
            .SelectMany(file => DecimalOrderingInSql
                .SqlIn(File.ReadAllText(file))
                .Select(sql => (File: Path.GetFileName(file), Sql: sql)))
            .ToList();

        // The vacuity guard every scanning check here carries. An extraction that
        // found nothing would pass this silently and assert about an empty set.
        Assert.NotEmpty(statements);
        Assert.Contains(statements, statement => statement.Sql.Contains("config_rows", StringComparison.Ordinal));

        var offences = statements
            .SelectMany(statement => BarredCallsIn(statement.Sql)
                .Select(form => $"{statement.File}: {form}"))
            .OrderBy(offence => offence, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offences.Count == 0,
            "This SQL takes its randomness from the store, which cannot be seeded and so cannot "
            + $"reproduce: {string.Join(", ", offences)}. Randomness the lab needs is produced in "
            + "code from a seeded generator [D-W51].");
    }

    /// <summary>The detector, on synthetic SQL, so it is known to fire.</summary>
    [Theory]
    [InlineData("SELECT * FROM candidates ORDER BY random();")]
    [InlineData("SELECT random() AS pick FROM candidates;")]
    [InlineData("INSERT INTO trials (nonce) VALUES (randomblob(16));")]
    [InlineData("CREATE TABLE t (id BLOB NOT NULL DEFAULT (randomblob(8)));")]
    [InlineData("SELECT * FROM candidates ORDER BY RANDOM ();")]
    public void A_barred_call_is_reported(string sql)
    {
        Assert.NotEmpty(BarredCallsIn(sql));
    }

    /// <summary>
    /// The word without the call is not an offence, which is why the patterns
    /// require a parenthesis.
    /// </summary>
    /// <remarks>
    /// Not hypothetical. <c>Policy:Random:Seed</c> is a config key, the random
    /// maker is named in seed descriptions, and a check keying on the word would
    /// fire on the very mechanism this decision points at as the correct one.
    /// </remarks>
    [Theory]
    [InlineData("SELECT value FROM config_rows WHERE key = 'Policy:Random:Seed';")]
    [InlineData("INSERT INTO config_rows (key, note) VALUES ('Policy:Random:DeltaMax', 'random control band');")]
    public void The_word_without_a_call_is_not_an_offence(string sql)
    {
        Assert.Empty(BarredCallsIn(sql));
    }

    /// <summary>
    /// Every barred name still exists in the bundled SQLite, so the list is
    /// checked against the binary rather than trusted [D-W51].
    /// </summary>
    [Fact]
    public void Every_barred_name_still_exists_in_the_bundled_sqlite()
    {
        var registered = RegisteredFunctions();

        var missing = BarredNames
            .Where(function => !registered.ContainsKey(function))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These names are barred and the bundled SQLite no longer registers them: "
            + $"{string.Join(", ", missing)}. The list was enumerated from the binary, so a "
            + "change here means the enumeration needs redoing.");
    }

    /// <summary>
    /// <c>SQLITE_DETERMINISTIC</c> is not the discriminator, and the measurement
    /// is why the list is two names rather than forty-eight.
    /// </summary>
    /// <remarks>
    /// <b>Measured rather than argued.</b> Of the 168 functions the bundled
    /// SQLite 3.53.3 registers, 48 lack the <c>SQLITE_DETERMINISTIC</c> flag. Only
    /// two of those vary between two runs over the same data. The rest are
    /// aggregates and window functions, whose values are fixed given the same
    /// input rows in the same order, plus the clock functions
    /// FX-ClockIsNotADateSource already holds, the two classes named uncovered
    /// below, and full-text and r-tree internals this lab does not use.
    /// <para>
    /// <b>So barring on the flag would fail <c>count</c>, <c>sum</c>, <c>max</c>
    /// and <c>min</c></b>, which this schema uses throughout, notably in
    /// <c>MAX(version)</c> for current config. That is the check a reader would
    /// reach for first and it is wrong, which is worth pinning rather than
    /// leaving to be rediscovered.
    /// </para>
    /// <para>
    /// The residual is bounded the way the clock fixture bounds its own: an
    /// upgrade changing either count returns here.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_deterministic_flag_over_reports_and_is_not_the_discriminator()
    {
        var registered = RegisteredFunctions();

        // SQLITE_DETERMINISTIC == 0x000000800.
        var withoutTheFlag = registered
            .Where(function => (function.Value & 0x800) == 0)
            .Select(function => function.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(168, registered.Count);
        Assert.Equal(48, withoutTheFlag.Count);

        // Both barred names are in it, which is why the flag looks like the answer.
        Assert.All(BarredNames, name => Assert.Contains(name, withoutTheFlag));

        // And so are these, which the schema uses and must keep using.
        Assert.All(
            new[] { "count", "sum", "max", "min" },
            name => Assert.Contains(name, withoutTheFlag));
    }

    /// <summary>
    /// Uncovered class two, asserted so that widening the bar to reach it fails
    /// here rather than passing [D-W51].
    /// </summary>
    [Fact]
    public void Connection_state_functions_are_not_barred()
    {
        Assert.All(
            ConnectionStateFunctions,
            name => Assert.Empty(BarredCallsIn($"SELECT {name}();")));
    }

    /// <summary>Uncovered class three, asserted for the same reason.</summary>
    [Fact]
    public void Version_functions_are_not_barred()
    {
        Assert.All(
            VersionFunctions,
            name => Assert.Empty(BarredCallsIn($"SELECT {name}();")));
    }

    /// <summary>
    /// Uncovered class one, row order, held where it can be held.
    /// </summary>
    /// <remarks>
    /// A <c>SELECT</c> has no guaranteed order, and a scanner cannot tell a scalar
    /// read from a sequence read without understanding the query, so this class is
    /// not scanned [D-W51]. What can be asserted is the one seam the byte-identical
    /// property actually rests on: the ledger read whose result FX-RunIsByteIdentical
    /// renders in order and compares. Unordered, two runs could produce the same
    /// rows in a different sequence and the comparison would fail for a reason that
    /// has nothing to do with the run.
    /// </remarks>
    [Fact]
    public void The_ledger_read_the_byte_identical_run_compares_orders_explicitly()
    {
        var sql = DecimalOrderingInSql
            .SqlIn(File.ReadAllText(
                Path.Combine(RepoRoot.SourcePath, "OptionsWheelLab.Core", "Storage", "TrialStore.cs")))
            .Where(statement => statement.Contains("FROM ledger_entries", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(sql);

        Assert.All(
            sql,
            statement => Assert.Contains("ORDER BY", statement, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> BarredCallsIn(string sql) =>
        [.. BarredCalls
            .Where(call => Regex.IsMatch(
                DecimalOrderingInSql.WithoutComments(sql),
                call.Pattern,
                RegexOptions.IgnoreCase))
            .Select(call => call.Form)];

    /// <summary>Every function the bundled SQLite registers, with its flags.</summary>
    private static Dictionary<string, long> RegisteredFunctions()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT name, flags FROM pragma_function_list();";

        var registered = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            registered[reader.GetString(0)] = reader.GetInt64(1);
        }

        Assert.NotEmpty(registered);

        return registered;
    }
}
