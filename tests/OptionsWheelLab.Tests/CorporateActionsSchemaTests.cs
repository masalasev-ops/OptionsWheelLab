using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Migration 6's rebuild keeps every row a real store holds, and the CHECK,
/// the triggers and the index all survive it.
/// </summary>
/// <remarks>
/// Not a registered fixture, on <see cref="BarsSchemaTests"/>' argument: this is
/// a migration's own behaviour rather than a property of the domain.
/// <para>
/// <b>The copy is the whole risk, and it is a different risk from migration
/// 5's.</b> That rebuild ran against a table no writer had ever touched, so no
/// store could hold a row for its copy to lose. This one runs against a table
/// <see cref="CorporateActionWriter"/> has written since 1.5, so the seeding
/// below goes through that writer rather than through hand-written SQL: a copy
/// asserted against rows the production path produced is the only version of
/// this assertion that means anything.
/// </para>
/// <para>
/// <b>The trigger assertions are against a seeded row, because a trigger is per
/// row.</b> 1.4's lesson, restated here because the failure is silent:
/// <c>DROP TABLE</c> takes the triggers with it, and a forgotten recreation
/// passes every schema check and fails only when something tries to rewrite the
/// table. It takes the index too, which nothing would notice at all.
/// </para>
/// </remarks>
public sealed class CorporateActionsSchemaTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Expiry = new(2026, 4, 17);
    private static readonly DateOnly ExDate = new(2026, 3, 16);

    /// <summary>WORKED_EXAMPLE's three-for-two, as 1.5's fixture states it.</summary>
    private static readonly StatedSuccessorTerms Stated =
        new(Strike: 60m, DeliverableShares: 150, Multiplier: 100);

    /// <summary>
    /// A store populated through the writer carries its rows across the rebuild,
    /// with every column intact.
    /// </summary>
    [Fact]
    public void A_store_populated_through_the_writer_survives_the_rebuild()
    {
        using var store = TempStore.Empty();

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            ApplyThrough(connection, id: 5);
            SeedThroughTheWriter(connection);
        }

        new MigrationRunner(store.Connections).Run(Instant.AddMinutes(1));

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);
        using var read = reader.CreateCommand();
        read.CommandText =
            """
            SELECT symbol, ex_date, kind, ratio, amount, observed_at
            FROM corporate_actions;
            """;

        using var row = read.ExecuteReader();

        Assert.True(row.Read(), "The rebuild lost the row the writer inserted.");
        Assert.Equal("WDGT", row.GetString(0));
        Assert.Equal("2026-03-16", row.GetString(1));
        Assert.Equal("split", row.GetString(2));
        Assert.Equal("1.50000000", row.GetString(3));
        Assert.True(row.IsDBNull(4), "A split carries a ratio and no amount.");
        Assert.Equal("2026-07-30T09:00:00.000Z", row.GetString(5));
        Assert.False(row.Read(), "The rebuild duplicated the row.");
    }

    /// <summary>
    /// The successor the same transaction minted is still reachable through its
    /// predecessor link after the rebuild.
    /// </summary>
    /// <remarks>
    /// The event row and the contract row are written atomically [1.5], and only
    /// one of the two tables is rebuilt here, so this is the assertion that the
    /// rebuild did not sever the halves of one act.
    /// </remarks>
    [Fact]
    public void The_minted_successor_outlives_the_rebuild()
    {
        using var store = TempStore.Empty();

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            ApplyThrough(connection, id: 5);
            SeedThroughTheWriter(connection);
        }

        new MigrationRunner(store.Connections).Run(Instant.AddMinutes(1));

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);
        using var read = reader.CreateCommand();
        read.CommandText =
            """
            SELECT strike, deliverable_shares
            FROM contracts
            WHERE predecessor_contract_id IS NOT NULL;
            """;

        using var row = read.ExecuteReader();

        Assert.True(row.Read());
        Assert.Equal("60.00000000", row.GetString(0));
        Assert.Equal(150L, row.GetInt64(1));
    }

    [Fact]
    public void An_update_is_still_refused_after_the_rebuild()
    {
        using var store = MigratedStoreWithAnAction();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Equal(1L, CountActions(connection));

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE corporate_actions SET kind = kind;";

        var refusal = Assert.Throws<SqliteException>(() => update.ExecuteNonQuery());
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_delete_is_still_refused_after_the_rebuild()
    {
        using var store = MigratedStoreWithAnAction();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Equal(1L, CountActions(connection));

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM corporate_actions;";

        var refusal = Assert.Throws<SqliteException>(() => delete.ExecuteNonQuery());
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The as-of index went with the table and came back, which nothing else
    /// would notice.
    /// </summary>
    /// <remarks>
    /// A missing index is the one casualty of a rebuild that changes no answer,
    /// only how long it takes to get one, so no behavioural test can find it.
    /// </remarks>
    [Fact]
    public void The_as_of_index_survives_the_rebuild()
    {
        using var store = MigratedStoreWithAnAction();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        using var read = connection.CreateCommand();
        read.CommandText =
            "SELECT COUNT(*) FROM pragma_index_list('corporate_actions') "
            + "WHERE name = 'corporate_actions_as_of';";

        Assert.Equal(1L, (long)read.ExecuteScalar()!);
    }

    /// <summary>
    /// The CHECK is what the rebuild was for, so it is asserted in both
    /// directions.
    /// </summary>
    [Fact]
    public void The_check_admits_the_vocabulary_and_refuses_a_word_outside_it()
    {
        using var store = MigratedStoreWithAnAction();
        using var connection = store.Connections.Open(StoreAccess.Write);

        foreach (var kind in new[]
        {
            "ordinary_dividend", "non_ordinary_dividend", "split", "rights_offering",
            "reorganization", "merger", "liquidation", "spin_off",
        })
        {
            using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO corporate_actions (symbol, ex_date, kind, observed_at)
                VALUES ('WDGT', '2026-03-16', $kind, '2026-07-30T09:00:00.000Z');
                """;
            insert.Parameters.AddWithValue("$kind", kind);

            insert.ExecuteNonQuery();
        }

        using var refused = connection.CreateCommand();
        refused.CommandText =
            """
            INSERT INTO corporate_actions (symbol, ex_date, kind, observed_at)
            VALUES ('WDGT', '2026-03-16', 'Split', '2026-07-30T09:00:00.000Z');
            """;

        var refusal = Assert.Throws<SqliteException>(() => refused.ExecuteNonQuery());
        Assert.Contains("CHECK", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>A store at current schema holding one action written by the writer.</summary>
    private static TempStore MigratedStoreWithAnAction()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            SeedThroughTheWriter(connection);
        }

        return store;
    }

    /// <summary>
    /// One predecessor contract and one split through
    /// <see cref="CorporateActionWriter"/>, which is the only production path
    /// into this table.
    /// </summary>
    private static void SeedThroughTheWriter(SqliteConnection connection)
    {
        long predecessorId;

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                INSERT INTO contracts (symbol, expiry, right, strike, multiplier, deliverable_shares)
                VALUES ($symbol, $expiry, 'put', '90.00000000', 100, 100)
                RETURNING contract_id;
                """;
            insert.Parameters.AddWithValue("$symbol", Symbol.Value);
            insert.Parameters.AddWithValue("$expiry", StoreDate.ToStored(Expiry));
            predecessorId = (long)insert.ExecuteScalar()!;
        }

        new CorporateActionWriter(connection).MintSuccessor(
            predecessorId,
            Stated,
            new CorporateAction(CorporateActionKind.Split, ExDate, Ratio: 1.5m),
            Instant);
    }

    private static long CountActions(SqliteConnection connection)
    {
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM corporate_actions;";
        return (long)count.ExecuteScalar()!;
    }

    /// <summary>
    /// A store at the schema <paramref name="id"/> leaves it, built from the
    /// frozen migration list itself.
    /// </summary>
    /// <remarks>
    /// By id rather than by <c>SkipLast</c>, which
    /// <see cref="BarsSchemaTests"/> uses: that form names a store by how far it
    /// is from current, so every migration added after it silently changes which
    /// store the test builds. This one names the schema it means.
    /// </remarks>
    private static void ApplyThrough(SqliteConnection connection, int id)
    {
        using (var ledger = connection.CreateCommand())
        {
            ledger.CommandText =
                """
                CREATE TABLE schema_migrations (
                    id         INTEGER NOT NULL PRIMARY KEY,
                    name       TEXT    NOT NULL,
                    applied_at TEXT    NOT NULL
                );
                """;
            ledger.ExecuteNonQuery();
        }

        foreach (var migration in Migrations.All.Where(migration => migration.Id <= id))
        {
            using (var apply = connection.CreateCommand())
            {
                apply.CommandText = migration.Sql;
                apply.ExecuteNonQuery();
            }

            using var record = connection.CreateCommand();
            record.CommandText =
                "INSERT INTO schema_migrations (id, name, applied_at) VALUES ($id, $name, $at);";
            record.Parameters.AddWithValue("$id", migration.Id);
            record.Parameters.AddWithValue("$name", migration.Name);
            record.Parameters.AddWithValue("$at", StoreTimestamp.ToStored(Instant));
            record.ExecuteNonQuery();
        }
    }
}
