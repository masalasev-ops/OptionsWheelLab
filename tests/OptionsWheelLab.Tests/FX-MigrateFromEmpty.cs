using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-MigrateFromEmpty: migration from empty is correct and idempotent.
/// </summary>
/// <remarks>
/// "Empty" means two different states and both are covered here, because they
/// behave differently: <b>no file at all</b>, which is the first run and has
/// nothing to snapshot, and <b>a file with no pending migrations</b>, which is
/// the second run and does snapshot.
/// </remarks>
public sealed class FX_MigrateFromEmpty
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 28, 9, 15, 30, 250, TimeSpan.Zero);

    [Fact]
    public void A_first_run_against_no_file_applies_every_migration()
    {
        using var store = TempStore.Empty();
        Assert.False(File.Exists(store.DatabasePath));

        var result = new MigrationRunner(store.Connections).Run(Instant);

        Assert.Equal(Migrations.All.Count, result.Applied.Count);
        Assert.Equal(Migrations.All[^1].Id, result.SchemaVersion);
    }

    /// <summary>
    /// The first run has nothing to copy, and says so rather than passing over
    /// it silently.
    /// </summary>
    [Fact]
    public void A_first_run_against_no_file_takes_no_snapshot_and_records_why()
    {
        using var store = TempStore.Empty();

        var result = new MigrationRunner(store.Connections).Run(Instant);

        Assert.False(result.Snapshot.Taken);
        Assert.Contains("nothing to snapshot", result.Snapshot.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_run_applies_nothing_and_leaves_the_schema_version_alone()
    {
        using var store = TempStore.Empty();
        var runner = new MigrationRunner(store.Connections);

        var first = runner.Run(Instant);
        var second = runner.Run(Instant.AddMinutes(1));

        Assert.Empty(second.Applied);
        Assert.Equal(first.SchemaVersion, second.SchemaVersion);
    }

    /// <summary>
    /// The second run has a file, so it snapshots. This is also the case that
    /// would fail if the runner opened the store before snapshotting, because
    /// the exclusive lock would find the runner's own connection.
    /// </summary>
    [Fact]
    public void A_second_run_takes_a_snapshot_rather_than_refusing_itself()
    {
        using var store = TempStore.Empty();
        var runner = new MigrationRunner(store.Connections);

        runner.Run(Instant);
        var second = runner.Run(Instant.AddMinutes(1));

        Assert.True(second.Snapshot.Taken);
        Assert.True(Directory.Exists(second.Snapshot.Directory!));
    }

    [Fact]
    public void The_migration_is_recorded_once_and_only_once()
    {
        using var store = TempStore.Empty();
        var runner = new MigrationRunner(store.Connections);

        runner.Run(Instant);
        runner.Run(Instant.AddMinutes(1));

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_migrations;";

        Assert.Equal(Migrations.All.Count, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Config_rows_exists_after_migrating()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'config_rows';";

        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }
}
