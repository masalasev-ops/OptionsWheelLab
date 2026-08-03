using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ExpiryResolvesAtOneCent: a short put closing one cent below its strike
/// assigns; one closing at the strike expires worthless [D-W38].
/// </summary>
/// <remarks>
/// OCC Rule 805's exercise-by-exception threshold for equity options is one cent,
/// against the closing price of the underlying on the last trading day before
/// expiration. The lab models the common case and records that it is a model: a
/// contrary exercise advice is a choice made by the holder of a contract the lab
/// is short, and the lab cannot observe it.
/// <para>
/// <b>The boundary is asserted at the boundary.</b> A cent in the money assigns
/// and anything less does not, so what distinguishes a correct threshold from an
/// approximately correct one is 49.99 against 49.995, not 49.99 against 50.00. A
/// pair either side of the strike would pass against a threshold of zero, which
/// is the rule the filing changed away from in the other direction.
/// </para>
/// </remarks>
public sealed class FX_ExpiryResolvesAtOneCent
{
    [Fact]
    public void A_put_one_cent_in_the_money_assigns()
    {
        var resolved = Resolve(close: 49.99m);

        Assert.Equal(PositionState.HoldingShares, resolved.State.State);
        Assert.Equal(LedgerEntryKind.Assignment, Assert.Single(resolved.Entries).Kind);
    }

    [Fact]
    public void A_put_closing_at_the_strike_expires_worthless()
    {
        var resolved = Resolve(close: 50.00m);

        Assert.Equal(PositionState.Cash, resolved.State.State);
        Assert.Equal(TrialCloseKind.ExpiredWorthless, resolved.State.CloseKind);
        Assert.Equal(LedgerEntryKind.ExpiredWorthless, Assert.Single(resolved.Entries).Kind);
    }

    /// <summary>
    /// Less than a cent in the money is out of the money for this purpose.
    /// </summary>
    /// <remarks>
    /// The half a threshold of zero would fail. Without it the pair above is
    /// satisfied by any rule that assigns below the strike, and the figure the
    /// decision cites would be untested.
    /// </remarks>
    [Fact]
    public void A_put_less_than_a_cent_in_the_money_expires_worthless()
    {
        var resolved = Resolve(close: 49.995m);

        Assert.Equal(PositionState.Cash, resolved.State.State);
        Assert.Equal(TrialCloseKind.ExpiredWorthless, resolved.State.CloseKind);
    }

    /// <summary>
    /// A short call resolves on the same threshold, from the other side.
    /// </summary>
    /// <remarks>
    /// The decision says a short option, not a short put, and a call in the money
    /// by a cent is the case that ends a wheel turn: the shares are called away.
    /// </remarks>
    [Fact]
    public void A_call_one_cent_in_the_money_is_assigned_and_takes_the_shares()
    {
        var machine = Machine();
        var holding = Resolve(close: 48.90m).State;
        var written = machine.WriteCall(
            holding, MondayAfter, Call(52.50m, SecondExpiry), credit: 69.35m).State;

        var resolved = machine.Advance(written, Session(SecondExpiry, close: 52.51m));

        Assert.Equal(TrialCloseKind.CalledAway, resolved.State.CloseKind);
        Assert.Equal(LedgerEntryKind.CallAway, Assert.Single(resolved.Entries).Kind);

        var atTheStrike = machine.Advance(written, Session(SecondExpiry, close: 52.50m));

        Assert.Equal(PositionState.HoldingShares, atTheStrike.State.State);
    }

    private static Transition Resolve(decimal close) =>
        Machine().Advance(OpenedTrial(), Session(FirstExpiry, close));
}
