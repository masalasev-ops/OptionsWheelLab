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
}
