using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-BoundClosePaysTheAsk: a forced close debits the ask, and a case where
/// intrinsic and ask differ shows which was used [D-W49].
/// </summary>
/// <remarks>
/// An option costs at least its intrinsic value to buy back and normally more, so
/// pricing a forced close at intrinsic closes below the bid and manufactures an
/// edge from the accounting, which is what [D-W12] fixes fills at the bid to
/// prevent. It flatters precisely the trials the bound exists to terminate, which
/// are the losing ones, so the error has a sign.
/// <para>
/// <b>The whole fixture is the case where the two figures differ.</b> A forced
/// close priced at intrinsic and one priced at the ask agree on every position
/// with no time value left, which is why this was wrong for a checkpoint without
/// a test noticing. Each case below states both numbers and asserts which one
/// reached the ledger.
/// </para>
/// </remarks>
public sealed class FX_BoundClosePaysTheAsk
{
    /// <summary>Five points in the money against a close of 45.00.</summary>
    private const decimal Intrinsic = 5.00m;

    [Fact]
    public void The_debit_is_the_ask_and_not_the_intrinsic_value()
    {
        var bound = Bind(ask: 5.40m);

        var entry = Assert.Single(bound.Entries);

        Assert.Equal(LedgerEntryKind.BoughtToClose, entry.Kind);
        Assert.Equal(-540.00m, entry.Amount);
        Assert.NotEqual(-Intrinsic * 100, entry.Amount);
    }

    /// <summary>
    /// A wider spread costs more, so the debit tracks the quote rather than the
    /// position.
    /// </summary>
    /// <remarks>
    /// Two asks against one intrinsic. A machine reading intrinsic would return
    /// the same figure for both and would have satisfied any single-case
    /// assertion.
    /// </remarks>
    [Fact]
    public void A_wider_ask_costs_more_to_close()
    {
        Assert.Equal(-540.00m, Assert.Single(Bind(5.40m).Entries).Amount);
        Assert.Equal(-620.00m, Assert.Single(Bind(6.20m).Entries).Amount);
    }

    /// <summary>
    /// The close is never cheaper than intrinsic, which is the direction the
    /// error had.
    /// </summary>
    /// <remarks>
    /// Stated as the property rather than as a figure: an option's ask is at
    /// least its intrinsic value, so a forced close can only ever cost more than
    /// the old arithmetic gave, and every trial the bound terminates is worse off
    /// than 3.3 first reported it.
    /// </remarks>
    [Fact]
    public void The_close_never_costs_less_than_the_intrinsic_value()
    {
        var atIntrinsic = Assert.Single(Bind(Intrinsic).Entries).Amount;

        Assert.Equal(-500.00m, atIntrinsic);
        Assert.True(Assert.Single(Bind(5.40m).Entries).Amount < atIntrinsic);
    }

    /// <summary>
    /// A session with no ask refuses rather than closing at a price this lab
    /// cannot observe [D-W37].
    /// </summary>
    [Fact]
    public void A_session_with_no_ask_cannot_close_at_market()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => Machine().Advance(RolledToTheBound(), Session(SecondExpiry, close: 45.00m)));

        Assert.Contains("no ask", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("pays the ask", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Shares still leave at the underlying's close, which the ask does not
    /// touch.
    /// </summary>
    /// <remarks>
    /// [D-W49] moves the option's price and not the share's. A trial holding
    /// shares at the bound sells them at the close, and the two prices are
    /// different questions about different instruments.
    /// </remarks>
    [Fact]
    public void Shares_still_leave_at_the_underlyings_close()
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        var bounds = new TrialBounds(MaxRolls: 2, MaxTrialDays: 30);
        var bound = new WheelStateMachine(Calendar, bounds).Advance(
            holding, Session(MondayAfter, close: 45.50m));

        var entry = Assert.Single(bound.Entries);

        Assert.Equal(LedgerEntryKind.SharesSold, entry.Kind);
        Assert.Equal(4_550.00m, entry.Amount);
    }

    private static Transition Bind(decimal ask) =>
        Machine().Advance(
            RolledToTheBound(), Session(SecondExpiry, close: 45.00m, ask: ask));

    /// <summary>The 50.00 put rolled to the seeded cap, expiring after it binds.</summary>
    private static TrialState RolledToTheBound()
    {
        var machine = Machine();
        var state = OpenedTrial();

        for (var roll = 0; roll < Seeded.MaxRolls; roll++)
        {
            state = machine.Roll(
                state, new DateOnly(2026, 4, 8), 1m, Put(50.00m, ThirdExpiry), 1m).State;
        }

        return state;
    }
}
