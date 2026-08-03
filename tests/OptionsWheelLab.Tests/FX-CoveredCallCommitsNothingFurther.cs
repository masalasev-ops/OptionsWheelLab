using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-CoveredCallCommitsNothingFurther: a trial holding assigned shares gates a
/// call candidate against the committed capital it already carries, and the
/// per-name headroom is unchanged by the call [D-W43].
/// </summary>
/// <remarks>
/// A trial's committed capital was fixed when the put was sold [D-W17] and the
/// shares are what that capital bought, so the portfolio caps read one figure per
/// trial from open to close and it does not change when the leg changes. No
/// authority states this: it is a modelling choice about how the lab measures its
/// own exposure, and the only one of 3.1's seven mechanics with no external
/// source of any kind.
/// <para>
/// <b>Why it is the conservative reading rather than the permissive one.</b> The
/// alternative charges a call its own committed figure, which counts the same
/// capital twice in one trial and makes the per-name cap bind on a leg that ties
/// up no cash. The risk a covered call carries is that it caps recovery below the
/// outlay, and that is [D-W19]'s gross-basis constraint rather than a capital cap.
/// </para>
/// </remarks>
public sealed class FX_CoveredCallCommitsNothingFurther
{
    /// <summary>WORKED_EXAMPLE §1's caps.</summary>
    private static readonly PortfolioBounds Seeded = new(
        Equity: 100_000.00m,
        PerNameCapFraction: 0.25m,
        TotalCapFraction: 0.60m,
        SimultaneousAssignmentLimitFraction: 0.60m);

    [Fact]
    public void Writing_a_call_leaves_the_trials_committed_capital_where_the_put_fixed_it()
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        var written = machine.WriteCall(
            holding, MondayAfter, Call(52.50m, SecondExpiry), credit: 69.35m).State;

        Assert.Equal(5_000.00m, OpenedTrial().CommittedCapital);
        Assert.Equal(5_000.00m, holding.CommittedCapital);
        Assert.Equal(5_000.00m, written.CommittedCapital);
    }

    /// <summary>
    /// The per-name headroom is what it was before the call was written.
    /// </summary>
    /// <remarks>
    /// The book carries the trial's own 5,000.00 and nothing else. If a call
    /// committed a further figure the book would carry 10,250.00 and the headroom
    /// would fall, which is the double count this decision refuses.
    /// </remarks>
    [Fact]
    public void The_per_name_headroom_is_unchanged_by_the_call()
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;
        var written = machine.WriteCall(
            holding, MondayAfter, Call(52.50m, SecondExpiry), credit: 69.35m).State;

        var beforeTheCall = BookOf(holding);
        var afterTheCall = BookOf(written);

        Assert.Equal(
            PortfolioConstraints.PerNameHeadroom(Seeded, beforeTheCall),
            PortfolioConstraints.PerNameHeadroom(Seeded, afterTheCall));

        Assert.Equal(20_000.00m, PortfolioConstraints.PerNameHeadroom(Seeded, afterTheCall));
    }

    /// <summary>
    /// The call candidate is gated against the figure the trial already carries.
    /// </summary>
    /// <remarks>
    /// A 52.50 call against a gross basis of 50.00 is admissible [D-W19] and the
    /// book carries the trial's 5,000.00, so the gate returns no reason. The
    /// assertion is that the call is judged at all rather than refused for capital
    /// it does not commit.
    /// </remarks>
    [Fact]
    public void A_call_candidate_is_gated_against_the_capital_the_trial_already_carries()
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        var candidate = new EnumeratedCandidate(
            new Core.Synthetic.ContractQuote(
                Call(52.50m, SecondExpiry), MondayAfter, Bid: 0.70m, Ask: 0.78m, Delta: 0.24m));

        Assert.Empty(PortfolioConstraints.Evaluate(candidate, Seeded, BookOf(holding)));
    }

    /// <summary>
    /// The book a trial holding shares presents to the gate.
    /// </summary>
    /// <remarks>
    /// Every field is a projection of <c>positions</c> [D-W35], which is what
    /// <see cref="BookState"/> says it takes rather than reads. The gross basis is
    /// the one the assignment set, premium tracked separately [D-W19].
    /// </remarks>
    private static BookState BookOf(TrialState state) =>
        new(state.CommittedCapital, state.CommittedCapital, state.GrossBasis);
}
