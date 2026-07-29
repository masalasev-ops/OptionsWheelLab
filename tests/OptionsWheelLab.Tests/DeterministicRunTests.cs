using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Time;
using OptionsWheelLab.Worker;

namespace OptionsWheelLab.Tests;

/// <summary>
/// 0.5's definition of done: with a fixed clock and the same inputs, two runs
/// produce identical stored rows.
/// </summary>
/// <remarks>
/// Not a registered fixture, so not named <c>FX-*</c>.
/// <para>
/// <b>Compared as table contents, not as file bytes.</b> A SQLite file is not a
/// deterministic rendering of its contents: page layout, the freelist and
/// whatever has not yet checkpointed out of the WAL all differ between two runs
/// that stored exactly the same rows. D-W28 already recorded the same thing
/// about snapshots, which are a defragmented rebuild rather than a byte copy. A
/// byte comparison here would fail for reasons that have nothing to do with the
/// clock, and would be read as the clock's fault.
/// </para>
/// <para>
/// <b>The output-level property is owed at Phase 3.</b> 0.5's detail asked for
/// byte-identical output across two invocations of a simulated run, and there is
/// no simulated run at 0.5 to make output. The thin slice is the first
/// checkpoint with a run to make, and carries it.
/// </para>
/// </remarks>
public sealed class DeterministicRunTests
{
    private const string Key = "Trial:MaxRolls";

    /// <summary>
    /// <c>applied_at</c> is the column that would differ if the clock leaked, so
    /// this fails by roughly the wall-clock gap between the two runs rather than
    /// subtly.
    /// </summary>
    [Fact]
    public void Two_runs_with_one_fixed_clock_store_identical_rows()
    {
        var clock = FixedClock.At();

        using var first = TempStore.Empty();
        using var second = TempStore.Empty();

        var firstRows = MigrateAndSeed(first, clock);
        var secondRows = MigrateAndSeed(second, clock);

        // Vacuity: comparing two empty stores would pass while asserting nothing,
        // which is the failure this corpus has already had once.
        Assert.NotEmpty(firstRows);
        Assert.Equal(firstRows, secondRows);
    }

    /// <summary>
    /// The other direction, so the comparison above is known to be capable of
    /// failing. Two different instants must produce different rows; a comparison
    /// that passed here would be comparing nothing that carries an instant.
    /// </summary>
    [Fact]
    public void Two_runs_with_different_instants_do_not()
    {
        using var first = TempStore.Empty();
        using var second = TempStore.Empty();

        var firstRows = MigrateAndSeed(first, FixedClock.At());
        var secondRows = MigrateAndSeed(
            second,
            new FixedClock(FixedClock.DefaultInstant.AddSeconds(1)));

        Assert.NotEqual(firstRows, secondRows);
    }

    /// <summary>
    /// Runs the verb, then writes a configuration row at the same instant so
    /// <c>config_rows</c> is not empty. Without the row, only
    /// <c>schema_migrations</c> would carry an instant and half the store would
    /// go uncompared.
    /// </summary>
    private static IReadOnlyList<string> MigrateAndSeed(TempStore store, IClock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<StorageOptions>>(
            Microsoft.Extensions.Options.Options.Create(
                new StorageOptions { Path = store.Directory }));
        services.AddSingleton(clock);

        MigrateCommand.Run(services.BuildServiceProvider(), new StringWriter());

        using (var connection = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(connection).Append(Key, "7", clock.UtcNow, "seeded by the test");
        }

        return ReadEverything(store);
    }

    /// <summary>
    /// Every row of every table the schema has, rendered as text.
    /// </summary>
    /// <remarks>
    /// Ordered by <c>id</c> and by <c>(key, version)</c>. Deliberately not by
    /// <c>value</c>: <c>config_rows.value</c> is declared decimal in
    /// <see cref="DecimalColumns"/> and the canonical decimal form is not
    /// order-preserving, so ordering by it in SQL is what D-W29 forbids and what
    /// FX-NoDecimalOrderingInSql would report.
    /// </remarks>
    private static IReadOnlyList<string> ReadEverything(TempStore store)
    {
        using var connection = store.Connections.Open(StoreAccess.Write);

        var rows = new List<string>();

        rows.AddRange(Query(
            connection,
            "SELECT id, name, applied_at FROM schema_migrations ORDER BY id;"));

        rows.AddRange(Query(
            connection,
            "SELECT key, version, value, set_at, note FROM config_rows ORDER BY key, version;"));

        return rows;
    }

    private static IReadOnlyList<string> Query(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var rows = new List<string>();

        while (reader.Read())
        {
            var fields = new string[reader.FieldCount];

            for (var field = 0; field < reader.FieldCount; field++)
            {
                fields[field] = reader.IsDBNull(field) ? "<null>" : reader.GetValue(field).ToString()!;
            }

            rows.Add(string.Join("|", fields));
        }

        return rows;
    }
}
