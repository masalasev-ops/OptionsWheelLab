using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The gate's bounds resolve as of the simulated date, and stop the evaluation
/// when they cannot.
/// </summary>
/// <remarks>
/// Not a registered fixture: the checks registered against 2.3 are the five
/// constraint fixtures plus the crossed quote and the worked example's
/// verdicts. This is 2.3's definition of done asserted rather than restated.
/// </remarks>
public sealed class GateBoundsTests
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Revised =
        new(2026, 6, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 2.3's definition of done: a bound is read as of the simulated date, not
    /// as it stands now [D-W26].
    /// </summary>
    /// <remarks>
    /// The revision is looser rather than tighter because the delta ceiling
    /// carries a cross-key invariant: it must be no tighter than the loosest
    /// policy band [D-W23], and a write violating that is refused rather than
    /// recorded [D-W34]. Loosening exercises the same as-of path and stays
    /// writable.
    /// </remarks>
    [Fact]
    public void A_date_before_a_later_version_resolves_the_earlier_bound()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(ConfigKeys.GateMaxDelta, "0.50", Revised);

        var configuration = new AsOfConfiguration(connection);

        Assert.Equal(
            0.35m,
            GateBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)).MaxDelta);

        Assert.Equal(
            0.50m,
            GateBounds.ResolveFor(configuration, new DateOnly(2026, 7, 1)).MaxDelta);
    }

    /// <summary>
    /// The other half: the seeded values are what a date after the seed sees,
    /// so the test above is comparing two live answers rather than one answer
    /// and one absence.
    /// </summary>
    [Fact]
    public void The_seeded_bounds_resolve_after_the_seed()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var bounds = GateBounds.ResolveFor(
            new AsOfConfiguration(connection), new DateOnly(2026, 3, 1));

        Assert.Equal(0.12m, bounds.MaxSpreadFractionOfMid);
        Assert.Equal(0.30m, bounds.MinPremium);
        Assert.Equal(0.35m, bounds.MaxDelta);
        Assert.Equal(7, bounds.MinDte);
        Assert.Equal(70, bounds.MaxDte);
        Assert.Equal(7, bounds.EarningsClearanceDays);
    }

    /// <summary>
    /// An unresolvable bound stops the evaluation naming the key and the date,
    /// rather than admitting or rejecting [D-W37].
    /// </summary>
    /// <remarks>
    /// Reachable in ordinary use and not only here: the seed stamps `set_at`
    /// from the wall clock, so every bound resolves null for a simulated date
    /// before the seed ran. That collision is owed at Phase 9 and surfaces
    /// through this path.
    /// </remarks>
    [Fact]
    public void A_date_before_the_bound_was_written_stops_the_evaluation()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => GateBounds.ResolveFor(
                new AsOfConfiguration(connection), new DateOnly(2025, 12, 31)));

        Assert.Contains("Gate:", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2025-12-31", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W37", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// It stops rather than admitting or rejecting, which is the distinction
    /// D-W37 exists to make and the one a caller could otherwise paper over.
    /// </summary>
    [Fact]
    public void An_unresolvable_bound_yields_no_verdict_at_all()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var configuration = new AsOfConfiguration(connection);

        // No overload returns a bounds object with a missing value filled in,
        // and none returns null for the caller to interpret. The only outcomes
        // are a complete set of bounds or a raise.
        Assert.Throws<InvalidOperationException>(
            () => GateBounds.ResolveFor(configuration, new DateOnly(2025, 6, 1)));

        Assert.NotNull(GateBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)));
    }

    private static TempStore SeededStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ConfigWriter(connection).AppendAll(SeedValues.All, Seeded);

        return store;
    }
}
