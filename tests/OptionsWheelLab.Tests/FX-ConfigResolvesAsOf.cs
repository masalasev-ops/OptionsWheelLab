using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ConfigResolvesAsOf: a key resolves to the version in force on the
/// simulated date.
/// </summary>
/// <remarks>
/// Resolution is inclusive of the as-of date [D-W26]. Each test is named for
/// which way it resolves, because the boundary is the case that decides whether
/// configuration written on a simulated date governs that date.
/// </remarks>
public sealed class FX_ConfigResolvesAsOf
{
    // Deliberately a key that belongs to no cross-key invariant. This fixture is
    // about resolution, not about the invariants, and a write touching an
    // invariant's key must carry that invariant's whole key set [D-W34]. It was
    // Gate:MaxDelta until 0.8 gave the write path teeth.
    private const string Key = "Gate:MinPremium";

    [Fact]
    public void Three_versions_resolve_to_the_one_in_force_not_the_newest()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.Append(Key, "0.30", At(2026, 1, 10));
        writer.Append(Key, "0.35", At(2026, 3, 20));
        writer.Append(Key, "0.40", At(2026, 6, 5));

        var configuration = new AsOfConfiguration(connection);

        Assert.Equal("0.30", configuration.Resolve(Key, new DateOnly(2026, 2, 1)));
        Assert.Equal("0.35", configuration.Resolve(Key, new DateOnly(2026, 4, 1)));
        Assert.Equal("0.40", configuration.Resolve(Key, new DateOnly(2026, 7, 1)));
    }

    /// <summary>
    /// The boundary, and the case a same-type comparison passes while a real
    /// caller fails. <c>set_at</c> is a timestamp and the as-of value is a
    /// date, so without widening the date to its last instant every row written
    /// on that date would sort after it and vanish.
    /// </summary>
    [Fact]
    public void A_row_written_at_any_time_on_the_as_of_date_is_in_force_on_that_date()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            Key,
            "written-late-in-the-day",
            new DateTimeOffset(2026, 3, 20, 23, 59, 59, 999, TimeSpan.Zero));

        var resolved = new AsOfConfiguration(connection).Resolve(Key, new DateOnly(2026, 3, 20));

        Assert.Equal("written-late-in-the-day", resolved);
    }

    [Fact]
    public void A_row_written_at_the_first_instant_of_the_as_of_date_is_in_force_on_that_date()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            Key,
            "written-at-midnight",
            new DateTimeOffset(2026, 3, 20, 0, 0, 0, 0, TimeSpan.Zero));

        var resolved = new AsOfConfiguration(connection).Resolve(Key, new DateOnly(2026, 3, 20));

        Assert.Equal("written-at-midnight", resolved);
    }

    [Fact]
    public void A_row_written_at_any_time_on_the_following_day_is_not_in_force()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            Key,
            "written-just-after-midnight",
            new DateTimeOffset(2026, 3, 21, 0, 0, 0, 1, TimeSpan.Zero));

        var resolved = new AsOfConfiguration(connection).Resolve(Key, new DateOnly(2026, 3, 20));

        Assert.Null(resolved);
    }

    [Fact]
    public void A_key_with_no_version_by_the_as_of_date_resolves_to_nothing()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(Key, "0.35", At(2026, 6, 1));

        Assert.Null(new AsOfConfiguration(connection).Resolve(Key, new DateOnly(2026, 1, 1)));
    }

    /// <summary>
    /// The current surface exists for operational paths and deliberately
    /// disagrees with the as-of surface, which is the whole point of separating
    /// them.
    /// </summary>
    [Fact]
    public void The_current_surface_returns_the_newest_where_the_as_of_surface_does_not()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.Append(Key, "0.30", At(2026, 1, 10));
        writer.Append(Key, "0.40", At(2026, 6, 5));

        Assert.Equal("0.30", new AsOfConfiguration(connection).Resolve(Key, new DateOnly(2026, 2, 1)));
        Assert.Equal("0.40", new CurrentConfiguration(connection).ResolveCurrent(Key));
    }

    private static DateTimeOffset At(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(At(2026, 1, 1));
        return store;
    }
}
