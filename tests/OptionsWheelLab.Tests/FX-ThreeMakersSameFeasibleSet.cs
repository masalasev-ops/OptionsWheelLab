using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ThreeMakersSameFeasibleSet: all makers holding the same position in a name
/// receive byte-identical candidate sets [D-W4].
/// </summary>
/// <remarks>
/// Registered since v1.0.0 and implemented at 4.3, which is the first checkpoint
/// with three makers to hold it for.
/// <para>
/// <b>The property is conditional and the decision says so.</b> D-W4's test line
/// was amended at 4.1: the makers' positions diverge by design and the divergence
/// is the experiment, so the claim is about a day the three hold the same
/// position. A maker holding shares faces calls where one in cash faces puts, and
/// that is the lab working rather than failing.
/// </para>
/// <para>
/// <b>Asserted end to end rather than at the offer.</b> Checking that one list
/// handed to three makers is one list proves a property of C# rather than of this
/// lab. What is checked is that three decisions recorded against one session
/// rebuild to byte-identical feasible sets, which is the claim a scorer depends
/// on: it re-scores each maker's decision against the set that maker saw, and if
/// those sets differed the regret figures would not be comparable.
/// </para>
/// <para>
/// <b>True by construction rather than by three writes agreeing</b> [D-W52]. The
/// set is stored once per symbol, session and right and referenced three times,
/// so there is one row for the three records to disagree about. The definition of
/// done asks for exactly that, and a fixture passing because three copies happened
/// to match would meet the test and not the definition.
/// </para>
/// </remarks>
public sealed class FX_ThreeMakersSameFeasibleSet
{
    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Three_makers_on_one_session_rebuild_to_identical_sets()
    {
        using var scenario = Scenario();

        var records = RecordAll(scenario)
            .Select(scenario.Reader.Read)
            .ToList();

        Assert.Equal(3, records.Count);

        // Every maker's rebuilt set, rendered, must be one string.
        var rendered = records.Select(record => Render(record)).Distinct(StringComparer.Ordinal);

        Assert.Single(rendered);
    }

    /// <summary>
    /// One stored set, referenced three times, rather than three copies.
    /// </summary>
    [Fact]
    public void The_three_decisions_reference_one_stored_set()
    {
        using var scenario = Scenario();

        RecordAll(scenario);

        using var read = scenario.Connection.CreateCommand();
        read.CommandText = "SELECT COUNT(*) FROM feasible_sets;";

        Assert.Equal(1L, read.ExecuteScalar());
    }

    /// <summary>
    /// The makers differ in what they chose and not in what they were shown.
    /// </summary>
    /// <remarks>
    /// The vacuity guard. Three makers that all took the same candidate, or all
    /// took nothing, would satisfy the cases above while showing nothing about
    /// whether a shared set permits different choices. On this chain the baseline
    /// and the learner take different strikes, which is what makes the identical
    /// set a claim about permission rather than about outcome.
    /// </remarks>
    [Fact]
    public void The_makers_differ_in_choice_while_sharing_the_set()
    {
        using var scenario = Scenario();

        var chosen = RecordAll(scenario)
            .Select(id => scenario.Reader.Read(id).Chosen?.Contract.Strike)
            .Distinct()
            .ToList();

        Assert.True(
            chosen.Count > 1,
            $"All three makers chose {string.Join(", ", chosen)}, so an identical set is not "
            + "evidence that the set permitted different choices.");
    }

    /// <summary>§3's feasible set, which the three bands cut differently.</summary>
    private static DecisionScenario Scenario() =>
        new(
        [
            GateScenario.Quote(45.00m, bid: 0.30m, ask: 0.32m, delta: -0.10m),
            GateScenario.Quote(47.50m, bid: 0.55m, ask: 0.59m, delta: -0.16m),
            GateScenario.Quote(50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
        ]);

    /// <summary>
    /// One gated set, three makers, three decisions.
    /// </summary>
    /// <remarks>
    /// <c>GateFor</c> is called once. Three calls would satisfy every assertion
    /// here and fail the definition of done, since the sets would then be equal
    /// because the generator repeats rather than because there is one set.
    /// </remarks>
    private static IReadOnlyList<long> RecordAll(DecisionScenario scenario)
    {
        var configuration = new AsOfConfiguration(scenario.Connection);
        var offered = scenario.Gated();

        IDecisionMaker[] makers =
        [
            HighestCreditMaker.Baseline(configuration),
            new RandomWithinBandMaker(configuration),
            HighestCreditMaker.Learner(configuration),
        ];

        return
        [
            .. makers.Select(maker =>
            {
                var decision = maker.Decide(
                    GateScenario.Symbol,
                    GateScenario.Simulated,
                    PositionState.Cash,
                    BookState.Empty,
                    offered);

                return scenario.Decisions.Record(
                    maker.MakerId,
                    GateScenario.Symbol,
                    GateScenario.Simulated,
                    OptionRight.Put,
                    offered,
                    decision.Kind,
                    decision.Chosen,
                    decision.TrialId,
                    decision.PolicyVersion,
                    Recorded);
            })
        ];
    }

    /// <summary>
    /// The rebuilt set as text, so equality is over what was recorded rather than
    /// over object identity.
    /// </summary>
    private static string Render(DecisionRecord record) =>
        string.Join(
            "\n",
            record.FeasibleSet.Select(candidate =>
                $"{candidate.Contract}|{candidate.ContractsQty}|{candidate.CommittedCapital}|"
                + $"{candidate.Credit}|{candidate.Bid}|{candidate.Ask}|{candidate.FeatureJson}|"
                + $"{string.Join(",", candidate.Reasons)}"));
}
