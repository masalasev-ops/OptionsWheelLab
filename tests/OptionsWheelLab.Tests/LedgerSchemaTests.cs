using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Migration 8's three tables: the record refuses a rewrite and the two
/// projections permit one, which is [D-W35]'s two halves against the store.
/// </summary>
/// <remarks>
/// Not a registered fixture, on <see cref="BarsSchemaTests"/>' argument.
/// <para>
/// <b>The permissive half has never been asserted anywhere in this corpus, and
/// it is not the trivial one.</b> Every table before 3.3 was append-only, so
/// "can this be updated" had one answer and no test needed to ask. A projection
/// that quietly acquired a pair of triggers, by a copy-paste in the migration
/// that creates it beside a record, would be unrewritable: the rebuild
/// [D-W35] requires would fail at its first write, and it would fail as a store
/// error rather than as anything naming the cause.
/// </para>
/// <para>
/// <b>The nullable bases are asserted rather than read off the DDL.</b> Cost
/// basis exists after assignment [D-W19], so a position in <c>cash</c> or
/// <c>short_put</c> has none. Under §4.3's unmarked convention both columns would
/// have been <c>NOT NULL</c> and two of the four states unwritable, which is a
/// schema that forbids the strategy's opening move.
/// </para>
/// </remarks>
public sealed class LedgerSchemaTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_update_to_the_ledger_is_refused()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE ledger_entries SET amount = amount;";

        var refusal = Assert.Throws<SqliteException>(() => update.ExecuteNonQuery());

        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("ledger_entries", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_delete_from_the_ledger_is_refused()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM ledger_entries;";

        var refusal = Assert.Throws<SqliteException>(() => delete.ExecuteNonQuery());

        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A projection can be updated and discarded, which is what makes rebuilding
    /// it possible at all.
    /// </summary>
    /// <remarks>
    /// <b>This found a defect on its first run.</b> Migration 8 was written with
    /// <c>ledger_entries.trial_id</c> referencing <c>trials</c>, which points the
    /// record at the projection derived from it: with foreign keys on, discarding
    /// <c>trials</c> to rebuild it was refused by the store. §4.3 carries no
    /// arrows where §4.1 carries three, so the schema had already said so and the
    /// absence read as an omission.
    /// <para>
    /// Each table is discarded on its own rather than in an order, because an
    /// order that works is not the property: a rebuild takes one projection at a
    /// time and a test that only ever deletes children first would pass against
    /// exactly the schema this rejected.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("trials")]
    [InlineData("positions")]
    public void A_projection_can_be_rewritten(string table)
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Execute(connection, $"UPDATE {table} SET trial_id = trial_id;");
        Execute(connection, $"DELETE FROM {table};");

        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {table};";

        Assert.Equal(0L, count.ExecuteScalar());
    }

    /// <summary>
    /// Every declared projection is a table that exists, so the list cannot name
    /// something the schema does not have.
    /// </summary>
    [Fact]
    public void Every_declared_projection_exists_in_the_store()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        foreach (var table in ProjectionTables.All)
        {
            using var read = connection.CreateCommand();
            read.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            read.Parameters.AddWithValue("$name", table);

            Assert.Equal(1L, read.ExecuteScalar());
        }
    }

    /// <summary>
    /// A position before assignment carries no basis, and the schema admits it.
    /// </summary>
    [Fact]
    public void A_short_put_position_is_writable_with_no_basis()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Execute(
            connection,
            """
            INSERT INTO positions (trial_id, state, effective_from, shares)
            VALUES (1, 'short_put', '2026-03-02', 0);
            """);

        using var read = connection.CreateCommand();
        read.CommandText =
            "SELECT COUNT(*) FROM positions WHERE gross_basis IS NULL AND net_basis IS NULL;";

        Assert.Equal(2L, read.ExecuteScalar());
    }

    /// <summary>
    /// The ledger's vocabulary is enforced by the store, in both directions.
    /// </summary>
    [Fact]
    public void The_kind_check_admits_the_vocabulary_and_refuses_a_word_outside_it()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        foreach (var kind in new[]
        {
            "premium_received", "premium_paid", "expired_worthless", "assignment",
            "call_away", "shares_sold", "dividend", "commission", "assignment_fee",
            "stopped",
        })
        {
            using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO ledger_entries (trial_id, entry_date, known_on, kind, amount)
                VALUES (1, '2026-03-02', '2026-03-02', $kind, '0.00000000');
                """;
            insert.Parameters.AddWithValue("$kind", kind);

            insert.ExecuteNonQuery();
        }

        using var refused = connection.CreateCommand();
        refused.CommandText =
            """
            INSERT INTO ledger_entries (trial_id, entry_date, known_on, kind, amount)
            VALUES (1, '2026-03-02', '2026-03-02', 'premium', '0.00000000');
            """;

        var refusal = Assert.Throws<SqliteException>(() => refused.ExecuteNonQuery());
        Assert.Contains("CHECK", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An entry that moves no cash is writable, which is what recording events
    /// rather than only cash means [D-W48].
    /// </summary>
    [Fact]
    public void An_expiry_that_pays_nothing_is_an_entry()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Execute(
            connection,
            """
            INSERT INTO ledger_entries (trial_id, entry_date, known_on, kind, amount)
            VALUES (1, '2026-05-15', '2026-05-15', 'expired_worthless', '0.00000000');
            """);

        using var read = connection.CreateCommand();
        read.CommandText =
            "SELECT amount FROM ledger_entries WHERE kind = 'expired_worthless';";

        Assert.Equal("0.00000000", read.ExecuteScalar());
    }

    /// <summary>
    /// A store at current schema holding one trial, one position and one entry.
    /// </summary>
    /// <remarks>
    /// A trial first, because the references are enforced:
    /// Microsoft.Data.Sqlite turns foreign keys on by default.
    /// </remarks>
    private static TempStore SeededStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);

        using var connection = store.Connections.Open(StoreAccess.Write);

        Execute(
            connection,
            """
            INSERT INTO trials
                (trial_id, maker_id, symbol, opened_on, open_strike, committed_capital, rolls_used)
            VALUES (1, 'baseline', 'WDGT', '2026-03-02', '50.00000000', '5000.00000000', 0);

            INSERT INTO positions (trial_id, state, effective_from, shares)
            VALUES (1, 'cash', '2026-03-02', 0);

            INSERT INTO ledger_entries (trial_id, entry_date, known_on, kind, amount)
            VALUES (1, '2026-03-02', '2026-03-02', 'premium_received', '94.35000000');
            """);

        return store;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
