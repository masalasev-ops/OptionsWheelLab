using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Config writes are append-only and versioned.
/// </summary>
/// <remarks>
/// Not a registered fixture, so deliberately not named <c>FX-*</c>:
/// FX-RegistryMatchesDisk requires every <c>FX-*.cs</c> to have a row in
/// <c>FIXTURES.md</c>.
/// </remarks>
public sealed class ConfigWriteTests
{
    private const string Key = "Trial:MaxRolls";

    private static readonly DateTimeOffset SetAt =
        new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_inserts_for_one_key_produce_versions_one_and_two()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);

        Assert.Equal(1, writer.Append(Key, "2", SetAt));
        Assert.Equal(2, writer.Append(Key, "3", SetAt.AddDays(1)));
    }

    [Fact]
    public void Versions_are_counted_per_key_not_across_the_table()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.Append(Key, "2", SetAt);
        writer.Append(Key, "3", SetAt);

        Assert.Equal(1, writer.Append("Trial:MaxTrialDays", "120", SetAt));
    }

    [Fact]
    public void An_update_against_config_rows_fails()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(Key, "2", SetAt);

        var thrown = Assert.Throws<SqliteException>(
            () => Execute(connection, "UPDATE config_rows SET value = '99' WHERE key = 'Trial:MaxRolls';"));

        Assert.Contains("append-only", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_delete_against_config_rows_fails()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(Key, "2", SetAt);

        var thrown = Assert.Throws<SqliteException>(
            () => Execute(connection, "DELETE FROM config_rows WHERE key = 'Trial:MaxRolls';"));

        Assert.Contains("append-only", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_earlier_version_stays_readable_after_a_revision()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.Append(Key, "2", new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        writer.Append(Key, "3", new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        var configuration = new AsOfConfiguration(connection);

        Assert.Equal("2", configuration.Resolve(Key, new DateOnly(2026, 2, 1)));
        Assert.Equal("3", configuration.Resolve(Key, new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void Set_at_is_stored_in_the_pinned_form()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(Key, "2", SetAt);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_at FROM config_rows WHERE key = $key;";
        command.Parameters.AddWithValue("$key", Key);

        Assert.Equal(StoreTimestamp.ToStored(SetAt), command.ExecuteScalar());
    }

    /// <summary>
    /// Resolution filters on <c>set_at</c> and then orders by version, so an
    /// out-of-order timestamp would make the value in force on a date depend on
    /// insertion order rather than on time, and the append-only guards would
    /// make that permanent.
    /// </summary>
    [Fact]
    public void A_version_earlier_than_the_newest_for_that_key_is_refused_naming_both_instants()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        var newest = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var earlier = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

        writer.Append(Key, "3", newest);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => writer.Append(Key, "2", earlier));

        Assert.Contains(StoreTimestamp.ToStored(earlier), thrown.Message, StringComparison.Ordinal);
        Assert.Contains(StoreTimestamp.ToStored(newest), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The store refuses it too, so the guard holds against any writer and not
    /// only against <see cref="ConfigWriter"/>.
    /// </summary>
    [Fact]
    public void The_store_itself_refuses_an_earlier_set_at()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            Key,
            "3",
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        var thrown = Assert.Throws<SqliteException>(() => Execute(
            connection,
            $"""
             INSERT INTO config_rows (key, version, value, set_at, note)
             VALUES ('{Key}', 99, '2', '2026-01-10T12:00:00.000Z', NULL);
             """));

        Assert.Contains("moves forward", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Two versions of one key can legitimately share an instant, and version
    /// breaks the tie, which is what as-of resolution already does.
    /// </summary>
    [Fact]
    public void An_equal_set_at_is_accepted_and_resolves_by_version()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);

        Assert.Equal(1, writer.Append(Key, "first", SetAt));
        Assert.Equal(2, writer.Append(Key, "second", SetAt));

        var resolved = new AsOfConfiguration(connection)
            .Resolve(Key, DateOnly.FromDateTime(SetAt.UtcDateTime));

        Assert.Equal("second", resolved);
    }

    /// <summary>
    /// The constraint is per key, because keys are versioned independently.
    /// </summary>
    [Fact]
    public void An_earlier_set_at_on_a_different_key_is_unaffected()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.Append(Key, "3", new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

        var version = writer.Append(
            "Trial:MaxTrialDays",
            "120",
            new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, version);
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
        new MigrationRunner(store.Connections).Run(SetAt);
        return store;
    }
}
