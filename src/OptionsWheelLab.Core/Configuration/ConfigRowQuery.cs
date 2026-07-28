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
    internal static string? ResolveCurrent(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
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
