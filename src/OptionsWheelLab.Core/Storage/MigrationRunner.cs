using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Storage;

/// <summary>What a migration run did.</summary>
public sealed record MigrationResult(
    SnapshotResult Snapshot,
    IReadOnlyList<Migration> Applied,
    int SchemaVersion);

/// <summary>
/// Applies pending migrations, snapshotting first.
/// </summary>
/// <remarks>
/// The guarantee lives here rather than in <c>migrate.ps1</c>, so running the
/// migration by hand cannot skip the snapshot.
/// <para>
/// The snapshot is taken before anything is applied, and against a store that
/// does not exist yet there is nothing to protect, which the result records
/// [D-W28].
/// </para>
/// </remarks>
public sealed class MigrationRunner
{
    private readonly StoreConnectionFactory _connections;

    public MigrationRunner(StoreConnectionFactory connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        _connections = connections;
    }

    public MigrationResult Run(DateTimeOffset instant)
    {
        // Snapshot first, so a failed migration is recoverable. Skipped on the
        // first run, which has no store yet.
        var snapshot = StoreSnapshot.Take(_connections.Location, instant);

        using var connection = _connections.Open(StoreAccess.Write);

        EnsureMigrationsTable(connection);

        var alreadyApplied = AppliedIds(connection);
        var applied = new List<Migration>();

        foreach (var migration in Migrations.All)
        {
            if (alreadyApplied.Contains(migration.Id))
            {
                continue;
            }

            using var transaction = connection.BeginTransaction();

            Execute(connection, transaction, migration.Sql);

            using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText =
                    "INSERT INTO schema_migrations (id, name, applied_at) VALUES ($id, $name, $at);";
                record.Parameters.AddWithValue("$id", migration.Id);
                record.Parameters.AddWithValue("$name", migration.Name);
                record.Parameters.AddWithValue("$at", StoreTimestamp.ToStored(instant));
                record.ExecuteNonQuery();
            }

            transaction.Commit();
            applied.Add(migration);
        }

        return new MigrationResult(snapshot, applied, SchemaVersionOf(connection));
    }

    /// <summary>The highest applied migration id, or zero when none is applied.</summary>
    public static int SchemaVersionOf(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(id), 0) FROM schema_migrations;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void EnsureMigrationsTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id         INTEGER NOT NULL PRIMARY KEY,
                name       TEXT    NOT NULL,
                applied_at TEXT    NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static HashSet<int> AppliedIds(SqliteConnection connection)
    {
        var ids = new HashSet<int>();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM schema_migrations;";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
