using System.Reflection;
using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Migration 5's rebuild holds the bars the record can express, and the
/// schema's nullability follows the record as a standing assertion.
/// </summary>
/// <remarks>
/// Not a registered fixture, for the same reason as
/// <see cref="MembershipStoreTests"/>.
/// <para>
/// The record-to-schema test is what makes "enumerated from the record" a
/// property rather than authoring-time care: the 1.2 finding named four
/// columns and <see cref="UnderlyingBar"/> makes five optional, which is
/// exactly the drift a sentence-sourced migration inherits. If the record
/// gains or loses an optional field, this fails and names the migration owed.
/// </para>
/// </remarks>
public sealed class BarsSchemaTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Property-to-column map for <see cref="UnderlyingBar"/>'s stored
    /// counterpart. <c>observed_at</c> has no property: the record mirrors the
    /// table minus the stamp, which ingest supplies.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ColumnOf =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Symbol"] = "symbol",
            ["SessionDate"] = "session_date",
            ["Open"] = "open",
            ["High"] = "high",
            ["Low"] = "low",
            ["Close"] = "close",
            ["AdjustedClose"] = "adj_close",
            ["Volume"] = "volume",
        };

    [Fact]
    public void The_tables_nullability_matches_the_record()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var notNullByColumn = TableNullability(connection);

        foreach (var property in typeof(UnderlyingBar).GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            var column = ColumnOf[property.Name];
            var optional = Nullable.GetUnderlyingType(property.PropertyType) is not null;

            Assert.True(
                notNullByColumn.TryGetValue(column, out var notNull),
                $"UnderlyingBar.{property.Name} maps to column '{column}', which the table "
                + "does not have.");
            Assert.True(
                optional != notNull,
                $"UnderlyingBar.{property.Name} is {(optional ? "optional" : "required")} but "
                + $"column '{column}' is {(notNull ? "NOT NULL" : "nullable")}. Nullability "
                + "follows what a chain can express, so the migration is owed, not the record.");
        }

        Assert.True(
            notNullByColumn["observed_at"],
            "observed_at is the stamp ingest supplies and stays NOT NULL.");
    }

    /// <summary>
    /// The map itself is guarded: a record property the map does not name
    /// would silently escape the assertion above.
    /// </summary>
    [Fact]
    public void Every_record_property_is_in_the_map()
    {
        var properties = typeof(UnderlyingBar)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(ColumnOf.Keys.OrderBy(name => name, StringComparer.Ordinal), properties);
    }

    [Fact]
    public void A_bar_with_only_a_close_inserts()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO underlying_bars (symbol, session_date, close, observed_at)
            VALUES ('WDGT', '2026-03-02', '52.40000000', '2026-07-30T09:00:00.000Z');
            """;
        insert.ExecuteNonQuery();

        Assert.Equal(1L, CountBars(connection));
    }

    /// <summary>
    /// The refusals survived the rebuild, asserted against a seeded row
    /// because a trigger is per row. DROP TABLE took the old triggers with it;
    /// a forgotten recreation passes every schema check and fails exactly
    /// here.
    /// </summary>
    [Fact]
    public void An_update_is_still_refused_after_the_rebuild()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneBar(connection);
        Assert.Equal(1L, CountBars(connection));

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE underlying_bars SET close = close;";

        var refusal = Assert.Throws<SqliteException>(() => update.ExecuteNonQuery());
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_delete_is_still_refused_after_the_rebuild()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        SeedOneBar(connection);
        Assert.Equal(1L, CountBars(connection));

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM underlying_bars;";

        var refusal = Assert.Throws<SqliteException>(() => delete.ExecuteNonQuery());
        Assert.Contains("append-only", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The INSERT ... SELECT is not dead weight: a store populated at schema 4
    /// carries its rows through the rebuild.
    /// </summary>
    [Fact]
    public void A_hand_populated_store_survives_the_rebuild()
    {
        using var store = TempStore.Empty();

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            ApplyAllBut(connection, last: 1);
            SeedOneBar(connection, close: "51.10000000");
        }

        new MigrationRunner(store.Connections).Run(Instant.AddMinutes(1));

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);
        using var read = reader.CreateCommand();
        read.CommandText = "SELECT close FROM underlying_bars;";

        Assert.Equal("51.10000000", read.ExecuteScalar());
    }

    private static void SeedOneBar(SqliteConnection connection, string close = "52.40000000")
    {
        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO underlying_bars
                (symbol, session_date, open, high, low, close, adj_close, volume, observed_at)
            VALUES ('WDGT', '2026-03-02', '52.00000000', '52.90000000', '51.80000000', $close,
                    $close, 1200000, '2026-07-30T09:00:00.000Z');
            """;
        insert.Parameters.AddWithValue("$close", close);
        insert.ExecuteNonQuery();
    }

    private static long CountBars(SqliteConnection connection)
    {
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM underlying_bars;";
        return (long)count.ExecuteScalar()!;
    }

    private static IReadOnlyDictionary<string, bool> TableNullability(SqliteConnection connection)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "SELECT name, [notnull] FROM pragma_table_info('underlying_bars');";

        var notNullByColumn = new Dictionary<string, bool>(StringComparer.Ordinal);
        using var reader = pragma.ExecuteReader();

        while (reader.Read())
        {
            notNullByColumn[reader.GetString(0)] = reader.GetInt64(1) != 0;
        }

        return notNullByColumn;
    }

    /// <summary>
    /// A store stopped <paramref name="last"/> migrations short of current,
    /// built from the frozen migration list itself.
    /// </summary>
    private static void ApplyAllBut(SqliteConnection connection, int last)
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

        foreach (var migration in Migrations.All.SkipLast(last))
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

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);
        return store;
    }
}
