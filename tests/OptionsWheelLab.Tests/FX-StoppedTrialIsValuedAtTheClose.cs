using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-StoppedTrialIsValuedAtTheClose: a trial holding shares that meets an
/// unmodelled action reports entries summing to the marked value, not to the
/// outlay [D-W49].
/// </summary>
/// <remarks>
/// [D-W47] says the trial stops and carries the action as its reason; it does not
/// say the position is liquidated at nothing. Zeroing it made every name with a
/// corporate action a total loss, which is a bias with a sign in a lab whose
/// criterion is comparing decision quality across makers: a maker that happened
/// to hold the name with the merger would be scored worse for an event no maker
/// chose.
/// <para>
/// <b>The value is a model and is recorded as one.</b> Shares at the session's
/// close and a short at its quoted price, which is what the account would have
/// had to pay to be rid of it.
/// </para>
/// </remarks>
public sealed class FX_StoppedTrialIsValuedAtTheClose
{
    private static readonly DateOnly ExDate = SecondExpiry;

    [Fact]
    public void The_entries_sum_to_the_marked_value_rather_than_the_outlay()
    {
        var stopped = StoppedHoldingShares(close: 47.00m);

        // 94.35 premium, 5,000.00 paid for the shares, and 100 shares marked at
        // 47.00. The outlay alone would report minus 4,905.65.
        Assert.Equal(
            94.35m - 5_000.00m + 4_700.00m,
            stopped.Entries.Sum(entry => entry.Amount) + 94.35m - 5_000.00m);

        var mark = Assert.Single(stopped.Entries);

        Assert.Equal(LedgerEntryKind.Stopped, mark.Kind);
        Assert.Equal(4_700.00m, mark.Amount);
        Assert.NotEqual(0m, mark.Amount);
    }

    /// <summary>
    /// The mark moves with the close, so it is a valuation rather than a
    /// constant.
    /// </summary>
    /// <remarks>
    /// A machine writing the outlay back, or any other fixed figure, would
    /// satisfy the assertion above on one close and fail here.
    /// </remarks>
    [Fact]
    public void The_mark_follows_the_session_close()
    {
        Assert.Equal(4_700.00m, Assert.Single(StoppedHoldingShares(47.00m).Entries).Amount);
        Assert.Equal(5_200.00m, Assert.Single(StoppedHoldingShares(52.00m).Entries).Amount);
    }

    /// <summary>
    /// A trial stopped while still short is marked at what closing the short
    /// would cost.
    /// </summary>
    /// <remarks>
    /// The other side of the same rule. A short is a liability, so its mark is
    /// negative and it is taken at the ask, which is the side of the spread the
    /// account does not choose [D-W12].
    /// </remarks>
    [Fact]
    public void A_trial_stopped_while_short_is_marked_at_what_closing_it_would_cost()
    {
        var stopped = Machine().Advance(
            OpenedTrial(),
            Session(
                new(2026, 4, 8),
                close: 45.80m,
                actions: [Merger(new(2026, 4, 8))],
                ask: 4.50m));

        Assert.Equal(-450.00m, Assert.Single(stopped.Entries).Amount);
    }

    /// <summary>
    /// The trial still stops, and the mark does not make it something else.
    /// </summary>
    [Fact]
    public void The_trial_still_stops_and_carries_the_action_as_its_reason()
    {
        var stopped = StoppedHoldingShares(close: 47.00m);

        Assert.Equal(TrialCloseKind.Stopped, stopped.State.CloseKind);
        Assert.Equal(ExDate, stopped.State.ClosedOn);
        Assert.Contains(
            "merger", Assert.Single(stopped.Entries).Note!, StringComparison.Ordinal);
        Assert.Contains(
            "marked at the close",
            Assert.Single(stopped.Entries).Note!,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A short with no quote refuses rather than being marked at a price this lab
    /// cannot observe [D-W37].
    /// </summary>
    /// <remarks>
    /// A trial holding only shares needs no quote, since the close prices them.
    /// The refusal is specific to a short the session says nothing about, which
    /// is the case where inventing a figure would put an unrecorded price inside
    /// a scored outcome.
    /// </remarks>
    [Fact]
    public void A_short_with_no_quote_cannot_be_marked()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => Machine().Advance(
                OpenedTrial(),
                Session(new(2026, 4, 8), close: 45.80m, actions: [Merger(new(2026, 4, 8))])));

        Assert.Contains("cannot be valued", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>§6.3's trial holding its assigned shares when a merger arrives.</summary>
    private static Transition StoppedHoldingShares(decimal close)
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        Assert.Equal(100, holding.Shares);

        return machine.Advance(holding, Session(ExDate, close, actions: [Merger(ExDate)]));
    }

    private static ActionOnUnderlying Merger(DateOnly exDate) =>
        new(new CorporateAction(CorporateActionKind.Merger, exDate));
}
