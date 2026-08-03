using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-OrdinaryDividendLeavesContractUnchanged: an ordinary dividend produces a
/// ledger entry and no contract adjustment, and a non-ordinary one produces the
/// adjustment its corporate action states [D-W44].
/// </summary>
/// <remarks>
/// A dividend is two events in this domain and the record carries both. An
/// ordinary cash dividend pays the holder of the shares and leaves the overlying
/// contracts untouched. A non-ordinary one adjusts them by calling for delivery
/// of the dividend, so the deliverable changes and the strike does not [D-W17].
/// <para>
/// <b>Which side of the line an event falls on is transcribed, never derived</b>
/// [D-W36]. Nothing here reads an amount and decides: the kind is stated by the
/// corporate action, and the $12.50 figure the filing gives is a general rule
/// rather than a bound to compute with.
/// </para>
/// <para>
/// <b>The two cases carry the same amount deliberately.</b> Both dividends below
/// are 0.44 per share, so nothing distinguishes them but the kind. A machine
/// deciding by size would pass a pair chosen either side of a threshold and fail
/// here, which is the rule the filing replaced with a test of regularity.
/// </para>
/// </remarks>
public sealed class FX_OrdinaryDividendLeavesContractUnchanged
{
    private const decimal PerShare = 0.44m;

    [Fact]
    public void An_ordinary_dividend_pays_cash_and_leaves_the_call_alone()
    {
        var machine = Machine();
        var written = Written(machine);

        var paid = machine.Advance(
            written,
            Session(SecondMonday, close: 51.30m, actions: [Ordinary(SecondMonday, PerShare)]));

        Assert.Equal(LedgerEntryKind.Dividend, Assert.Single(paid.Entries).Kind);
        Assert.Equal(44.00m, Assert.Single(paid.Entries).Amount);
        Assert.Equal(written.Contract, paid.State.Contract);
    }

    [Fact]
    public void A_non_ordinary_dividend_adjusts_the_call_and_pays_no_cash()
    {
        var machine = Machine();
        var written = Written(machine);

        var adjusted = machine.Advance(
            written,
            Session(
                SecondMonday,
                close: 51.30m,
                actions:
                [
                    NonOrdinary(
                        SecondMonday,
                        PerShare,
                        new StatedSuccessorTerms(
                            Strike: 52.50m, DeliverableShares: 144, Multiplier: 100)),
                ]));

        Assert.Empty(adjusted.Entries);
        Assert.NotEqual(written.Contract, adjusted.State.Contract);
        Assert.Equal(144, adjusted.State.Contract!.DeliverableShares);
    }

    /// <summary>
    /// The adjustment moves the deliverable and leaves the strike [D-W17].
    /// </summary>
    /// <remarks>
    /// The method in force leaves the strike and the values used to calculate
    /// aggregate exercise prices where it found them, so an adjusted contract is a
    /// new identity differing in its fifth component alone [1.5].
    /// </remarks>
    [Fact]
    public void The_adjustment_leaves_the_strike_where_it_found_it()
    {
        var machine = Machine();
        var written = Written(machine);

        var adjusted = machine.Advance(
            written,
            Session(
                SecondMonday,
                close: 51.30m,
                actions:
                [
                    NonOrdinary(
                        SecondMonday,
                        PerShare,
                        new StatedSuccessorTerms(
                            Strike: 52.50m, DeliverableShares: 144, Multiplier: 100)),
                ])).State;

        Assert.Equal(written.Contract!.Strike, adjusted.Contract!.Strike);
        Assert.Equal(written.Contract.Expiry, adjusted.Contract.Expiry);
        Assert.Equal(written.Contract.Right, adjusted.Contract.Right);
    }

    /// <summary>
    /// An adjusting action stating no terms stops rather than deriving them
    /// [D-W36].
    /// </summary>
    /// <remarks>
    /// The tripwire on the decision that adjusted terms are transcribed. A
    /// machine computing a deliverable from a ratio would satisfy every assertion
    /// above and be wrong in exactly the way D-W36 exists to prevent, since the
    /// methodology is era-dependent rather than a formula.
    /// </remarks>
    [Fact]
    public void A_non_ordinary_dividend_stating_no_terms_stops_rather_than_deriving_them()
    {
        var machine = Machine();
        var written = Written(machine);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => machine.Advance(
                written,
                Session(
                    SecondMonday,
                    close: 51.30m,
                    actions:
                    [
                        new ActionOnUnderlying(
                            new CorporateAction(
                                CorporateActionKind.NonOrdinaryDividend,
                                SecondMonday,
                                Amount: PerShare)),
                    ])));

        Assert.Contains("never derived from a ratio", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A scenario states both kinds, and the reader keeps them apart.
    /// </summary>
    [Fact]
    public void A_scenario_states_both_kinds_of_dividend()
    {
        var chain = Core.Synthetic.SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT",
              "bars": [ { "date": "2026-05-18", "close": "51.30000000" } ],
              "chains": [],
              "actions": [
                {
                  "exDate": "2026-05-18",
                  "kind": "ordinary_dividend",
                  "amount": "0.44000000"
                },
                {
                  "exDate": "2026-05-18",
                  "kind": "non_ordinary_dividend",
                  "amount": "0.44000000",
                  "successor": {
                    "strike": "52.50000000",
                    "deliverableShares": "144"
                  }
                }
              ]
            }
            """);

        Assert.Equal(2, chain.Actions.Count);

        // Ordered by ex-date then by stored kind, so two actions on one date have
        // a total order rather than file order.
        Assert.Equal(
            [CorporateActionKind.NonOrdinaryDividend, CorporateActionKind.OrdinaryDividend],
            chain.Actions.Select(action => action.Action.Kind));

        var adjusting = chain.Actions[0];

        Assert.Equal(52.50m, adjusting.StatedSuccessor!.Strike);
        Assert.Equal(144, adjusting.StatedSuccessor.DeliverableShares);
        Assert.Equal(100, adjusting.StatedSuccessor.Multiplier);
        Assert.Null(chain.Actions[1].StatedSuccessor);
    }

    private static TrialState Written(WheelStateMachine machine)
    {
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        return machine.WriteCall(
            holding, MondayAfter, Call(52.50m, ThirdExpiry), Sold(0.70m)).State;
    }
}
