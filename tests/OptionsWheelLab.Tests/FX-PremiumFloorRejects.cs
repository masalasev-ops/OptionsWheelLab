using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-PremiumFloorRejects: a candidate below the premium floor is rejected with
/// its reason.
/// </summary>
/// <remarks>
/// The floor's ground is cost drag rather than measurement [D-W22]: fills are
/// at the bid [D-W12], so below some absolute premium the per-contract
/// commission consumes a large fraction of the credit.
/// <para>
/// <b>The floor rejects a bid below it, not at it</b> [D-W22, which
/// WORKED_EXAMPLE §3 corroborates with two rows sitting exactly on 0.30 and
/// passing]. That boundary is asserted here rather than assumed, because a
/// floor written as "at or below" would pass every other case in this file.
/// </para>
/// </remarks>
public sealed class FX_PremiumFloorRejects
{
    [Fact]
    public void A_bid_below_the_floor_is_rejected_and_one_above_it_is_not()
    {
        var verdicts = GateScenario.Shared(
        [
            // §3's 40.00: bid 0.15 against a floor of 0.30, spread 6.45 percent
            // so the liquidity cap is not what rejects it.
            GateScenario.Quote(40.00m, bid: 0.15m, ask: 0.16m, delta: -0.05m),
            GateScenario.Quote(50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
        ]);

        Assert.Equal([GateReason.PremiumFloor], verdicts[40.00m]);
        Assert.Empty(verdicts[50.00m]);
    }

    /// <summary>
    /// The boundary itself, from both sides.
    /// </summary>
    [Fact]
    public void A_bid_exactly_on_the_floor_passes_and_a_cent_below_does_not()
    {
        var verdicts = GateScenario.Shared(
        [
            GateScenario.Quote(45.00m, bid: 0.30m, ask: 0.32m, delta: -0.10m),
            GateScenario.Quote(47.50m, bid: 0.29m, ask: 0.31m, delta: -0.10m),
        ]);

        Assert.Empty(verdicts[45.00m]);
        Assert.Equal([GateReason.PremiumFloor], verdicts[47.50m]);
    }
}
