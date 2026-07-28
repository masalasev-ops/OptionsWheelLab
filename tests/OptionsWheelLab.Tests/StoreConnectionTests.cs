using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Journal mode. Not a registered fixture, so not named <c>FX-*</c>.
/// </summary>
public sealed class StoreConnectionTests
{
    /// <summary>
    /// Without WAL the snapshot definition of done would demonstrate copying a
    /// write-ahead log that does not exist.
    /// </summary>
    [Fact]
    public void An_opened_store_reports_wal()
    {
        using var store = TempStore.Empty();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Equal("wal", StoreConnectionFactory.JournalModeOf(connection), ignoreCase: true);
    }

    [Fact]
    public void Wal_persists_with_the_database_across_connections()
    {
        using var store = TempStore.Created();

        using var reopened = store.Connections.Open(StoreAccess.ReadOnly);

        Assert.Equal("wal", StoreConnectionFactory.JournalModeOf(reopened), ignoreCase: true);
    }
}
