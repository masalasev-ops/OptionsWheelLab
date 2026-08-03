using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The fill model's costs resolve as of the simulated date, and stop the
/// evaluation when they cannot.
/// </summary>
/// <remarks>
/// Not a registered fixture, on <see cref="GateBoundsTests"/>' argument: 3.4's
/// registered check is the worked example's total, and this is the checkpoint's
/// definition of done asserted rather than restated.
/// </remarks>
public sealed class CostBoundsTests
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Revised =
        new(2026, 6, 1, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_seeded_costs_resolve_after_the_seed()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var costs = CostBounds.ResolveFor(
            new AsOfConfiguration(connection), new DateOnly(2026, 3, 1));

        Assert.Equal(0.65m, costs.CommissionPerContract);
        Assert.Equal(0.00m, costs.AssignmentFee);
        Assert.Equal(FillPoint.Bid, costs.FillPoint);
    }

    /// <summary>
    /// A cost is read as of the simulated date, not as it stands now [D-W26].
    /// </summary>
    /// <remarks>
    /// The commission is the one that moves, because a broker's rate changing is
    /// the change this store's versioning exists for and the note on that key
    /// already says so. A trial priced before the revision keeps the rate it was
    /// actually charged, which is the whole of what as-of resolution buys here:
    /// re-scoring a decision under a rate it never paid would be a different
    /// trial.
    /// </remarks>
    [Fact]
    public void A_date_before_a_later_version_resolves_the_earlier_rate()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            ConfigKeys.CostsCommissionPerContract, "0.50", Revised);

        var configuration = new AsOfConfiguration(connection);

        Assert.Equal(
            0.65m,
            CostBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1))
                .CommissionPerContract);

        Assert.Equal(
            0.50m,
            CostBounds.ResolveFor(configuration, new DateOnly(2026, 7, 1))
                .CommissionPerContract);
    }

    /// <summary>
    /// The assignment fee moves the same way, which is why zero still earns a key
    /// [D-W50].
    /// </summary>
    /// <remarks>
    /// One broker's schedule is not a market rule, so a broker that charges is a
    /// stored value changing rather than code changing. This is that claim
    /// exercised rather than asserted.
    /// </remarks>
    [Fact]
    public void A_broker_that_charges_is_a_stored_value_rather_than_a_code_change()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(ConfigKeys.CostsAssignmentFee, "0.55", Revised);

        var configuration = new AsOfConfiguration(connection);

        Assert.Equal(
            0.00m,
            CostBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)).AssignmentFee);

        Assert.Equal(
            0.55m,
            CostBounds.ResolveFor(configuration, new DateOnly(2026, 7, 1)).AssignmentFee);
    }

    /// <summary>
    /// A cost with no value in force stops the evaluation [D-W37].
    /// </summary>
    [Fact]
    public void A_date_before_the_seed_stops_rather_than_defaulting()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var configuration = new AsOfConfiguration(connection);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => CostBounds.ResolveFor(configuration, new DateOnly(2025, 6, 1)));

        Assert.Contains("Costs:", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2025-06-01", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W37", thrown.Message, StringComparison.Ordinal);

        Assert.NotNull(CostBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)));
    }

    /// <summary>
    /// A fill point the lab does not admit is refused, which is why the key is
    /// read at all [D-W12].
    /// </summary>
    /// <remarks>
    /// A fill model that skipped the key would honour the rule by accident while
    /// the row asserted a different one. This is the case that makes reading a
    /// value that cannot vary worth doing.
    /// </remarks>
    [Fact]
    public void A_fill_point_outside_the_vocabulary_is_refused()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(ConfigKeys.CostsFillPoint, "mid", Revised);

        var configuration = new AsOfConfiguration(connection);

        var thrown = Assert.Throws<FormatException>(
            () => CostBounds.ResolveFor(configuration, new DateOnly(2026, 7, 1)));

        Assert.Contains("not a stored fill point", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W12", thrown.Message, StringComparison.Ordinal);

        // The earlier version still resolves, so the refusal is about the value
        // rather than about the key having become unreadable.
        Assert.Equal(
            FillPoint.Bid,
            CostBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)).FillPoint);
    }

    [Fact]
    public void The_fill_points_stored_form_round_trips()
    {
        Assert.Equal("bid", StoreFillPoint.ToStored(FillPoint.Bid));
        Assert.Equal(FillPoint.Bid, StoreFillPoint.ParseStored("bid"));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoreFillPoint.ToStored(default));
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
