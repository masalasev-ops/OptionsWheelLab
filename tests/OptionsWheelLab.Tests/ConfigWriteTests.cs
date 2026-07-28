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
