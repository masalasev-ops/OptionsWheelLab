using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-DteWindowRejects: candidates on either side of the expiry window are
/// rejected.
/// </summary>
/// <remarks>
/// The two bounds have different grounds [D-W24]. The upper bound is capital
/// sitting committed for the whole life of the contract at a worse return per
/// day; the lower is that the fill model is least defensible for contracts
/// about to expire [D-W12] and the assignment model's error is unmeasured
/// there.
/// <para>
/// <b>Both sides, because a one-sided test passes on a constraint that only
/// checks one.</b> That is the failure this fixture's registered wording names,
/// and it is the reason the file asserts four points rather than two.
/// </para>
/// </remarks>
public sealed class FX_DteWindowRejects
{
    [Fact]
    public void Candidates_on_either_side_of_the_window_are_rejected()
    {
        var verdicts = GateScenario.Gate(
        [
            GateScenario.Quote(40.00m, expiry: GateScenario.Simulated.AddDays(3)),
            GateScenario.Quote(45.00m, expiry: GateScenario.Simulated.AddDays(46)),
            GateScenario.Quote(50.00m, expiry: GateScenario.Simulated.AddDays(120)),
        ]);

        Assert.Equal([GateReason.ExpiryWindow], verdicts[40.00m]);
        Assert.Empty(verdicts[45.00m]);
        Assert.Equal([GateReason.ExpiryWindow], verdicts[50.00m]);
    }

    /// <summary>
    /// The window admits its own bounds [D-W24, as amended], from both sides.
    /// </summary>
    /// <remarks>
    /// D-W24 said "outside `Gate:MinDte` to `Gate:MaxDte`" until 2.3, which
    /// stated its edge only through the convention that a range includes its
    /// endpoints. The amendment says "the inclusive range", and this is where
    /// that reads as an assertion rather than as a reading.
    /// </remarks>
    [Fact]
    public void The_window_admits_its_own_bounds()
    {
        var verdicts = GateScenario.Gate(
        [
            GateScenario.Quote(40.00m, expiry: GateScenario.Simulated.AddDays(6)),
            GateScenario.Quote(45.00m, expiry: GateScenario.Simulated.AddDays(7)),
            GateScenario.Quote(50.00m, expiry: GateScenario.Simulated.AddDays(70)),
            GateScenario.Quote(55.00m, expiry: GateScenario.Simulated.AddDays(71)),
        ]);

        Assert.Equal([GateReason.ExpiryWindow], verdicts[40.00m]);
        Assert.Empty(verdicts[45.00m]);
        Assert.Empty(verdicts[50.00m]);
        Assert.Equal([GateReason.ExpiryWindow], verdicts[55.00m]);
    }
}
