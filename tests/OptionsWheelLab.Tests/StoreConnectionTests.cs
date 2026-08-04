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

    /// <summary>
    /// Foreign keys are enforced on a connection this store opens.
    /// </summary>
    /// <remarks>
    /// <b>Pinned because it was a claim in a comment and is now a fact the suite
    /// holds.</b> <see cref="MarketData.ChainWriter"/> states that
    /// Microsoft.Data.Sqlite enables foreign keys where a bare sqlite3 prompt does
    /// not, and orders its inserts accordingly, so that ordering is load-bearing
    /// under the application and unchecked outside it. Nothing asserted it until
    /// now.
    /// <para>
    /// <b>Read through the real factory, and that is the whole method.</b> A probe
    /// opening its own connection measures the probe: one written for this
    /// question set the pragma before reading it and reported zero, which is the
    /// value it had just written rather than the value the store runs under.
    /// </para>
    /// <para>
    /// What rests on it. Every <c>REFERENCES</c> in the schema enforces rather than
    /// documents, including the two in migration 1 and the composite pair
    /// [DATA_AND_SCHEMA §4.3] uses to keep a decision and a candidate on one
    /// feasible set. Migration 8 carrying no foreign keys is a choice made against
    /// enforcement that works, not around enforcement that is absent.
    /// </para>
    /// </remarks>
    [Fact]
    public void Foreign_keys_are_enforced_on_a_connection_this_store_opens()
    {
        using var store = TempStore.Created();
        using var connection = store.Connections.Open(StoreAccess.Write);

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys;";

        Assert.Equal(1L, pragma.ExecuteScalar());
    }
}
