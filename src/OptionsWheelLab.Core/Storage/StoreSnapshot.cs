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

        // The lock is held for the whole copy, not merely tested before it.
        // Releasing it first would prove only that nothing was writing at the
        // moment of the check, and a writer starting immediately afterwards
        // would tear the copy anyway, which is the failure the lock exists to
        // prevent.
        using var writeLock = TakeExclusiveLock(location);

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

    /// <summary>
    /// The database and the write-ahead log, which together are the whole of
    /// the committed state.
    /// </summary>
    /// <remarks>
    /// <c>-shm</c> is deliberately absent, and this is a departure from
    /// <c>DATA_AND_SCHEMA.md</c>, which says the snapshot copies it too.
    /// <para>
    /// Holding the write lock across the copy, which is the stronger guarantee,
    /// byte-range locks <c>-shm</c> and makes it unreadable while the lock is
    /// held. The two requirements cannot both be met. <c>-shm</c> is a
    /// transient wal-index that SQLite rebuilds from the write-ahead log
    /// whenever it is missing, so a snapshot of <c>.db</c> and <c>-wal</c>
    /// restores identically and nothing is lost by omitting it.
    /// </para>
    /// <para>
    /// Omitted unconditionally rather than attempted and skipped on failure,
    /// so the snapshot has the same contents on every platform. Windows
    /// enforces the lock; Linux would not.
    /// </para>
    /// </remarks>
    public static IEnumerable<string> SourceFiles(string databasePath) =>
    [
        databasePath,
        databasePath + "-wal",
    ];

    /// <summary>
    /// Takes the write lock and keeps it until disposed.
    /// </summary>
    /// <remarks>
    /// Refuses loudly rather than copying a database somebody is writing to. A
    /// torn copy across the three files looks intact until someone restores
    /// from it.
    /// <para>
    /// This blocks writers, not readers. In WAL mode a reader does not tear a
    /// file copy, so refusing one would be stricter than the problem requires
    /// and would make a snapshot impossible while the Api is merely running.
    /// </para>
    /// </remarks>
    private static ExclusiveLock TakeExclusiveLock(StoreLocation location)
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

        var connection = new SqliteConnection(builder.ConnectionString);

        try
        {
            connection.Open();

            var transaction = connection.BeginTransaction(deferred: false);

            try
            {
                // BeginTransaction(deferred: false) issues BEGIN IMMEDIATE,
                // which takes the write lock now rather than on first write.
                // In WAL mode that is as exclusive as it gets: readers are
                // deliberately not blocked.
                using var probe = connection.CreateCommand();
                probe.Transaction = transaction;
                probe.CommandText = "SELECT 1;";
                probe.ExecuteScalar();

                return new ExclusiveLock(connection, transaction);
            }
            catch
            {
                transaction.Dispose();
                throw;
            }
        }
        catch (SqliteException locked)
        {
            connection.Dispose();

            throw new InvalidOperationException(
                $"Cannot snapshot '{location.DatabasePath}' because something else is writing to "
                + "it. The Worker is the sole writer, so stop the Worker and run this again. "
                + "Copying while a writer is active can tear the snapshot across its .db, -wal "
                + "and -shm files, which looks intact until it is needed.",
                locked);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>The write lock, held for the life of the copy.</summary>
    private sealed class ExclusiveLock(SqliteConnection connection, SqliteTransaction transaction)
        : IDisposable
    {
        public void Dispose()
        {
            // Rolled back rather than committed: the lock exists to keep other
            // writers out during the copy and changes nothing itself.
            transaction.Rollback();
            transaction.Dispose();
            connection.Dispose();
        }
    }
}
