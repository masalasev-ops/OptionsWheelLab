using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-SnapshotNeverRewritten: a vendor correction appends rather than updates.
/// </summary>
/// <remarks>
/// The fixture the amended keys exist for. <c>underlying_bars</c>,
/// <c>chain_snapshots</c> and <c>contract_quotes</c> were keyed without
/// <c>observed_at</c> until v1.17.0, so a second row for the same bar violated the
/// key and the only way to record a correction was an update, which D-W8 forbids.
/// <para>
/// <b>Both halves are asserted, and they are different claims.</b> That a
/// correction can be written is the key change. That a rewrite cannot be is the
/// triggers. A schema with the first and not the second would let a writer outside
/// <c>src/</c> destroy an observation, and FX-NoRewriteOfAppendOnlyTables cannot see
/// that writer because it reads source text.
/// </para>
/// </remarks>
public sealed class FX_SnapshotNeverRewritten
{
    private const string Symbol = "WDGT";
    private const string SessionDate = "2026-03-02";

    private static readonly DateTimeOffset Observed =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Corrected =
        new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_correction_appends_and_both_rows_survive_with_their_own_stamps()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);
        InsertBar(connection, close: "52.44000000", observedAt: Corrected);

        var rows = BarsFor(connection);

        Assert.Equal(2, rows.Count);
        Assert.Equal(("52.40000000", StoreTimestamp.ToStored(Observed)), rows[0]);
        Assert.Equal(("52.44000000", StoreTimestamp.ToStored(Corrected)), rows[1]);
    }

    /// <summary>
    /// The as-of read, which is what the correction is for.
    /// </summary>
    /// <remarks>
    /// A read at a date between the two stamps sees the first observation and not
    /// the second, so a decision replayed at that date sees what stood at the time
    /// rather than what the vendor said afterwards. This is the first as-of read
    /// over a market-data table; 1.2 gives it a home in <c>src/</c>.
    /// </remarks>
    [Fact]
    public void An_as_of_read_between_the_stamps_sees_the_first_and_not_the_second()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);
        InsertBar(connection, close: "52.44000000", observedAt: Corrected);

        var between = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("52.40000000", CloseAsOf(connection, between));
        Assert.Equal("52.44000000", CloseAsOf(connection, Corrected));
    }

    /// <summary>
    /// Before either observation there is nothing, rather than the earliest row.
    /// </summary>
    [Fact]
    public void An_as_of_read_before_the_first_stamp_sees_nothing()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);

        var before = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Null(CloseAsOf(connection, before));
    }

    /// <summary>
    /// The key admits the correction, which is the change v1.17.0 made.
    /// </summary>
    /// <remarks>
    /// Asserted directly rather than inferred from the append succeeding: a second
    /// row at the SAME stamp is still a key violation, which is what makes the
    /// stamp the thing that distinguishes them rather than the row count.
    /// </remarks>
    [Fact]
    public void Two_observations_at_one_stamp_are_still_a_key_violation()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);

        var thrown = Assert.Throws<SqliteException>(
            () => InsertBar(connection, close: "52.44000000", observedAt: Observed));

        Assert.Contains("UNIQUE", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An <c>UPDATE</c> against each of the six is refused by the store.
    /// </summary>
    /// <remarks>
    /// By the store, not by the detector. FX-NoRewriteOfAppendOnlyTables reads
    /// <c>src/</c> and cannot see a writer at a <c>sqlite3</c> prompt; these
    /// triggers hold against any writer.
    /// <para>
    /// <b>Each table is seeded first, and that is not incidental.</b> A SQLite
    /// trigger is per row, so an <c>UPDATE</c> against an empty table matches
    /// nothing, fires nothing and succeeds. Asserting the refusal on an empty table
    /// would have passed for the wrong reason on the delete cases and failed on
    /// nothing, which is how a check that guards an empty set reads as green.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(SnapshotTables))]
    public void An_update_against_a_snapshot_table_is_refused(string table)
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneRow(connection, table);

        var column = AColumnOf(table);

        var thrown = Assert.Throws<SqliteException>(
            () => Execute(connection, $"UPDATE {table} SET {column} = {column};"));

        Assert.Contains("append-only", thrown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(table, thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SnapshotTables))]
    public void A_delete_against_a_snapshot_table_is_refused(string table)
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneRow(connection, table);

        var thrown = Assert.Throws<SqliteException>(
            () => Execute(connection, $"DELETE FROM {table};"));

        Assert.Contains("append-only", thrown.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(table, thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The seed itself put a row in, so the refusals above met a row rather than an
    /// empty table.
    /// </summary>
    /// <remarks>
    /// Without this the two theories could both pass against a seed that silently
    /// inserted nothing, since a trigger that never fires and a table that was never
    /// written look the same from outside.
    /// </remarks>
    [Theory]
    [MemberData(nameof(SnapshotTables))]
    public void The_seed_puts_exactly_one_row_in(string table)
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneRow(connection, table);

        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {table};";

        Assert.Equal(1L, count.ExecuteScalar());
    }

    /// <summary>
    /// Every table this checkpoint adds is append-only or is a declared
    /// projection.
    /// </summary>
    /// <remarks>
    /// The direction that was unmeetable at 0.7, when the vocabulary named all six
    /// forward and none existed. It is the per-checkpoint definition of done from
    /// <c>DecimalColumns</c>'s two-way contract, and 1.1 was the first checkpoint
    /// that could discharge it against real tables.
    /// <para>
    /// <b>Widened at 3.3, when the first table that is neither arrived.</b> Until
    /// then every table in this store was append-only, so a single-list assertion
    /// was total by accident rather than by design. <c>trials</c> and
    /// <c>positions</c> are projections and may be rewritten [D-W35], so the
    /// claim is now a partition: every created table falls in exactly one list.
    /// </para>
    /// <para>
    /// <b>Exactly one, not at least one, and the disjointness is the half that
    /// matters.</b> A table appearing in both would be permitted to be rewritten
    /// by one rule and forbidden by the other, and every check downstream would
    /// read whichever list it happened to consult. That is how a vocabulary stops
    /// meaning anything, and it costs one assertion to make impossible.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_table_this_checkpoint_adds_is_append_only_or_a_projection()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var created = TablesIn(connection)
            .Where(name => !name.StartsWith("sqlite_", StringComparison.Ordinal))
            .Where(name => name is not ("config_rows" or "schema_migrations"))
            .ToList();

        Assert.NotEmpty(created);

        Assert.All(
            created,
            table => Assert.True(
                AppendOnlyTables.All.Contains(table) ^ ProjectionTables.All.Contains(table),
                $"'{table}' is in {(AppendOnlyTables.All.Contains(table) ? "both" : "neither")} "
                + "vocabulary. A created table is append-only or it is a projection rebuilt "
                + "from one, and a table in both is governed by two rules that contradict "
                + "[D-W35]."));
    }

    /// <summary>
    /// The two vocabularies share no name, asserted over the lists rather than
    /// over the tables that happen to exist.
    /// </summary>
    /// <remarks>
    /// The assertion above can only see created tables, and both lists carry
    /// forward declarations for phases not yet built: <c>decisions</c> and
    /// <c>candidates</c> are Phase 4's. A collision declared there would sit
    /// undetected until the migration that created it.
    /// </remarks>
    [Fact]
    public void The_two_vocabularies_are_disjoint()
    {
        Assert.Empty(AppendOnlyTables.All.Intersect(ProjectionTables.All, StringComparer.OrdinalIgnoreCase));
    }

    public static TheoryData<string> SnapshotTables() =>
    [
        "market_sessions",
        "underlying_bars",
        "corporate_actions",
        "earnings_calendar",
        "chain_snapshots",
        "contracts",
        "contract_quotes",
    ];

    /// <summary>
    /// A column the table actually has, for the update that must be refused.
    /// </summary>
    /// <remarks>
    /// <c>observed_at</c> is not universal: <c>contracts</c> carries none, because a
    /// corporate action mints a new identity rather than restating an old row [§4.1].
    /// So the update sets a column to itself, which changes nothing and still has to
    /// be refused, since the rule is that the row is never rewritten and not that its
    /// value must differ.
    /// </remarks>
    private static string AColumnOf(string table) => table switch
    {
        "contract_quotes" => "snapshot_date",

        // The one table with no symbol at all, which is what it is for: a
        // session is a fact about the market rather than about a name [D-W46].
        "market_sessions" => "session_date",

        _ => "symbol",
    };

    /// <summary>
    /// One minimal row per table, so a row-level trigger has something to fire on.
    /// </summary>
    /// <remarks>
    /// Only the NOT NULL columns are supplied. A trigger fires on any row, so the
    /// row's content is irrelevant and the minimum is what keeps this legible.
    /// </remarks>
    private static void SeedOneRow(SqliteConnection connection, string table)
    {
        var stamp = StoreTimestamp.ToStored(Observed);

        var sql = table switch
        {
            "market_sessions" =>
                "INSERT INTO market_sessions (session_date, observed_at) "
                + $"VALUES ('{SessionDate}', '{stamp}');",

            "underlying_bars" =>
                "INSERT INTO underlying_bars "
                + "(symbol, session_date, open, high, low, close, adj_close, volume, observed_at) "
                + $"VALUES ('{Symbol}', '{SessionDate}', '1', '1', '1', '1', '1', 1, '{stamp}');",

            "corporate_actions" =>
                "INSERT INTO corporate_actions (symbol, ex_date, kind, observed_at) "
                + $"VALUES ('{Symbol}', '{SessionDate}', 'split', '{stamp}');",

            "earnings_calendar" =>
                "INSERT INTO earnings_calendar (symbol, report_date, session, observed_at) "
                + $"VALUES ('{Symbol}', '{SessionDate}', 'after', '{stamp}');",

            "chain_snapshots" =>
                "INSERT INTO chain_snapshots (symbol, snapshot_date, observed_at) "
                + $"VALUES ('{Symbol}', '{SessionDate}', '{stamp}');",

            "contracts" =>
                "INSERT INTO contracts (symbol, expiry, right, strike) "
                + $"VALUES ('{Symbol}', '2026-04-17', 'put', '50.00000000');",

            // The contract comes first because the reference is enforced.
            // Microsoft.Data.Sqlite turns foreign keys on by default, which a bare
            // sqlite3 prompt does not, so the quote cannot point at a contract that
            // does not exist.
            "contract_quotes" =>
                "INSERT INTO contracts (contract_id, symbol, expiry, right, strike) "
                + $"VALUES (1, '{Symbol}', '2026-04-17', 'put', '50.00000000');"
                + "INSERT INTO contract_quotes "
                + "(contract_id, snapshot_date, bid, ask, observed_at) "
                + $"VALUES (1, '{SessionDate}', '0.30000000', '0.36000000', '{stamp}');",

            _ => throw new ArgumentOutOfRangeException(
                nameof(table),
                table,
                "No seed row is defined for this table, so a refusal asserted against it "
                + "would pass on an empty table without firing a trigger."),
        };

        Execute(connection, sql);
    }

    private static void InsertBar(SqliteConnection connection, string close, DateTimeOffset observedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO underlying_bars
                (symbol, session_date, open, high, low, close, adj_close, volume, observed_at)
            VALUES ($symbol, $session, $open, $high, $low, $close, $adj, $volume, $observed);
            """;
        command.Parameters.AddWithValue("$symbol", Symbol);
        command.Parameters.AddWithValue("$session", SessionDate);
        command.Parameters.AddWithValue("$open", "52.00000000");
        command.Parameters.AddWithValue("$high", "52.90000000");
        command.Parameters.AddWithValue("$low", "51.80000000");
        command.Parameters.AddWithValue("$close", close);
        command.Parameters.AddWithValue("$adj", close);
        command.Parameters.AddWithValue("$volume", 1_200_000L);
        command.Parameters.AddWithValue("$observed", StoreTimestamp.ToStored(observedAt));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// The close in force at an instant: the latest observation at or before it.
    /// </summary>
    private static string? CloseAsOf(SqliteConnection connection, DateTimeOffset asOf)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT close
            FROM underlying_bars
            WHERE symbol = $symbol
              AND session_date = $session
              AND observed_at <= $asOf
            ORDER BY observed_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$symbol", Symbol);
        command.Parameters.AddWithValue("$session", SessionDate);
        command.Parameters.AddWithValue("$asOf", StoreTimestamp.ToStored(asOf));

        return command.ExecuteScalar() as string;
    }

    private static IReadOnlyList<(string Close, string ObservedAt)> BarsFor(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT close, observed_at
            FROM underlying_bars
            WHERE symbol = $symbol AND session_date = $session
            ORDER BY observed_at;
            """;
        command.Parameters.AddWithValue("$symbol", Symbol);
        command.Parameters.AddWithValue("$session", SessionDate);

        var rows = new List<(string, string)>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static IReadOnlyList<string> TablesIn(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";

        var names = new List<string>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Observed);
        return store;
    }
}
