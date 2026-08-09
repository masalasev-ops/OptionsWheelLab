using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-DeltaCeilingRejects: a candidate above the delta ceiling is rejected with
/// its reason.
/// </summary>
/// <remarks>
/// Delta is the best available proxy for assignment probability and premium
/// rises with it, so a learner rewarded on outcomes in a calm sample drifts
/// toward higher delta [D-W23]. The ceiling makes the worst of that impossible
/// rather than merely visible after the fact.
/// <para>
/// <b>The sign is where this constraint goes wrong.</b> The chain states a
/// put's delta negative [WORKED_EXAMPLE §2] and the ceiling compares the
/// magnitude [D-W23], so a comparison that forgot the absolute value would
/// admit every put ever written: -0.62 is less than 0.35 on a signed
/// comparison. Both signs are asserted for that reason.
/// </para>
/// </remarks>
public sealed class FX_DeltaCeilingRejects
{
    /// <summary>§3's 52.50, at 0.44 against a ceiling of 0.35.</summary>
    private const decimal Above = 52.50m;

    /// <summary>§3's 50.00, at 0.24.</summary>
    private const decimal Inside = 50.00m;

    [Fact]
    public void A_put_above_the_ceiling_is_rejected_and_one_inside_it_is_not()
    {
        var verdicts = GateScenario.Shared(
        [
            GateScenario.Quote(Above, bid: 2.05m, ask: 2.20m, delta: -0.44m),
            GateScenario.Quote(Inside, bid: 0.95m, ask: 1.01m, delta: -0.24m),
        ]);

        Assert.Equal([GateReason.DeltaCeiling], verdicts[Above]);
        Assert.Empty(verdicts[Inside]);
    }

    /// <summary>
    /// A signed comparison would admit the rejected quote above, so this states
    /// the trap rather than only avoiding it.
    /// </summary>
    [Fact]
    public void A_signed_comparison_would_have_admitted_it()
    {
        Assert.True(-0.44m < 0.35m);
        Assert.True(Math.Abs(-0.44m) > 0.35m);
    }

    /// <summary>
    /// The magnitude is what is compared, so a call at the same magnitude is
    /// rejected too.
    /// </summary>
    [Fact]
    public void The_same_magnitude_rejects_whichever_sign_it_carries()
    {
        var verdicts = GateScenario.Shared(
        [
            GateScenario.Quote(Above, bid: 2.05m, ask: 2.20m, delta: 0.44m),
        ]);

        Assert.Equal([GateReason.DeltaCeiling], verdicts[Above]);
    }

    /// <summary>
    /// The ceiling rejects on "exceeds" [D-W23], so a delta exactly at it
    /// passes.
    /// </summary>
    [Fact]
    public void A_delta_exactly_on_the_ceiling_passes()
    {
        var verdicts = GateScenario.Shared(
        [
            GateScenario.Quote(45.00m, bid: 0.95m, ask: 1.01m, delta: -0.35m),
            GateScenario.Quote(47.50m, bid: 0.95m, ask: 1.01m, delta: -0.36m),
        ]);

        Assert.Empty(verdicts[45.00m]);
        Assert.Equal([GateReason.DeltaCeiling], verdicts[47.50m]);
    }
}
