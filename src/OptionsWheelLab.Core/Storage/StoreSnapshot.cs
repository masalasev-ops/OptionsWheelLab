using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Storage;

/// <summary>What a snapshot attempt did.</summary>
/// <param name="Taken">False when there was no database file to snapshot.</param>
/// <param name="Path">The snapshot file, when one was taken.</param>
/// <param name="Reason">Why it was skipped, when it was.</param>
public sealed record SnapshotResult(bool Taken, string? Path, string? Reason)
{
    public static SnapshotResult Skipped(string reason) => new(false, null, reason);

    public static SnapshotResult Of(string path) => new(true, path, null);
}

/// <summary>
/// Writes a timestamped snapshot of the store beside it.
/// </summary>
/// <remarks>
/// Taken with <c>VACUUM INTO</c> [D-W28]. That runs in a read transaction, so it
/// is atomic, blocks no writer, needs no lock, and produces one file from the
/// committed state including whatever has not yet checkpointed into the
/// database.
/// <para>
/// The result is a defragmented rebuild rather than a byte-identical copy, so a
/// snapshot cannot be compared to its source by hash. A rollback artefact needs
/// logical identity rather than byte identity, and nothing in this corpus asks
/// for the latter.
/// </para>
/// </remarks>
public static class StoreSnapshot
{
    public const string FileNamePrefix = "snapshot-";

    public const string FileNameExtension = ".db";

    /// <summary>
    /// Writes a snapshot beside the store, named for the instant it was taken.
    /// </summary>
    public static SnapshotResult Take(StoreLocation location, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(location);

        // The base case, not an exception: the first run has no store yet.
        if (!File.Exists(location.DatabasePath))
        {
            return SnapshotResult.Skipped(
                "no database file exists yet, so there is nothing to snapshot");
        }

        var target = PathFor(location, instant);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = location.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        };

        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $target;";
        command.Parameters.AddWithValue("$target", target);
        command.ExecuteNonQuery();

        return SnapshotResult.Of(target);
    }

    /// <summary>Where a snapshot taken at <paramref name="instant"/> is written.</summary>
    public static string PathFor(StoreLocation location, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(location);

        return System.IO.Path.Combine(
            location.Directory,
            FileNamePrefix + StoreTimestamp.ToFileName(instant) + FileNameExtension);
    }
}
