using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-UnmodelledActionStopsTheTrial: a merger on a held underlying stops the
/// trial with the action recorded as its reason, and a split does not [D-W47].
/// </summary>
/// <remarks>
/// What each action does to a contract is deferred, which the obligation raising
/// this permitted. What an unmodelled action does is not deferred, because that
/// is the silence it forbade. After a merger the trial's contract no longer
/// overlies what the trial opened against, so carrying on would price a position
/// on terms the lab cannot compute, and dropping the event would leave a return
/// that reads as ordinary.
/// <para>
/// <b>The vocabulary is complete and the transitions are not, which is the state
/// this asserts.</b> Every kind OCC's adjustment provisions name can be stated by
/// a scenario and stored, and the four the lab models pass through while the rest
/// stop the trial. A vocabulary that admitted only what the lab handles would
/// make an unmodelled event unrepresentable rather than visible.
/// </para>
/// </remarks>
public sealed class FX_UnmodelledActionStopsTheTrial
{
    private static readonly DateOnly ExDate = new(2026, 4, 8);

    [Fact]
    public void A_merger_stops_the_trial_and_records_the_action_as_its_reason()
    {
        var stopped = Resolve(CorporateActionKind.Merger);

        Assert.Equal(TrialCloseKind.Stopped, stopped.State.CloseKind);
        Assert.Equal(ExDate, stopped.State.ClosedOn);

        var entry = Assert.Single(stopped.Entries);

        Assert.Equal(LedgerEntryKind.Stopped, entry.Kind);
        Assert.Equal(0m, entry.Amount);
        Assert.Contains("merger", entry.Note!, StringComparison.Ordinal);
        Assert.Contains("2026-04-08", entry.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void A_split_does_not_stop_the_trial()
    {
        var machine = Machine();

        var adjusted = machine.Advance(
            OpenedTrial(),
            Session(
                ExDate,
                close: 45.80m,
                actions:
                [
                    new ActionOnUnderlying(
                        new CorporateAction(CorporateActionKind.Split, ExDate, Ratio: 1.5m),
                        new StatedSuccessorTerms(
                            Strike: 50.00m, DeliverableShares: 150, Multiplier: 100)),
                ]));

        Assert.False(adjusted.State.IsClosed);
        Assert.Empty(adjusted.Entries);
        Assert.Equal(150, adjusted.State.Contract!.DeliverableShares);
    }

    /// <summary>
    /// Every kind the lab does not model stops the trial, not only the merger.
    /// </summary>
    /// <remarks>
    /// The obligation named five that were unrepresented in this corpus, and a
    /// fixture asserting one of them would leave the other three untested while
    /// reading as though the class were covered.
    /// </remarks>
    [Theory]
    [InlineData(CorporateActionKind.RightsOffering)]
    [InlineData(CorporateActionKind.Reorganization)]
    [InlineData(CorporateActionKind.Merger)]
    [InlineData(CorporateActionKind.Liquidation)]
    [InlineData(CorporateActionKind.SpinOff)]
    public void Every_unmodelled_kind_stops_the_trial(CorporateActionKind kind)
    {
        var stopped = Resolve(kind);

        Assert.Equal(TrialCloseKind.Stopped, stopped.State.CloseKind);
        Assert.Contains(
            StoreCorporateActionKind.ToStored(kind),
            Assert.Single(stopped.Entries).Note!,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The three the lab models do not stop it, which is the other half of the
    /// partition.
    /// </summary>
    /// <remarks>
    /// Asserted over the kinds rather than one of them, so a machine stopping on
    /// everything would fail here rather than passing the theory above for the
    /// wrong reason.
    /// </remarks>
    [Theory]
    [InlineData(CorporateActionKind.OrdinaryDividend)]
    [InlineData(CorporateActionKind.NonOrdinaryDividend)]
    [InlineData(CorporateActionKind.Split)]
    public void A_modelled_kind_does_not_stop_the_trial(CorporateActionKind kind)
    {
        var machine = Machine();

        var advanced = machine.Advance(
            OpenedTrial(),
            Session(
                ExDate,
                close: 45.80m,
                actions:
                [
                    new ActionOnUnderlying(
                        new CorporateAction(kind, ExDate, Amount: 0.44m),
                        new StatedSuccessorTerms(
                            Strike: 50.00m, DeliverableShares: 150, Multiplier: 100)),
                ]));

        Assert.False(advanced.State.IsClosed);
    }

    /// <summary>
    /// A scenario can state an action the lab does not model, and the store can
    /// hold it.
    /// </summary>
    /// <remarks>
    /// The vocabulary being complete before the transitions are is only true if
    /// an unmodelled kind survives the round trip through the format and the
    /// stored form.
    /// </remarks>
    [Fact]
    public void A_scenario_can_state_an_unmodelled_action()
    {
        var chain = Core.Synthetic.SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT",
              "bars": [ { "date": "2026-04-08", "close": "45.80000000" } ],
              "chains": [],
              "actions": [ { "exDate": "2026-04-08", "kind": "merger" } ]
            }
            """);

        var stated = Assert.Single(chain.Actions);

        Assert.Equal(CorporateActionKind.Merger, stated.Action.Kind);
        Assert.Equal(
            CorporateActionKind.Merger,
            StoreCorporateActionKind.ParseStored(
                StoreCorporateActionKind.ToStored(stated.Action.Kind)));
    }

    private static Transition Resolve(CorporateActionKind kind) =>
        Machine().Advance(
            OpenedTrial(),
            Session(
                ExDate,
                close: 45.80m,
                actions: [new ActionOnUnderlying(new CorporateAction(kind, ExDate))]));
}
