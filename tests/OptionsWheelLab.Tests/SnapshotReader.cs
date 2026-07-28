using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Opens a snapshot file directly, read-only.
/// </summary>
/// <remarks>
/// A snapshot is a database in its own right, not a copy that has to be
/// restored before it can be inspected, which is one of the things
/// <c>VACUUM INTO</c> buys [D-W28].
/// </remarks>
internal static class SnapshotReader
{
    internal static SqliteConnection Open(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }
}
