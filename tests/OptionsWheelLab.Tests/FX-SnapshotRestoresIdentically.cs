using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-SnapshotRestoresIdentically: a store restored from its snapshot resolves
/// the values it did before the mutation.
/// </summary>
/// <remarks>
/// This is what a snapshot is for, and the property `VACUUM INTO` has to
/// deliver [D-W28]. The snapshot is a defragmented rebuild rather than a
/// byte-identical copy, so identity is asserted on what the store resolves, not
/// on its bytes. A rollback artefact needs logical identity.
/// </remarks>
public sealed class FX_SnapshotRestoresIdentically
{
    private const string Key = "Gate:MaxDelta";

    private static readonly DateTimeOffset Instant =
        new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly AsOf = new(2026, 6, 1);

    [Fact]
    public void A_store_restored_from_its_snapshot_resolves_what_it_did_before_the_mutation()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);

        // The state worth getting back.
        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(connection).Append(Key, "0.35", Instant);
        }

        var snapshot = StoreSnapshot.Take(store.Location, Instant);
        Assert.True(snapshot.Taken);

        // The change being rolled back.
        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(connection).Append(Key, "0.99", Instant.AddDays(1));

            Assert.Equal("0.99", new AsOfConfiguration(connection).Resolve(Key, AsOf));
        }

        Restore(store, snapshot.Path!);

        using (var connection = store.Connections.Open(StoreAccess.ReadOnly))
        {
            Assert.Equal("0.35", new AsOfConfiguration(connection).Resolve(Key, AsOf));
        }
    }

    [Fact]
    public void The_snapshot_is_itself_a_readable_store()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(connection).Append(Key, "0.35", Instant);
        }

        var snapshot = StoreSnapshot.Take(store.Location, Instant);

        using var reader = SnapshotReader.Open(snapshot.Path!);

        Assert.Equal("0.35", new AsOfConfiguration(reader).Resolve(Key, AsOf));
    }

    /// <summary>
    /// The append-only triggers survive the rebuild, so a restored store is
    /// still guarded rather than merely holding the same rows.
    /// </summary>
    [Fact]
    public void A_restored_store_keeps_its_append_only_guards()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(connection).Append(Key, "0.35", Instant);
        }

        var snapshot = StoreSnapshot.Take(store.Location, Instant);
        Restore(store, snapshot.Path!);

        using var restored = store.Connections.Open(StoreAccess.Write);
        using var command = restored.CreateCommand();
        command.CommandText = "DELETE FROM config_rows;";

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => command.ExecuteNonQuery());
    }

    /// <summary>
    /// Puts the snapshot back in place of the store. The write-ahead log and
    /// its index belong to the replaced database and are removed with it.
    /// </summary>
    private static void Restore(TempStore store, string snapshotPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        File.Delete(store.DatabasePath);
        File.Delete(store.DatabasePath + "-wal");
        File.Delete(store.DatabasePath + "-shm");

        File.Copy(snapshotPath, store.DatabasePath);
    }
}
