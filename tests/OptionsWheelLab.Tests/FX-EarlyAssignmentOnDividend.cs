using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-EarlyAssignmentOnDividend: a short call whose underlying goes ex-dividend
/// by more than the call's remaining time value is assigned on the preceding
/// session, and one where the time value is larger is not [D-W42].
/// </summary>
/// <remarks>
/// <b>The decision cites nothing, which is different from citing weakly.</b>
/// Whether the holder of a long call exercises early is that holder's decision
/// and no rule governs the making of it. The condition is chosen: a holder who
/// exercises captures the dividend and gives up the option's remaining time
/// value, so the exchange is worth making when the first exceeds the second.
/// <para>
/// <b>Time value is measured off the bid</b>, which is the only price this lab
/// reads [D-W12]. It is also the conservative direction: a lower price is a lower
/// time value, so more exercises are modelled, which is the outcome adverse to
/// the lab.
/// </para>
/// <para>
/// <b>The adjusted case has no early assignment at all</b>, as amended by 3.2's
/// completeness pass. A holder who receives the dividend through an adjusted
/// deliverable has no reason to surrender the time value to capture it [D-W44],
/// so the condition reaches unadjusted dividends only.
/// </para>
/// </remarks>
public sealed class FX_EarlyAssignmentOnDividend
{
    /// <summary>
    /// A call at the money, so its bid is time value and nothing else.
    /// </summary>
    /// <remarks>
    /// The close equals the strike, so intrinsic value is zero and the bid is the
    /// whole of the remaining time value. That keeps the two figures the
    /// condition compares visible in the test rather than buried in an
    /// intrinsic-value subtraction.
    /// </remarks>
    private const decimal AtTheMoney = 52.50m;

    [Fact]
    public void A_dividend_exceeding_the_time_value_assigns_on_the_preceding_session()
    {
        var assigned = Resolve(bid: 0.30m, perShare: 0.44m);

        Assert.Equal(TrialCloseKind.CalledAway, assigned.State.CloseKind);
        Assert.Equal(SecondMonday, assigned.State.ClosedOn);

        var entry = Assert.Single(assigned.Entries);

        Assert.Equal(LedgerEntryKind.CallAway, entry.Kind);
        Assert.Equal(SecondMonday, entry.EntryDate);
    }

    [Fact]
    public void A_dividend_below_the_time_value_does_not_assign()
    {
        var held = Resolve(bid: 0.60m, perShare: 0.44m);

        Assert.Equal(PositionState.ShortCall, held.State.State);
        Assert.Empty(held.Entries);
    }

    /// <summary>
    /// The comparison is strict, so a dividend equal to the time value does not
    /// assign.
    /// </summary>
    /// <remarks>
    /// The exchange is worth making when the dividend exceeds the time value, and
    /// at equality the holder gains nothing by giving up the option. Asserted
    /// because the boundary is where a chosen condition is most easily written
    /// the other way.
    /// </remarks>
    [Fact]
    public void A_dividend_equal_to_the_time_value_does_not_assign()
    {
        Assert.Equal(PositionState.ShortCall, Resolve(bid: 0.44m, perShare: 0.44m).State.State);
    }

    /// <summary>
    /// An adjusted dividend has no early assignment, whatever the time value.
    /// </summary>
    /// <remarks>
    /// The same figures as the assigning case, with the dividend non-ordinary.
    /// A holder receiving the dividend through the deliverable has no reason to
    /// exercise, so this is the scope [D-W42] was narrowed to at 3.2.
    /// </remarks>
    [Fact]
    public void A_non_ordinary_dividend_does_not_assign_early()
    {
        var machine = Machine();
        var written = Written(machine);

        var held = machine.Advance(
            written,
            Session(
                SecondMonday,
                close: AtTheMoney,
                actions:
                [
                    NonOrdinary(
                        ThirdExpiry,
                        perShare: 0.44m,
                        new StatedSuccessorTerms(
                            Strike: AtTheMoney, DeliverableShares: 100, Multiplier: 100)),
                ],
                bid: 0.30m));

        Assert.Equal(PositionState.ShortCall, held.State.State);
        Assert.Empty(held.Entries);
    }

    /// <summary>
    /// No quote, no assignment, rather than an assignment on an assumed price.
    /// </summary>
    /// <remarks>
    /// The condition compares a dividend against a time value, and a session with
    /// no bid for the short has no time value to compare. Modelling one would put
    /// an unrecorded price assumption inside an assignment.
    /// </remarks>
    [Fact]
    public void A_session_with_no_bid_for_the_call_does_not_assign()
    {
        var machine = Machine();
        var written = Written(machine);

        var held = machine.Advance(
            written,
            Session(
                SecondMonday,
                close: AtTheMoney,
                actions: [Ordinary(ThirdExpiry, 0.44m)],
                bid: null));

        Assert.Equal(PositionState.ShortCall, held.State.State);
    }

    /// <summary>
    /// The trial short a call on the session before an ex-dividend date.
    /// </summary>
    private static Transition Resolve(decimal bid, decimal perShare)
    {
        var machine = Machine();

        return machine.Advance(
            Written(machine),
            Session(
                SecondMonday,
                close: AtTheMoney,
                actions: [Ordinary(ThirdExpiry, perShare)],
                bid: bid));
    }

    private static TrialState Written(WheelStateMachine machine)
    {
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        return machine.WriteCall(
            holding, MondayAfter, Call(AtTheMoney, ThirdExpiry), credit: 69.35m).State;
    }
}
