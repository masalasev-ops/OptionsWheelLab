using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-RollCapCloses: a trial reaching the roll bound closes at market and
/// resolves [D-W14].
/// </summary>
/// <remarks>
/// A position may be rolled, but a rolled chain terminates at a bound of
/// <c>Trial:MaxRolls</c> or <c>Trial:MaxTrialDays</c>, whichever binds first, at
/// which point the position closes at market. Bounding it keeps trials finite,
/// which the scorer and the walk-forward learning boundary both require, and
/// forces an explicit rule about when to stop defending a losing position.
/// <para>
/// <b>Resolving is the half that matters.</b> A bound that closed the position
/// and left the trial open would keep the count finite and the trial unscorable,
/// which is the failure the fixture name does not describe and the decision's
/// consequence does: this fixes the trial unit the scorer depends on.
/// </para>
/// </remarks>
public sealed class FX_RollCapCloses
{
    /// <summary>The seeded roll bound [CONFIG_REFERENCE].</summary>
    private const int MaxRolls = 2;

    [Fact]
    public void A_trial_at_the_roll_bound_closes_at_market_and_resolves()
    {
        var machine = Machine();

        var bound = machine.Advance(RolledToTheBound(), Session(SecondExpiry, close: 45.00m, ask: 5.40m));

        Assert.Equal(PositionState.Cash, bound.State.State);
        Assert.Equal(TrialCloseKind.ClosedAtBound, bound.State.CloseKind);
        Assert.Equal(SecondExpiry, bound.State.ClosedOn);
        Assert.True(bound.State.IsClosed);
    }

    /// <summary>
    /// At market means at the ask, which is what buying the short back costs
    /// [D-W49].
    /// </summary>
    /// <remarks>
    /// The put is 5.00 in the money against a close of 45.00 and the ask is 5.40,
    /// so the debit is 540.00 rather than the 500.00 intrinsic value would give.
    /// A bound closing at the strike, or at nothing, would satisfy every assertion
    /// about the state, and one closing at intrinsic would satisfy every
    /// assertion here until the two figures differed.
    /// </remarks>
    [Fact]
    public void The_close_pays_the_ask()
    {
        var bound = Machine().Advance(
            RolledToTheBound(), Session(SecondExpiry, close: 45.00m, ask: 5.40m));

        var entry = Assert.Single(bound.Entries);

        Assert.Equal(LedgerEntryKind.BoughtToClose, entry.Kind);
        Assert.Equal(-540.00m, entry.Amount);
        Assert.Equal(SecondExpiry, entry.EntryDate);
        Assert.Equal(SecondMonday, entry.KnownOn);
    }

    /// <summary>
    /// A trial below the bound is not closed, which is what makes the bound the
    /// thing that acted.
    /// </summary>
    /// <remarks>
    /// One roll short of the cap, on the same session and the same close. Without
    /// this the assertions above pass against a machine that closes every trial
    /// it advances.
    /// </remarks>
    [Fact]
    public void A_trial_below_the_bound_is_not_closed()
    {
        var machine = Machine();
        var once = machine.Roll(
            OpenedTrial(), new DateOnly(2026, 4, 8), 1m, Put(50.00m, ThirdExpiry), 1m).State;

        var advanced = machine.Advance(once, Session(SecondExpiry, close: 45.00m));

        Assert.False(advanced.State.IsClosed);
        Assert.Empty(advanced.Entries);
    }

    /// <summary>
    /// The day bound closes a trial that never rolled, which is the other trigger
    /// of the same mechanism.
    /// </summary>
    /// <remarks>
    /// [D-W14] names one mechanism with two triggers, whichever binds first, and
    /// <see cref="TrialCloseKind.ClosedAtBound"/> is one value for that reason.
    /// Which of them fired is read from <c>rolls_used</c> beside the dates: zero
    /// rolls here, against two above.
    /// </remarks>
    [Fact]
    public void The_day_bound_closes_a_trial_that_never_rolled()
    {
        var bounds = new TrialBounds(MaxRolls: 2, MaxTrialDays: 30);
        var machine = new WheelStateMachine(Calendar, bounds);

        var bound = machine.Advance(
            TrialState.OpenShortPut(Put(50.00m, ThirdExpiry), credit: 94.35m, Opened),
            Session(SecondExpiry, close: 45.00m, ask: 5.40m));

        Assert.Equal(TrialCloseKind.ClosedAtBound, bound.State.CloseKind);
        Assert.Equal(0, bound.State.RollsUsed);
        Assert.True(SecondExpiry.DayNumber - Opened.DayNumber >= bounds.MaxTrialDays);
    }

    /// <summary>§6.3's put rolled to the seeded cap, expiring after the bound binds.</summary>
    private static TrialState RolledToTheBound()
    {
        var machine = Machine();
        var state = OpenedTrial();

        for (var roll = 0; roll < MaxRolls; roll++)
        {
            state = machine.Roll(
                state, new DateOnly(2026, 4, 8), 1m, Put(50.00m, ThirdExpiry), 1m).State;
        }

        Assert.Equal(MaxRolls, state.RollsUsed);

        return state;
    }
}
