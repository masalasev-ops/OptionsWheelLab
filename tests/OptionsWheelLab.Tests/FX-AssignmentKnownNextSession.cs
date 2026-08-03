using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-AssignmentKnownNextSession: a decision on the day of assignment sees the
/// pre-assignment state, and the following session sees the shares [D-W39].
/// </summary>
/// <remarks>
/// This is [D-W8] applied to the account rather than to the market. A maker that
/// reacted to its own assignment on the day it happened would be reading a fact
/// that did not exist yet, which is the leak an as-of read exists to prevent.
/// <para>
/// <b>Asserted through the read rather than through the field.</b> Checking
/// <c>EffectiveFrom</c> alone would pass against a state machine that stamped the
/// right date and a reader that ignored it. What a decision may see is
/// <see cref="LedgerReading.AsKnownOn"/>'s answer, so that is what is asserted.
/// </para>
/// </remarks>
public sealed class FX_AssignmentKnownNextSession
{
    [Fact]
    public void A_decision_on_the_day_of_assignment_sees_the_short_put()
    {
        var states = Assigned();

        var known = LedgerReading.AsKnownOn(states, FirstExpiry);

        Assert.Equal(PositionState.ShortPut, known.State);
        Assert.Equal(0, known.Shares);
    }

    [Fact]
    public void The_following_session_sees_the_shares()
    {
        var states = Assigned();

        var known = LedgerReading.AsKnownOn(states, MondayAfter);

        Assert.Equal(PositionState.HoldingShares, known.State);
        Assert.Equal(100, known.Shares);
        Assert.Equal(50.00m, known.GrossBasis);
    }

    /// <summary>
    /// The entry carries both dates, which is what the read filters on.
    /// </summary>
    /// <remarks>
    /// An assignment carries the session it occurred in and the session the
    /// account may act on it. Both are stored, because a projection rebuilt from
    /// the ledger [D-W35] must reproduce what was known when, and one date cannot
    /// answer both questions.
    /// </remarks>
    [Fact]
    public void The_assignment_entry_carries_the_session_it_occurred_in_and_the_one_it_was_known()
    {
        var assignment = Assert.Single(
            Machine().Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).Entries);

        Assert.Equal(LedgerEntryKind.Assignment, assignment.Kind);
        Assert.Equal(FirstExpiry, assignment.EntryDate);
        Assert.Equal(MondayAfter, assignment.KnownOn);
        Assert.NotEqual(assignment.EntryDate, assignment.KnownOn);
    }

    /// <summary>
    /// A session before the trial opened has no state to read, rather than the
    /// earliest one.
    /// </summary>
    /// <remarks>
    /// The same shape as an as-of read before the first observation seeing
    /// nothing [1.1]. Returning the opening state would let a decision predating
    /// the trial read a position.
    /// </remarks>
    [Fact]
    public void A_session_before_the_trial_opened_has_no_state_to_read()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => LedgerReading.AsKnownOn(Assigned(), new DateOnly(2026, 3, 1)));

        Assert.Contains("nothing a decision", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>§6.3's put, assigned at its first expiry.</summary>
    private static IReadOnlyList<TrialState> Assigned()
    {
        var opened = OpenedTrial();
        var assigned = Machine().Advance(opened, Session(FirstExpiry, close: 48.90m));

        return [opened, assigned.State];
    }
}
