using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-SpreadCapRejects: a candidate above the spread cap is rejected with its
/// reason.
/// </summary>
/// <remarks>
/// The filter protects the measurement before it protects the trade [D-W22].
/// The scorer prices every candidate in the feasible set [D-W5] and regret is
/// measured against the best of them, so an untransactable quote corrupts the
/// regret figure for every decision that day rather than only for one that
/// selects it.
/// <para>
/// Both directions on one chain: a quote above the cap and one below it, so
/// neither passes for want of a counterexample. Everything else about both
/// quotes passes, so a reason other than the spread cap means the constraint
/// leaked.
/// </para>
/// </remarks>
public sealed class FX_SpreadCapRejects
{
    /// <summary>
    /// Bid 0.30 against ask 0.44 is a spread of 0.14 on a mid of 0.37, being
    /// 37.84 percent against a cap of twelve. WORKED_EXAMPLE §3's 42.50.
    /// </summary>
    private const decimal Wide = 42.50m;

    /// <summary>
    /// Bid 0.95 against ask 1.01 is 6.12 percent of mid. §3's 50.00.
    /// </summary>
    private const decimal Tight = 50.00m;

    [Fact]
    public void A_quote_above_the_cap_is_rejected_and_one_below_it_is_not()
    {
        var verdicts = GateScenario.Gate(
        [
            GateScenario.Quote(Wide, bid: 0.30m, ask: 0.44m, delta: -0.07m),
            GateScenario.Quote(Tight, bid: 0.95m, ask: 1.01m, delta: -0.24m),
        ]);

        Assert.Equal([GateReason.SpreadCap], verdicts[Wide]);
        Assert.Empty(verdicts[Tight]);
    }

    /// <summary>
    /// The cap rejects on "exceeds" [D-W22], so a spread exactly at it passes.
    /// </summary>
    /// <remarks>
    /// Bid 1.00 against ask 1.13 is 0.13 on a mid of 1.065, which is 12.2
    /// percent and breaches; bid 1.00 against ask 1.1276 is 0.1276 on 1.0638,
    /// which is 11.99 percent and does not. The pair brackets the cap from both
    /// sides rather than asserting one point.
    /// </remarks>
    [Fact]
    public void The_cap_brackets_from_both_sides()
    {
        var verdicts = GateScenario.Gate(
        [
            GateScenario.Quote(45.00m, bid: 1.00m, ask: 1.13m),
            GateScenario.Quote(47.50m, bid: 1.00m, ask: 1.1276m),
        ]);

        Assert.Equal([GateReason.SpreadCap], verdicts[45.00m]);
        Assert.Empty(verdicts[47.50m]);
    }
}
