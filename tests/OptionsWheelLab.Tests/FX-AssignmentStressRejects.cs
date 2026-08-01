using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-AssignmentStressRejects: a candidate breaching the simultaneous-assignment
/// limit is rejected with a reason.
/// </summary>
/// <remarks>
/// The third of D-W11's three and the one that matters, because the wheel's real
/// cash-loss event is not one bad position, it is every short put assigning
/// together in a correlated selloff while the account lacks the cash to fund them
/// [SYSTEM_DESIGN §3.4].
/// <para>
/// <b>It cannot be shown binding alone today, and that is a property of the
/// values rather than of this fixture.</b> The limit is held equal to the total
/// cap [CONFIG_REFERENCE], and a cash-secured put's committed capital is what
/// would be owed if it assigned, so assignment exposure never exceeds committed
/// capital on a book this lab can hold and the two caps bind together. A lower
/// fraction would make the total cap unreachable and a higher one would never
/// bind, which is the argument the chosen value carries.
/// </para>
/// <para>
/// What this fixture can show, and does, is that the limit is wired to an
/// exposure rather than decorative: it fires on a book that reaches it, it reads
/// the total rather than this name's share, and it stops firing when the book is
/// emptied.
/// </para>
/// </remarks>
public sealed class FX_AssignmentStressRejects
{
    /// <summary>
    /// 3,000.00 of assignment headroom, the name itself empty.
    /// </summary>
    private static readonly BookState Book = new(
        CommittedInName: 0.00m,
        CommittedTotal: 57_000.00m);

    /// <summary>
    /// A book of the same size concentrated in one name, so the per-name cap is
    /// what binds and the account-wide limits are far off.
    /// </summary>
    private static readonly BookState Concentrated = new(
        CommittedInName: 20_000.00m,
        CommittedTotal: 20_000.00m);

    /// <summary>4,000.00 committed against 3,000.00 of headroom.</summary>
    private const decimal Above = 40.00m;

    [Fact]
    public void A_candidate_above_the_assignment_headroom_is_rejected()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(Above), GateScenario.Quote(25.00m)],
            book: Book);

        Assert.Contains(GateReason.AssignmentStress, verdicts[Above]);
        Assert.Empty(verdicts[25.00m]);
    }

    /// <summary>
    /// The limit reads exposure across all names, not this name's share of it.
    /// </summary>
    /// <remarks>
    /// 20,000.00 committed and all of it in this name leaves 40,000.00 of
    /// assignment headroom, so a 4,000.00 candidate passes the limit while
    /// breaching nothing else either: the per-name headroom is 5,000.00. A limit
    /// wired to the per-name figure would reject here.
    /// </remarks>
    [Fact]
    public void The_limit_reads_the_account_rather_than_the_name()
    {
        var verdicts = GateScenario.Gate([GateScenario.Quote(Above)], book: Concentrated);

        Assert.Empty(verdicts[Above]);
    }

    /// <summary>
    /// The limit rejects exposure that exceeds the headroom, not exposure that
    /// reaches it.
    /// </summary>
    [Fact]
    public void The_headroom_brackets_from_both_sides()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(30.00m), GateScenario.Quote(30.50m)],
            book: Book);

        Assert.Empty(verdicts[30.00m]);
        Assert.Contains(GateReason.AssignmentStress, verdicts[30.50m]);
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
