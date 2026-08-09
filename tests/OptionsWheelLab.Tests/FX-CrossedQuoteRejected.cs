using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-CrossedQuoteRejected: a crossed quote is rejected with its own reason,
/// not the spread cap.
/// </summary>
/// <remarks>
/// The obligation this discharges was that no fixture could exist: 0.6's loader
/// refused a crossed quote, so nothing could put one in a store and nothing
/// could exercise the gate against one. The rule moved to the gate at 2.3
/// [D-W22, as amended], which is what makes this file possible.
/// <para>
/// <b>Its own reason rather than the spread cap.</b> A crossed quote's spread
/// as a fraction of mid is negative, so "above the cap" would be
/// arithmetically false in the audit trail, and the two failures differ in
/// kind: a wide quote is transactable at a bad price, where a crossed quote is
/// not a market at all.
/// </para>
/// <para>
/// The gate must own this regardless of the loader, because Phase 8's vendor
/// ingest reaches the store without passing through the loader at all.
/// </para>
/// </remarks>
public sealed class FX_CrossedQuoteRejected
{
    private const decimal Crossed = 45.00m;
    private const decimal Ordinary = 50.00m;

    [Fact]
    public void A_crossed_quote_is_rejected_and_an_uncrossed_one_is_not()
    {
        var verdicts = GateScenario.Shared(
        [
            GateScenario.Quote(Crossed, bid: 1.20m, ask: 1.10m),
            GateScenario.Quote(Ordinary, bid: 0.95m, ask: 1.01m),
        ]);

        Assert.Equal([GateReason.CrossedMarket], verdicts[Crossed]);
        Assert.Empty(verdicts[Ordinary]);
    }

    /// <summary>
    /// The reason recorded is the crossed one and never the spread cap.
    /// </summary>
    [Fact]
    public void The_reason_is_crossed_and_not_the_spread_cap()
    {
        var verdicts = GateScenario.Shared([GateScenario.Quote(Crossed, bid: 1.20m, ask: 1.10m)]);

        Assert.Contains(GateReason.CrossedMarket, verdicts[Crossed]);
        Assert.DoesNotContain(GateReason.SpreadCap, verdicts[Crossed]);
    }

    /// <summary>
    /// Why the separate reason is necessary rather than tidy: the spread cap
    /// alone admits this quote.
    /// </summary>
    /// <remarks>
    /// Stated as arithmetic so the fixture carries the argument rather than
    /// pointing at it. A cap of twelve percent of mid, against a spread of
    /// -0.10 on a mid of 1.15, is -8.7 percent, which is below any positive cap.
    /// </remarks>
    [Fact]
    public void The_spread_cap_alone_would_have_admitted_it()
    {
        var bid = 1.20m;
        var ask = 1.10m;
        var mid = (bid + ask) / 2m;

        Assert.True(ask - bid < 0m);
        Assert.True((ask - bid) / mid < 0.12m);
    }

    /// <summary>
    /// A locked market is admitted, which is the line between "not a market"
    /// and "a market with no spread". The loader never refused one either.
    /// </summary>
    [Fact]
    public void A_locked_market_is_admitted()
    {
        var verdicts = GateScenario.Shared([GateScenario.Quote(Ordinary, bid: 1.10m, ask: 1.10m)]);

        Assert.Empty(verdicts[Ordinary]);
    }
}
