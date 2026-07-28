using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ApiCannotWrite: the Api connection is read-only.
/// </summary>
/// <remarks>
/// The Worker is the sole writer [D-W1]. Read-only is set on the connection
/// rather than left to callers to respect.
/// </remarks>
public sealed class FX_ApiCannotWrite
{
    [Fact]
    public void A_write_through_the_read_only_connection_throws()
    {
        using var store = TempStore.Created();

        using (var writer = store.Connections.Open(StoreAccess.Write))
        {
            Execute(writer, "CREATE TABLE probe (value TEXT);");
        }

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);

        var thrown = Assert.Throws<SqliteException>(
            () => Execute(reader, "INSERT INTO probe (value) VALUES ('x');"));

        Assert.Contains("readonly", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_read_only_connection_can_still_read()
    {
        using var store = TempStore.Created();

        using (var writer = store.Connections.Open(StoreAccess.Write))
        {
            Execute(writer, "CREATE TABLE probe (value TEXT);");
            Execute(writer, "INSERT INTO probe (value) VALUES ('written');");
        }

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);
        using var command = reader.CreateCommand();
        command.CommandText = "SELECT value FROM probe;";

        Assert.Equal("written", command.ExecuteScalar());
    }

    /// <summary>
    /// The finding reported with this checkpoint, pinned as a test so the
    /// behaviour is recorded rather than remembered: a read-only connection
    /// cannot open a store that has never been migrated.
    /// </summary>
    [Fact]
    public void A_read_only_connection_to_a_store_that_does_not_exist_throws()
    {
        using var store = TempStore.Empty();

        Assert.False(File.Exists(store.DatabasePath));
        Assert.Throws<SqliteException>(() => store.Connections.Open(StoreAccess.ReadOnly));
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
