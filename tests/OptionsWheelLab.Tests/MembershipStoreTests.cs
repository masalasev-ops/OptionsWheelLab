using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Migration 4 creates the membership record and the store refuses what the
/// source detector merely detects.
/// </summary>
/// <remarks>
/// Not a registered fixture: the one check registered against 1.3 is
/// FX-PitMembershipExcludesLaterJoiner, and these land unregistered the way
/// <c>ConfigWriteTests</c> does.
/// <para>
/// The upgrade test is the first from-previous-schema migration test in the
/// suite. FX-MigrateFromEmpty covers the empty and the nothing-pending states;
/// nothing before 1.3 exercised a store part-way through the migration list,
/// because until now every store in the tree was either empty or current.
/// </para>
/// </remarks>
public sealed class MembershipStoreTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Instant.AddDays(2);

    [Fact]
    public void Migrating_a_previous_schema_store_applies_only_the_new_migration()
    {
        using var store = StoreAtPreviousSchema();

        var result = new MigrationRunner(store.Connections).Run(Later);

        var applied = Assert.Single(result.Applied);
        Assert.Equal(Migrations.All[^1].Id, applied.Id);
        Assert.Equal(Migrations.All[^1].Id, result.SchemaVersion);
    }

    [Fact]
    public void Migrating_a_previous_schema_store_snapshots_first()
    {
        using var store = StoreAtPreviousSchema();

        var result = new MigrationRunner(store.Connections).Run(Later);

        Assert.True(result.Snapshot.Taken);
        Assert.True(File.Exists(result.Snapshot.Path!));
    }

    [Fact]
    public void Watchlist_membership_exists_after_migrating_from_empty()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'watchlist_membership';";

        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    /// <summary>
    /// The refusal is asserted against a seeded table, because a trigger is per
    /// row and an empty table fires nothing.
    /// </summary>
    [Fact]
    public void An_update_is_refused_by_the_store()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneTransition(connection);
        Assert.Equal(1L, CountRows(connection));

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE watchlist_membership SET reason = 'edited';";

        var refusal = Assert.Throws<SqliteException>(() => update.ExecuteNonQuery());
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("transition", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_delete_is_refused_by_the_store()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneTransition(connection);
        Assert.Equal(1L, CountRows(connection));

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM watchlist_membership;";

        var refusal = Assert.Throws<SqliteException>(() => delete.ExecuteNonQuery());
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stored form the database enforces, the same shape `right` has.
    /// </summary>
    [Fact]
    public void A_kind_outside_the_vocabulary_is_refused_by_the_store()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var refusal = Assert.Throws<SqliteException>(
            () => Insert(connection, version: 1, kind: "member", observedAt: Instant));

        Assert.Contains("CHECK", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Version ordering is no substitute for the monotonic stamp: an append
    /// carrying an earlier stamp would change what was believed at a past
    /// instant, after the fact.
    /// </summary>
    [Fact]
    public void A_backdated_observed_at_is_refused_by_the_store()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Insert(connection, version: 1, kind: "joined", observedAt: Later);
        Assert.Equal(1L, CountRows(connection));

        var refusal = Assert.Throws<SqliteException>(
            () => Insert(connection, version: 2, kind: "left", observedAt: Instant));

        Assert.Contains("moves forward", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Equal is allowed: two transitions can share an instant, and version
    /// breaks the tie.
    /// </summary>
    [Fact]
    public void An_equal_observed_at_is_allowed()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Insert(connection, version: 1, kind: "joined", observedAt: Instant);
        Insert(connection, version: 2, kind: "left", observedAt: Instant);

        Assert.Equal(2L, CountRows(connection));
    }

    /// <summary>
    /// The stamp is monotonic per symbol, not per table, because symbols are
    /// versioned independently.
    /// </summary>
    [Fact]
    public void A_different_symbol_is_not_bound_by_another_symbols_stamp()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Insert(connection, version: 1, kind: "joined", observedAt: Later);
        Insert(connection, version: 1, kind: "joined", observedAt: Instant, symbol: "OTHR");

        Assert.Equal(2L, CountRows(connection));
    }

    private static void SeedOneTransition(SqliteConnection connection) =>
        Insert(connection, version: 1, kind: "joined", observedAt: Instant);

    private static void Insert(
        SqliteConnection connection,
        long version,
        string kind,
        DateTimeOffset observedAt,
        string symbol = "WDGT")
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO watchlist_membership
                (symbol, version, effective_on, kind, reason, observed_at)
            VALUES ($symbol, $version, '2026-03-01', $kind, NULL, $observed);
            """;
        command.Parameters.AddWithValue("$symbol", symbol);
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$observed", StoreTimestamp.ToStored(observedAt));
        command.ExecuteNonQuery();
    }

    private static long CountRows(SqliteConnection connection)
    {
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM watchlist_membership;";
        return (long)count.ExecuteScalar()!;
    }

    /// <summary>
    /// A store with every migration but the newest applied, built from the
    /// frozen migration list itself rather than from transcribed DDL.
    /// </summary>
    private static TempStore StoreAtPreviousSchema()
    {
        var store = TempStore.Empty();
        using var connection = store.Connections.Open(StoreAccess.Write);

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

        foreach (var migration in Migrations.All.SkipLast(1))
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

        return store;
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);
        return store;
    }
}
