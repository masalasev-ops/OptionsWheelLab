using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// Appends a new version of a configuration key.
/// </summary>
/// <remarks>
/// Worker-side: the Worker is the sole writer [D-W1]. A change never updates a
/// row, it inserts version + 1, which is what lets a later behaviour change be
/// explained after the fact.
/// <para>
/// Cross-key invariant enforcement on writes is owed by 0.8 [D-W23, D-W24].
/// Nothing is seeded until then, so there is no window in which an unguarded
/// write path could admit a violating version.
/// </para>
/// </remarks>
public sealed class ConfigWriter
{
    private readonly SqliteConnection _connection;

    public ConfigWriter(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Inserts the next version of <paramref name="key"/> and returns the
    /// version number written.
    /// </summary>
    /// <remarks>
    /// The version is computed inside the same statement and transaction as the
    /// insert, so two writers cannot both read the same maximum and produce one
    /// version. The primary key on (key, version) makes a collision fail rather
    /// than pass silently.
    /// <para>
    /// <paramref name="setAt"/> is a parameter, never a clock read. The clock
    /// abstraction lands at 0.5 and a <c>DateTime.UtcNow</c> here would be a
    /// call 0.5 has to remove.
    /// </para>
    /// </remarks>
    public int Append(string key, string value, DateTimeOffset setAt, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        using var transaction = _connection.BeginTransaction(deferred: false);

        RefuseIfEarlierThanNewest(transaction, key, setAt);

        using (var insert = _connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO config_rows (key, version, value, set_at, note)
                SELECT $key,
                       COALESCE(MAX(version), 0) + 1,
                       $value,
                       $setAt,
                       $note
                FROM config_rows
                WHERE key = $key;
                """;
            insert.Parameters.AddWithValue("$key", key);
            insert.Parameters.AddWithValue("$value", value);
            insert.Parameters.AddWithValue("$setAt", StoreTimestamp.ToStored(setAt));
            insert.Parameters.AddWithValue("$note", note ?? (object)DBNull.Value);
            insert.ExecuteNonQuery();
        }

        int version;

        using (var read = _connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT MAX(version) FROM config_rows WHERE key = $key;";
            read.Parameters.AddWithValue("$key", key);
            version = Convert.ToInt32(read.ExecuteScalar());
        }

        transaction.Commit();
        return version;
    }

    /// <summary>
    /// Refuses a version that predates the newest already stored for the key.
    /// </summary>
    /// <remarks>
    /// The store enforces this with a trigger, which holds against any writer.
    /// This check exists only so the refusal can name both instants: SQLite's
    /// <c>RAISE</c> takes a string literal and cannot interpolate the values
    /// that caused it. Inside the same transaction, so it cannot race the
    /// insert it guards.
    /// </remarks>
    private void RefuseIfEarlierThanNewest(
        SqliteTransaction transaction,
        string key,
        DateTimeOffset setAt)
    {
        using var newest = _connection.CreateCommand();
        newest.Transaction = transaction;
        newest.CommandText = "SELECT MAX(set_at) FROM config_rows WHERE key = $key;";
        newest.Parameters.AddWithValue("$key", key);

        if (newest.ExecuteScalar() is not string newestSetAt)
        {
            return;
        }

        var candidate = StoreTimestamp.ToStored(setAt);

        if (string.CompareOrdinal(candidate, newestSetAt) < 0)
        {
            throw new InvalidOperationException(
                $"Cannot append '{key}' at {candidate} because its newest version is already at "
                + $"{newestSetAt}. set_at moves forward for a key: resolution filters on set_at "
                + "and then orders by version, so an earlier timestamp would make the value in "
                + "force on a date depend on insertion order rather than on time, and the "
                + "append-only guards would make that permanent.");
        }
    }
}
