using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-GrossBasisBindsCallStrike: a call strike admitted by net basis and refused
/// by gross basis is refused.
/// </summary>
/// <remarks>
/// Netting premium into basis permits call strikes below the cash outlay, which
/// caps recovery below entry and lets accumulated premium subsidise
/// progressively worse strike selection [D-W19]. The total stays positive while
/// the banked premium covers the gap, which is why the drift is easy to miss and
/// has to be prevented structurally rather than detected in the profit and loss.
/// <para>
/// <b>Both bases appear in one test, or it shows a rejection rather than the
/// distinction.</b> A fixture asserting only that a 49.50 call is refused passes
/// against a rule that refuses every call, and passes against a rule binding on
/// net basis if the strike happens to sit below that too. The arithmetic that
/// makes the case a case is asserted beside the verdict.
/// </para>
/// <para>
/// The figures are WORKED_EXAMPLE §6.3's: assigned 100 shares at 50.00, gross
/// basis 50.00 per share, net basis 50.00 less the 0.9435 per share of collected
/// premium, being 49.0565. That section states in prose that only strikes at or
/// above 50.00 are eligible and that a 49.50 call would have looked admissible
/// under net basis, which is the drift the rule prevents.
/// </para>
/// <para>
/// Calls enumerate only against held shares [D-W16], so every case here gates in
/// <see cref="PositionState.HoldingShares"/>. That is also why the book carries a
/// basis at all: a null one means no shares, and a call reaching the gate then is
/// a caller error rather than a candidate to judge.
/// </para>
/// </remarks>
public sealed class FX_GrossBasisBindsCallStrike
{
    /// <summary>WORKED_EXAMPLE §6.3's gross basis, premium tracked separately.</summary>
    private const decimal Gross = 50.00m;

    /// <summary>§6.3's net basis, being gross less 0.9435 of premium per share.</summary>
    private const decimal Net = 49.0565m;

    /// <summary>§6.3's own counterexample: the strike net basis would admit.</summary>
    private const decimal Below = 49.50m;

    /// <summary>The call §6.3 actually sells.</summary>
    private const decimal Above = 52.50m;

    /// <summary>The assigned position: 5,000.00 committed at the strike [D-W17].</summary>
    private static readonly BookState Book = new(
        CommittedInName: 5_000.00m,
        CommittedTotal: 5_000.00m,
        GrossBasis: Gross);

    [Fact]
    public void A_strike_net_basis_admits_and_gross_basis_rejects_is_rejected()
    {
        // The case only exists because the strike sits between the two bases.
        // Without this the verdict below could be produced by a rule binding on
        // either one.
        Assert.True(Below > Net);
        Assert.True(Below < Gross);

        var verdicts = Gate(Below, Above);

        Assert.Equal([GateReason.GrossBasis], verdicts[Below]);
        Assert.Empty(verdicts[Above]);
    }

    /// <summary>
    /// A strike exactly at gross basis is admitted [D-W19, as amended].
    /// </summary>
    /// <remarks>
    /// The decision said "above basis" and left the edge unstated until 2.4. The
    /// constraint exists to stop a call capping recovery below the cash outlay
    /// and a strike at basis recovers it exactly, so excluding it would forbid
    /// the break-even strike for no stated reason. Called away at basis the trial
    /// returns its premium and no capital loss, which is the worst outcome this
    /// constraint is meant to permit rather than the best it is meant to prevent.
    /// </remarks>
    [Fact]
    public void A_strike_exactly_at_gross_basis_is_admitted()
    {
        var verdicts = Gate(49.95m, Gross);

        Assert.Equal([GateReason.GrossBasis], verdicts[49.95m]);
        Assert.Empty(verdicts[Gross]);
    }

    /// <summary>
    /// A call with no basis stops the evaluation rather than resolving either
    /// way.
    /// </summary>
    /// <remarks>
    /// Admitting would drop D-W19 silently and rejecting would blame the strike
    /// for a book that has lost its position, which is D-W37's argument arriving
    /// through book state rather than through configuration.
    /// </remarks>
    [Fact]
    public void A_call_with_no_basis_stops_the_evaluation()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => GateScenario.Gate(
                [GateScenario.Quote(Above, right: OptionRight.Call)],
                book: new BookState(0m, 0m),
                state: PositionState.HoldingShares));

        Assert.Contains("no gross basis", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule binds calls and leaves puts alone.
    /// </summary>
    /// <remarks>
    /// A put struck below basis is the ordinary case of selling a cash-secured
    /// put after a drawdown, and D-W19's constraint is on covered call strikes.
    /// A rule comparing every strike against basis would reject it.
    /// </remarks>
    [Fact]
    public void A_put_below_the_same_basis_is_not_refused()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(Below)], book: Book, state: PositionState.Cash);

        Assert.Empty(verdicts[Below]);
    }

    private static IReadOnlyDictionary<decimal, IReadOnlyList<GateReason>> Gate(
        params decimal[] strikes) =>
        GateScenario.Gate(
            [.. strikes.Select(strike => GateScenario.Quote(strike, right: OptionRight.Call))],
            book: Book,
            state: PositionState.HoldingShares);
}
