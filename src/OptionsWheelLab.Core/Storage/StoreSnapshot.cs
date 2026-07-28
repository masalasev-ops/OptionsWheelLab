using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Storage;

/// <summary>What a snapshot attempt did.</summary>
/// <param name="Taken">False when there was no database file to copy.</param>
/// <param name="Directory">The snapshot directory, when one was taken.</param>
/// <param name="Reason">Why it was skipped, when it was.</param>
public sealed record SnapshotResult(bool Taken, string? Directory, string? Reason)
{
    public static SnapshotResult Skipped(string reason) => new(false, null, reason);

    public static SnapshotResult Of(string directory) => new(true, directory, null);
}

/// <summary>
/// Copies the store beside itself, timestamped.
/// </summary>
/// <remarks>
/// The copy takes the <c>.db</c> and its <c>-wal</c> and <c>-shm</c> files. The
/// database alone would lose whatever has not yet checkpointed, which is the
/// failure that makes a snapshot worthless exactly when it is needed.
/// <para>
/// An exclusive lock is taken first. Sole-writer [D-W1] makes the precondition
/// satisfiable but does not enforce it, and a torn three-file copy looks intact
/// until someone tries to restore from it.
/// </para>
/// </remarks>
public static class StoreSnapshot
{
    public const string DirectoryPrefix = "snapshot-";

    /// <summary>
    /// Copies the store into a timestamped directory beside it.
    /// </summary>
    /// <remarks>
    /// Call with no connection open to the store. The lock cannot tell the
    /// caller's own connection from another process's, so a snapshot attempted
    /// while the caller holds one would be refused with a message naming the
    /// Worker when nothing else is running.
    /// </remarks>
    public static SnapshotResult Take(StoreLocation location, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!File.Exists(location.DatabasePath))
        {
            return SnapshotResult.Skipped(
                "no database file exists yet, so there is nothing to snapshot");
        }

        AssertNothingElseHasItOpen(location);

        var directory = Path.Combine(
            location.Directory,
            DirectoryPrefix + StoreTimestamp.ToFileName(instant));

        Directory.CreateDirectory(directory);

        foreach (var source in SourceFiles(location.DatabasePath))
        {
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(directory, Path.GetFileName(source)), overwrite: false);
            }
        }

        return SnapshotResult.Of(directory);
    }

    /// <summary>The database and the two files that carry uncheckpointed state.</summary>
    public static IEnumerable<string> SourceFiles(string databasePath) =>
    [
        databasePath,
        databasePath + "-wal",
        databasePath + "-shm",
    ];

    /// <summary>
    /// Refuses loudly rather than copying a database somebody is writing.
    /// </summary>
    private static void AssertNothingElseHasItOpen(StoreLocation location)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = location.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,

            // Refuse quickly. The default waits thirty seconds before giving
            // up, which turns a clear "the Worker is running" into a hang.
            DefaultTimeout = 1,
        };

        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "BEGIN EXCLUSIVE; COMMIT;";
            command.ExecuteNonQuery();
        }
        catch (SqliteException locked)
        {
            throw new InvalidOperationException(
                $"Cannot snapshot '{location.DatabasePath}' because something else has it open. "
                + "The Worker is the sole writer, so stop the Worker and run this again. "
                + "Copying while a writer is active can tear the snapshot across its .db, -wal "
                + "and -shm files, which looks intact until it is needed.",
                locked);
        }
    }
}
