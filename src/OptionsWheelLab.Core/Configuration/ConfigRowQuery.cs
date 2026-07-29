using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The one SQL definition of resolving a key, shared by the two surfaces.
/// </summary>
/// <remarks>
/// Internal so neither surface can be bypassed by reaching this directly.
/// </remarks>
internal static class ConfigRowQuery
{
    /// <summary>
    /// The value of the highest version at or before <paramref name="upperBound"/>.
    /// </summary>
    internal static string? ResolveAtOrBefore(
        SqliteConnection connection,
        string key,
        string upperBound)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT value
            FROM config_rows
            WHERE key = $key AND set_at <= $upperBound
            ORDER BY version DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$upperBound", upperBound);

        return command.ExecuteScalar() as string;
    }

    /// <summary>The value of the highest version, with no bound.</summary>
    /// <remarks>
    /// <paramref name="transaction"/> is supplied when the read happens inside a
    /// write that has not committed, which is how the cross-key invariants see
    /// the rows they are about to guard [D-W23, D-W24]. Microsoft.Data.Sqlite
    /// refuses a command with no transaction while one is pending on the
    /// connection, so it cannot be left off there.
    /// </remarks>
    internal static string? ResolveCurrent(
        SqliteConnection connection,
        string key,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT value
            FROM config_rows
            WHERE key = $key
            ORDER BY version DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);

        return command.ExecuteScalar() as string;
    }
}
