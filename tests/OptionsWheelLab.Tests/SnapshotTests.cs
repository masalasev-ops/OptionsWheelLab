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
    /// One file, not a set whose members can disagree [D-W28].
    /// </summary>
    [Fact]
    public void A_snapshot_is_a_single_file_beside_the_store()
    {
        using var store = TempStore.Created();

        var result = StoreSnapshot.Take(store.Location, Instant);

        Assert.True(result.Taken);
        Assert.True(File.Exists(result.Path!));
        Assert.Equal(store.Directory, Path.GetDirectoryName(result.Path!));
    }

    [Fact]
    public void The_snapshot_is_named_for_the_instant_it_was_taken()
    {
        using var store = TempStore.Created();

        var result = StoreSnapshot.Take(store.Location, Instant);

        var name = Path.GetFileNameWithoutExtension(result.Path!);
        var stamp = name[StoreSnapshot.FileNamePrefix.Length..];

        Assert.StartsWith(StoreSnapshot.FileNamePrefix, name, StringComparison.Ordinal);
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

    /// <summary>
    /// The base case, not an exception: the first run has no store yet.
    /// </summary>
    [Fact]
    public void A_snapshot_of_a_store_that_does_not_exist_is_skipped_with_a_reason()
    {
        using var store = TempStore.Empty();

        var result = StoreSnapshot.Take(store.Location, Instant);

        Assert.False(result.Taken);
        Assert.NotNull(result.Reason);
        Assert.Empty(Directory.GetFiles(store.Directory));
    }

    /// <summary>
    /// A snapshot carries what has not yet checkpointed into the database file,
    /// which is why the mechanism has to be more than copying the `.db`.
    /// </summary>
    [Fact]
    public void A_snapshot_carries_state_that_has_not_yet_checkpointed()
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

        Assert.Equal("uncheckpointed", ReadProbe(result.Path!));
    }

    /// <summary>
    /// No lock, so a reader cannot prevent a snapshot.
    /// </summary>
    [Fact]
    public void A_snapshot_succeeds_while_a_reader_holds_the_store()
    {
        using var store = TempStore.Created();

        using var reader = store.Connections.Open(StoreAccess.ReadOnly);

        using (var command = reader.CreateCommand())
        {
            command.CommandText = "SELECT 1;";
            command.ExecuteScalar();
        }

        Assert.True(StoreSnapshot.Take(store.Location, Instant).Taken);
    }

    /// <summary>
    /// No lock, so a writer cannot prevent a snapshot either, and the snapshot
    /// sees committed state only.
    /// </summary>
    [Fact]
    public void A_snapshot_while_a_writer_holds_the_store_captures_committed_state_only()
    {
        using var store = TempStore.Empty();

        using var writer = store.Connections.Open(StoreAccess.Write);

        using (var setup = writer.CreateCommand())
        {
            setup.CommandText =
                """
                CREATE TABLE probe (value TEXT);
                INSERT INTO probe (value) VALUES ('committed');
                """;
            setup.ExecuteNonQuery();
        }

        using var uncommitted = writer.BeginTransaction();

        using (var command = writer.CreateCommand())
        {
            command.Transaction = uncommitted;
            command.CommandText = "UPDATE probe SET value = 'uncommitted';";
            command.ExecuteNonQuery();
        }

        var result = StoreSnapshot.Take(store.Location, Instant);

        Assert.True(result.Taken);
        Assert.Equal("committed", ReadProbe(result.Path!));

        uncommitted.Rollback();
    }

    private static string? ReadProbe(string databasePath)
    {
        using var connection = SnapshotReader.Open(databasePath);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM probe;";
        return command.ExecuteScalar() as string;
    }
}
