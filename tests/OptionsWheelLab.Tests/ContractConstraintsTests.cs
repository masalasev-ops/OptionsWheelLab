using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The constraint arithmetic, tested without a store because it is a function
/// of its arguments.
/// </summary>
/// <remarks>
/// Not a registered fixture: each family has its own registered check against a
/// real chain. What is here is the behaviour those checks cannot isolate, being
/// the interaction of several constraints on one quote and the order the
/// reasons come back in.
/// </remarks>
public sealed class ContractConstraintsTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Simulated = new(2026, 3, 2);
    private static readonly DateOnly Expiry = new(2026, 4, 17);

    /// <summary>The seeded bounds [CONFIG_REFERENCE].</summary>
    private static readonly GateBounds Seeded = new(
        MaxSpreadFractionOfMid: 0.12m,
        MinPremium: 0.30m,
        MaxDelta: 0.35m,
        MinDte: 7,
        MaxDte: 70,
        EarningsClearanceDays: 7);

    [Fact]
    public void A_quote_breaching_nothing_carries_no_reasons()
    {
        Assert.Empty(Evaluate(Quote(bid: 0.95m, ask: 1.01m, delta: -0.24m)));
    }

    /// <summary>
    /// Every reason, on one quote, in the enum's declared order.
    /// </summary>
    /// <remarks>
    /// The order is what three makers receiving byte-identical sets depends on
    /// [D-W4], so it is asserted rather than assumed. A crossed quote cannot
    /// also breach the spread cap, since the cap is only reached for an
    /// uncrossed one, so the maximum is five.
    /// </remarks>
    [Fact]
    public void Several_breaches_come_back_in_declared_order()
    {
        var reasons = ContractConstraints.Evaluate(
            Quote(bid: 0.10m, ask: 5.00m, delta: -0.90m, expiry: Simulated.AddDays(400)),
            Simulated,
            Seeded,
            [Simulated.AddDays(10)]);

        Assert.Equal(
            [
                GateReason.SpreadCap,
                GateReason.PremiumFloor,
                GateReason.DeltaCeiling,
                GateReason.ExpiryWindow,
                GateReason.EarningsClearance,
            ],
            reasons);

        Assert.Equal(reasons.OrderBy(reason => reason), reasons);
    }

    /// <summary>
    /// A crossed quote carries the crossed reason and not the spread cap, which
    /// is the whole point of it having its own [D-W22].
    /// </summary>
    [Fact]
    public void A_crossed_quote_carries_its_own_reason_and_not_the_spread_cap()
    {
        var reasons = Evaluate(Quote(bid: 1.20m, ask: 1.10m, delta: -0.24m));

        Assert.Contains(GateReason.CrossedMarket, reasons);
        Assert.DoesNotContain(GateReason.SpreadCap, reasons);
    }

    /// <summary>
    /// And the arithmetic that makes it necessary: a crossed quote's spread as
    /// a fraction of mid is negative, so a cap alone admits it.
    /// </summary>
    [Fact]
    public void The_spread_ratio_alone_would_admit_a_crossed_quote()
    {
        var bid = 1.20m;
        var ask = 1.10m;
        var mid = (bid + ask) / 2m;

        Assert.True((ask - bid) / mid < Seeded.MaxSpreadFractionOfMid);
    }

    [Fact]
    public void A_locked_market_passes_the_spread_cap()
    {
        Assert.Empty(Evaluate(Quote(bid: 1.10m, ask: 1.10m, delta: -0.24m)));
    }

    /// <summary>
    /// The floor rejects a bid below it, not at it [D-W22, WORKED_EXAMPLE §3].
    /// </summary>
    [Fact]
    public void A_bid_exactly_on_the_floor_passes()
    {
        Assert.DoesNotContain(
            GateReason.PremiumFloor, Evaluate(Quote(bid: 0.30m, ask: 0.32m, delta: -0.10m)));

        Assert.Contains(
            GateReason.PremiumFloor, Evaluate(Quote(bid: 0.29m, ask: 0.31m, delta: -0.10m)));
    }

    /// <summary>
    /// The ceiling compares absolute delta, so the sign the chain states does
    /// not admit a put the ceiling should reject [D-W23].
    /// </summary>
    [Fact]
    public void The_ceiling_compares_absolute_delta()
    {
        Assert.Contains(
            GateReason.DeltaCeiling, Evaluate(Quote(bid: 2.05m, ask: 2.20m, delta: -0.44m)));

        Assert.Contains(
            GateReason.DeltaCeiling, Evaluate(Quote(bid: 2.05m, ask: 2.20m, delta: 0.44m)));

        Assert.DoesNotContain(
            GateReason.DeltaCeiling, Evaluate(Quote(bid: 0.95m, ask: 1.01m, delta: -0.24m)));
    }

    [Fact]
    public void A_delta_exactly_on_the_ceiling_passes()
    {
        Assert.DoesNotContain(
            GateReason.DeltaCeiling, Evaluate(Quote(bid: 0.95m, ask: 1.01m, delta: -0.35m)));
    }

    /// <summary>
    /// A quote with no delta is not tested against the ceiling, because an
    /// absent value is not a breach.
    /// </summary>
    [Fact]
    public void An_absent_delta_is_not_a_breach()
    {
        Assert.DoesNotContain(
            GateReason.DeltaCeiling, Evaluate(Quote(bid: 0.95m, ask: 1.01m, delta: null)));
    }

    /// <summary>
    /// The window admits its own bounds [D-W24, as amended].
    /// </summary>
    [Fact]
    public void The_expiry_window_admits_its_own_bounds()
    {
        Assert.DoesNotContain(GateReason.ExpiryWindow, AtDte(7));
        Assert.DoesNotContain(GateReason.ExpiryWindow, AtDte(70));
        Assert.Contains(GateReason.ExpiryWindow, AtDte(6));
        Assert.Contains(GateReason.ExpiryWindow, AtDte(71));
    }

    /// <summary>
    /// The clearance window is the contract's life widened by the buffer on
    /// both sides, inclusive of its edge [D-W25, as amended].
    /// </summary>
    [Fact]
    public void The_clearance_window_widens_the_life_by_the_buffer_on_both_sides()
    {
        var window = ContractConstraints.ClearanceWindow(Simulated, Expiry, bufferDays: 7);

        Assert.Equal(new DateOnly(2026, 2, 23), window.From);
        Assert.Equal(new DateOnly(2026, 4, 24), window.To);
    }

    /// <summary>
    /// At a buffer of zero the window collapses to the contract's life, which
    /// is the condition under which life-endpoint inclusivity becomes live.
    /// </summary>
    [Fact]
    public void At_a_zero_buffer_the_window_is_the_life_itself()
    {
        var window = ContractConstraints.ClearanceWindow(Simulated, Expiry, bufferDays: 0);

        Assert.Equal(Simulated, window.From);
        Assert.Equal(Expiry, window.To);
    }

    [Fact]
    public void No_report_in_the_window_is_no_breach()
    {
        Assert.DoesNotContain(
            GateReason.EarningsClearance,
            ContractConstraints.Evaluate(
                Quote(bid: 0.95m, ask: 1.01m, delta: -0.24m), Simulated, Seeded, []));
    }

    private static IReadOnlyList<GateReason> AtDte(int days) =>
        ContractConstraints.Evaluate(
            Quote(bid: 0.95m, ask: 1.01m, delta: -0.24m, expiry: Simulated.AddDays(days)),
            Simulated,
            Seeded,
            []);

    private static IReadOnlyList<GateReason> Evaluate(ContractQuote quote) =>
        ContractConstraints.Evaluate(quote, Simulated, Seeded, []);

    private static ContractQuote Quote(
        decimal bid,
        decimal ask,
        decimal? delta,
        DateOnly? expiry = null) =>
        new(
            ContractIdentity.Of(Symbol, expiry ?? Expiry, OptionRight.Put, 50.00m),
            Simulated,
            bid,
            ask,
            Delta: delta);
}
