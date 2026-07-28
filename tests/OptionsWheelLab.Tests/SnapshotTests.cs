using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Snapshot behaviour. Not a registered fixture, so not named <c>FX-*</c>.
/// </summary>
public sealed class SnapshotTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 28, 9, 15, 30, 250, TimeSpan.Zero);

    /// <summary>
    /// The case the three-file copy exists for.
    /// </summary>
    /// <remarks>
    /// Automatic checkpointing is turned off and the connection is left open,
    /// because SQLite checkpoints and deletes the write-ahead log when the last
    /// connection closes cleanly. Without both, there is no <c>-wal</c> at
    /// snapshot time and a one-file copy would pass this test while losing data
    /// in the situation the snapshot is for.
    /// <para>
    /// The open connection holds no transaction, so it does not trip the
    /// exclusive lock. That is the real shape: a writer between transactions
    /// still has uncheckpointed frames on disk.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_snapshot_copies_the_wal_and_shm_alongside_the_database()
    {
        using var store = TempStore.Empty();

        using var connection = store.Connections.Open(StoreAccess.Write);

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                PRAGMA wal_autocheckpoint = 0;
                CREATE TABLE probe (value TEXT);
                INSERT INTO probe (value) VALUES ('uncheckpointed');
                """;
            command.ExecuteNonQuery();
        }

        Assert.True(
            new FileInfo(store.DatabasePath + "-wal").Length > 0,
            "the write-ahead log should hold uncheckpointed frames for this test to mean anything");

        var result = StoreSnapshot.Take(store.Location, Instant);

        Assert.True(result.Taken);

        var copied = Directory.GetFiles(result.Directory!)
            .Select(Path.GetFileName)
            .ToList();

        Assert.Contains(StoreLocation.DatabaseFileName, copied);
        Assert.Contains(StoreLocation.DatabaseFileName + "-wal", copied);

        Assert.True(
            new FileInfo(Path.Combine(result.Directory!, StoreLocation.DatabaseFileName + "-wal"))
                .Length > 0,
            "the copied write-ahead log should carry the frames the database file does not");
    }

    /// <summary>
    /// <c>-shm</c> is not copied, and that is deliberate. Holding the write
    /// lock across the copy byte-range locks it, and it is a transient
    /// wal-index SQLite rebuilds from the write-ahead log, so the snapshot
    /// restores identically without it.
    /// </summary>
    [Fact]
    public void The_snapshot_omits_the_shm_because_the_lock_makes_it_unreadable()
    {
        using var store = TempStore.Empty();

        using var connection = store.Connections.Open(StoreAccess.Write);

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "PRAGMA wal_autocheckpoint = 0; CREATE TABLE probe (value TEXT);";
            command.ExecuteNonQuery();
        }

        var result = StoreSnapshot.Take(store.Location, Instant);

        var copied = Directory.GetFiles(result.Directory!).Select(Path.GetFileName).ToList();

        Assert.DoesNotContain(StoreLocation.DatabaseFileName + "-shm", copied);
    }

    /// <summary>
    /// A reader is not refused. In WAL mode a reader cannot tear a file copy,
    /// so refusing one would be stricter than the problem requires and would
    /// make a snapshot impossible while the Api is merely running.
    /// </summary>
    [Fact]
    public void A_snapshot_is_allowed_while_a_reader_holds_the_store()
    {
        using var store = TempStore.Created();

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);

        using (var command = reader.CreateCommand())
        {
            command.CommandText = "SELECT 1;";
            command.ExecuteScalar();
        }

        var result = StoreSnapshot.Take(store.Location, Instant);

        Assert.True(result.Taken);
    }

    [Fact]
    public void The_snapshot_directory_is_named_for_the_instant_it_was_taken()
    {
        using var store = TempStore.Created();

        var result = StoreSnapshot.Take(store.Location, Instant);

        var name = Path.GetFileName(result.Directory!);
        var stamp = name[StoreSnapshot.DirectoryPrefix.Length..];

        Assert.StartsWith(StoreSnapshot.DirectoryPrefix, name, StringComparison.Ordinal);
        Assert.Equal(Instant, StoreTimestamp.ParseFileName(stamp));
    }

    /// <summary>
    /// One instant, two renderings. The filename form exists because the stored
    /// form contains colons, which are illegal in a Windows path.
    /// </summary>
    [Fact]
    public void A_snapshot_filename_round_trips_to_the_same_instant_as_the_stored_form()
    {
        var stored = StoreTimestamp.ToStored(Instant);
        var fileName = StoreTimestamp.ToFileName(Instant);

        Assert.Equal(StoreTimestamp.ParseStored(stored), StoreTimestamp.ParseFileName(fileName));
        Assert.DoesNotContain(':', fileName);
        Assert.Contains(':', stored);
    }

    [Fact]
    public void A_snapshot_of_a_store_that_does_not_exist_is_skipped_with_a_reason()
    {
        using var store = TempStore.Empty();

        var result = StoreSnapshot.Take(store.Location, Instant);

        Assert.False(result.Taken);
        Assert.NotNull(result.Reason);
        Assert.Empty(Directory.GetDirectories(store.Directory));
    }

    /// <summary>
    /// A torn three-file copy looks intact until someone restores from it, so
    /// the snapshot refuses loudly instead.
    /// </summary>
    [Fact]
    public void A_snapshot_is_refused_while_something_else_holds_the_database()
    {
        using var store = TempStore.Created();

        using var holder = store.Connections.Open(StoreAccess.Write);
        using var writing = holder.BeginTransaction();

        using (var command = holder.CreateCommand())
        {
            command.Transaction = writing;
            command.CommandText = "CREATE TABLE held (value TEXT);";
            command.ExecuteNonQuery();
        }

        var thrown = Assert.Throws<InvalidOperationException>(
            () => StoreSnapshot.Take(store.Location, Instant));

        Assert.Contains("Worker", thrown.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(store.Directory));
    }
}
