using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-TotalCapRejectsAboveHeadroom: a candidate breaching the total
/// committed-capital cap is rejected with a reason.
/// </summary>
/// <remarks>
/// The second of D-W11's three, and the one the worked example cannot
/// demonstrate: §1 says in as many words that the per-name cap binds and the
/// total does not, so every candidate on that chain sits 16,500.00 clear of the
/// total headroom. A cap nothing reaches is a cap nothing tests, which is why
/// this fixture is authored rather than read out of that document.
/// <para>
/// <b>The book commits across names without committing much in this one</b>, so
/// the per-name cap cannot be what rejects. 57,000.00 committed in total against
/// a 60,000.00 cap leaves 3,000.00, while the name itself is empty and has its
/// whole 25,000.00 free.
/// </para>
/// <para>
/// <b>The assignment limit fires alongside and that is recorded rather than
/// worked around.</b> Both fractions are 0.60 [CONFIG_REFERENCE] and a
/// cash-secured put's committed capital is its assignment exposure, so on any
/// book this lab can hold the two caps bind together. The assertions are
/// therefore containment: this fixture owns the total cap and says the other
/// reason is expected rather than filtering it out of sight.
/// </para>
/// </remarks>
public sealed class FX_TotalCapRejectsAboveHeadroom
{
    /// <summary>
    /// 3,000.00 of total headroom and the whole per-name cap free.
    /// </summary>
    private static readonly BookState Book = new(
        CommittedInName: 0.00m,
        CommittedTotal: 57_000.00m);

    /// <summary>4,000.00 committed against 3,000.00 of headroom.</summary>
    private const decimal Above = 40.00m;

    /// <summary>2,500.00 committed, inside it by 500.00.</summary>
    private const decimal Within = 25.00m;

    [Fact]
    public void A_candidate_above_the_total_headroom_is_rejected_and_one_within_it_is_not()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(Above), GateScenario.Quote(Within)],
            book: Book);

        Assert.Contains(GateReason.TotalCap, verdicts[Above]);
        Assert.Empty(verdicts[Within]);
    }

    /// <summary>
    /// The total cap rejects on the total, not on this name's share of it.
    /// </summary>
    /// <remarks>
    /// The name is empty on the breaching book, so a per-name reason here would
    /// mean the total cap is reading the wrong figure. This is the distinction
    /// the two caps' headrooms exist to keep, since 57,000.00 committed
    /// elsewhere leaves this name entirely free.
    /// </remarks>
    [Fact]
    public void The_rejection_is_the_total_cap_and_not_the_per_name_one()
    {
        var verdicts = GateScenario.Gate([GateScenario.Quote(Above)], book: Book);

        Assert.DoesNotContain(GateReason.PerNameCap, verdicts[Above]);
    }

    /// <summary>
    /// The assignment limit binds on the same book, which is what holding the
    /// two fractions equal means when it is observed.
    /// </summary>
    /// <remarks>
    /// Stated as an assertion so the day Phase 3 separates committed capital
    /// from assignment exposure, this fails and says that 2.4 assumed otherwise.
    /// The same shape 2.2 used for the rolling states.
    /// </remarks>
    [Fact]
    public void The_assignment_limit_binds_on_the_same_book()
    {
        var verdicts = GateScenario.Gate([GateScenario.Quote(Above)], book: Book);

        Assert.Equal(
            [GateReason.TotalCap, GateReason.AssignmentStress], verdicts[Above]);
    }

    /// <summary>
    /// The cap rejects capital that exceeds the headroom, not capital that
    /// reaches it.
    /// </summary>
    /// <remarks>
    /// 30.00 commits exactly 3,000.00 and takes the account to its cap; 30.50
    /// commits 3,050.00 and passes it.
    /// </remarks>
    [Fact]
    public void The_headroom_brackets_from_both_sides()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(30.00m), GateScenario.Quote(30.50m)],
            book: Book);

        Assert.Empty(verdicts[30.00m]);
        Assert.Contains(GateReason.TotalCap, verdicts[30.50m]);
    }

    /// <summary>
    /// The book reaches the constraint, shown by removing it.
    /// </summary>
    [Fact]
    public void The_same_candidate_is_admitted_against_an_empty_book()
    {
        var verdicts = GateScenario.Gate([GateScenario.Quote(Above)], book: BookState.Empty);

        Assert.Empty(verdicts[Above]);
    }
}
