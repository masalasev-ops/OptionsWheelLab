using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Storage;

/// <summary>How a connection may treat the store.</summary>
public enum StoreAccess
{
    /// <summary>The Worker's mode. The Worker is the sole writer [D-W1].</summary>
    Write,

    /// <summary>The Api's mode.</summary>
    ReadOnly,
}

/// <summary>
/// Opens connections to the store in one of two modes.
/// </summary>
/// <remarks>
/// Read-only is <see cref="SqliteOpenMode.ReadOnly"/> on the connection string,
/// so the mode is a property of the connection rather than a convention a caller
/// can forget.
/// <para>
/// Read-only is a guarantee about what the connection may do to the data, not
/// about what the process needs from the filesystem. A read-only connection to a
/// WAL database still needs write access to the <c>-shm</c> file, so "the Api
/// opens read-only" is not a claim of filesystem isolation.
/// </para>
/// </remarks>
public sealed class StoreConnectionFactory
{
    private readonly StoreLocation _location;

    public StoreConnectionFactory(StoreLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        _location = location;
    }

    public StoreLocation Location => _location;

    /// <summary>
    /// Opens a connection. A write connection creates the file if absent and
    /// puts the database in WAL journal mode; a read-only connection requires
    /// the file to exist already.
    /// </summary>
    public SqliteConnection Open(StoreAccess access)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _location.DatabasePath,
            Mode = access == StoreAccess.Write
                ? SqliteOpenMode.ReadWriteCreate
                : SqliteOpenMode.ReadOnly,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        if (access == StoreAccess.Write)
        {
            // WAL is persisted with the database, so this is a no-op after the
            // first time. Set here rather than in a migration because the
            // snapshot copies -wal and -shm and must have them to copy.
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode = WAL;";
            pragma.ExecuteScalar();
        }

        return connection;
    }

    /// <summary>The journal mode the database currently reports.</summary>
    public static string JournalModeOf(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        return (string)command.ExecuteScalar()!;
    }
}
