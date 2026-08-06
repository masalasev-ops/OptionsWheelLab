using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The three makers' rules, on cases the worked example cannot supply. Not a
/// registered fixture, so not named <c>FX-*</c>.
/// </summary>
/// <remarks>
/// <b>These assert the rules where FX-WorkedExampleDecisions asserts the
/// document.</b> That fixture reproduces §4's three choices and is worth having,
/// and it discriminates almost nothing: the baseline's band admits one candidate
/// there, and the seeded draw is index zero for every set size the document could
/// hold. So the rules are tested here, on data chosen to discriminate.
/// </remarks>
public sealed class MakerTests
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The baseline takes the higher credit when two candidates are in band.
    /// </summary>
    /// <remarks>
    /// Two in band is the minimum that tells a maximum from a first. The worked
    /// example never reaches it: its baseline band admits only the 50.00, so
    /// "highest credit in band" and "the only one in band" give the same answer
    /// there and the rule is unexercised.
    /// <para>
    /// Both quotes sit inside 0.20 to 0.30, and the one with the higher bid is
    /// second in identity order, so a maker taking the first would fail.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_baseline_takes_the_higher_credit_of_two_in_band()
    {
        using var scenario = new DecisionScenario(
        [
            GateScenario.Quote(50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
            GateScenario.Quote(52.50m, bid: 2.05m, ask: 2.20m, delta: -0.28m),
        ]);

        var decision = Decide(HighestCreditMaker.Baseline(Configuration(scenario)), scenario);

        Assert.Equal(DecisionKind.OpenPut, decision.Kind);
        Assert.Equal(52.50m, decision.Chosen!.Strike);
    }

    /// <summary>
    /// Two candidates tied on credit: the first in the offered order wins.
    /// </summary>
    /// <remarks>
    /// <b>The rule the document cannot reach at all.</b> Nothing in the corpus
    /// states what happens when two in-band candidates carry the same bid. The
    /// order offered is contract identity order, which is the order the generator
    /// emits and the order the record stores, so taking the first takes the order
    /// the maker was given rather than one the comparison invents. A build-time
    /// convention with no authored source, reported at sign-off.
    /// </remarks>
    [Fact]
    public void A_tie_on_credit_takes_the_first_in_the_offered_order()
    {
        using var scenario = new DecisionScenario(
        [
            GateScenario.Quote(50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
            GateScenario.Quote(52.50m, bid: 0.95m, ask: 1.01m, delta: -0.28m),
        ]);

        var decision = Decide(HighestCreditMaker.Baseline(Configuration(scenario)), scenario);

        Assert.Equal(50.00m, decision.Chosen!.Strike);
    }

    /// <summary>
    /// The random maker draws something other than the first candidate.
    /// </summary>
    /// <remarks>
    /// <b>The case the worked example cannot supply, and the reason it cannot is
    /// measured.</b> For that document's session the derived seed's first draw is
    /// 6.4 percent of range, so the index is zero for every set size up to
    /// fifteen. The document enumerates seven strikes, so no set it could hold
    /// would distinguish a uniform draw from a maker taking the first candidate.
    /// That is a property of the document rather than of its feasible set.
    /// <para>
    /// The seed is derived per session and name, so one line changes the draw
    /// without inventing a maker or a configuration. The session after the
    /// document's draws index two, and the document's own draws index zero, so
    /// the two together show the derivation moving with the date.
    /// </para>
    /// <para>
    /// Pinned to a literal index rather than to a distribution. A case asserting
    /// only that two sessions differ would pass on any derivation that varies at
    /// all, including one varying per process.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("2026-03-02", "45.00")]
    [InlineData("2026-03-03", "50.00")]
    [InlineData("2026-03-06", "47.50")]
    public void The_random_maker_draws_by_session(string session, string strike)
    {
        using var scenario = new DecisionScenario(
        [
            GateScenario.Quote(45.00m, bid: 0.30m, ask: 0.32m, delta: -0.10m),
            GateScenario.Quote(47.50m, bid: 0.55m, ask: 0.59m, delta: -0.16m),
            GateScenario.Quote(50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
        ]);

        var decision = new RandomWithinBandMaker(Configuration(scenario)).Decide(
            GateScenario.Symbol,
            DateOnly.Parse(session, System.Globalization.CultureInfo.InvariantCulture),
            PositionState.Cash,
            BookState.Empty,
            scenario.Gated());

        Assert.Equal(StoreDecimal.ParseStored(strike), decision.Chosen!.Strike);
    }

    /// <summary>
    /// A maker whose band admits nothing records a decision to take nothing.
    /// </summary>
    /// <remarks>
    /// A maker that declines every candidate has chosen, and the scorer computes
    /// an outcome for every candidate in that day's feasible set [D-W5], so the
    /// choice to take none of them is as scoreable as taking one. Writing no
    /// decision would make a declining maker indistinguishable from one never
    /// asked.
    /// </remarks>
    [Fact]
    public void A_band_admitting_nothing_records_taking_nothing()
    {
        using var scenario = new DecisionScenario(
            [GateScenario.Quote(45.00m, bid: 0.30m, ask: 0.32m, delta: -0.10m)]);

        // 0.10 is below the baseline's floor of 0.20 and inside the learner's band.
        var baseline = Decide(HighestCreditMaker.Baseline(Configuration(scenario)), scenario);
        var learner = Decide(HighestCreditMaker.Learner(Configuration(scenario)), scenario);

        Assert.Equal(DecisionKind.None, baseline.Kind);
        Assert.Null(baseline.Chosen);
        Assert.Equal(DecisionKind.OpenPut, learner.Kind);
    }

    /// <summary>
    /// Every maker carries its identifier and the three are distinct.
    /// </summary>
    [Fact]
    public void The_three_makers_are_named_distinctly()
    {
        using var scenario = new DecisionScenario([GateScenario.Quote(50.00m)]);

        var configuration = Configuration(scenario);

        string[] named =
        [
            HighestCreditMaker.Baseline(configuration).MakerId,
            new RandomWithinBandMaker(configuration).MakerId,
            HighestCreditMaker.Learner(configuration).MakerId,
        ];

        Assert.Equal(MakerIds.All, named);
        Assert.Equal(3, named.Distinct(StringComparer.Ordinal).Count());
    }

    private static AsOfConfiguration Configuration(DecisionScenario scenario) =>
        new(scenario.Connection);

    private static MakerDecision Decide(IDecisionMaker maker, DecisionScenario scenario) =>
        maker.Decide(
            GateScenario.Symbol,
            GateScenario.Simulated,
            PositionState.Cash,
            BookState.Empty,
            scenario.Gated());
}
