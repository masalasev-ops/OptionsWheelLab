using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-OneSetThreeBooks: two makers with different books receive the same candidate
/// identities with the same contract-level reasons and different portfolio
/// reasons, from one shared evaluation [D-W52].
/// </summary>
/// <remarks>
/// <b>This is the property the record's own refusal cannot see.</b>
/// `DecisionStore` refuses a second maker presenting a set the first did not, and
/// it compares contract identities rather than verdicts, so three separate gate
/// evaluations would pass that refusal while the shared half was computed three
/// times. A property enforced by a comparison that cannot see the difference is
/// not enforced, which is why the sharing is structural from 4.5 and why this
/// fixture asserts the structure rather than the agreement.
/// <para>
/// <b>The two halves are asserted in opposite directions on purpose.</b> Equal
/// contract-level reasons is the shared half; different portfolio reasons is what
/// stops the fixture passing on a gate that ignored the book entirely. A test
/// asserting only the first would be satisfied by a gate with no caps at all.
/// </para>
/// <para>
/// <b>One evaluation, not two compared.</b> The shared pass runs once and both
/// books are applied over that one result, which is what the composition root
/// does. Running it twice and comparing would assert agreement, which is the
/// weaker claim this decision rejects.
/// </para>
/// </remarks>
public sealed class FX_OneSetThreeBooks
{
    /// <summary>
    /// A book committing enough in this name that the 50.00 put breaches the
    /// per-name cap, where an empty book does not.
    /// </summary>
    /// <remarks>
    /// The put commits 5,000.00, being strike times multiplier [D-W17]. Seeded
    /// equity is 100,000.00 at a per-name fraction of 0.25, so the cap is
    /// 25,000.00 and 21,000.00 already committed leaves 4,000.00, which the
    /// candidate breaches. Measured from `SeedValues` rather than borrowed from
    /// the worked example's own account.
    /// </remarks>
    private static readonly BookState Committed = new(21_000m, 21_000m);

    [Fact]
    public void One_evaluation_serves_two_books()
    {
        var (empty, committed) = TwoBooks();

        // The same contracts, in the same order, from one evaluation.
        Assert.Equal(
            empty.Select(candidate => candidate.Candidate.Quote.Contract),
            committed.Select(candidate => candidate.Candidate.Quote.Contract));
    }

    /// <summary>
    /// The contract-level verdicts are identical, which is the shared half.
    /// </summary>
    /// <remarks>
    /// The 45.00 quote breaches the spread cap on both sides, so the assertion is
    /// over a set where the contract half has something to say. Two empty lists
    /// would agree for the reason every vacuous check agrees.
    /// </remarks>
    [Fact]
    public void The_contract_level_verdicts_are_the_same_for_both()
    {
        var (empty, committed) = TwoBooks();

        Assert.Equal(ContractLevel(empty), ContractLevel(committed));

        Assert.Contains(
            ContractLevel(empty).SelectMany(reasons => reasons),
            reason => reason == GateReason.SpreadCap);
    }

    /// <summary>
    /// The portfolio verdicts differ, which is what a shared book would hide.
    /// </summary>
    /// <remarks>
    /// The committed book breaches the per-name cap on the 50.00 and the empty one
    /// breaches nothing [D-W11]. Without this the fixture would pass against a
    /// gate that never read a book, which is the failure the split could
    /// introduce and this is the case that would catch it.
    /// </remarks>
    [Fact]
    public void The_portfolio_verdicts_differ_where_the_books_do()
    {
        var (empty, committed) = TwoBooks();

        Assert.All(PortfolioLevel(empty), reasons => Assert.Empty(reasons));

        Assert.Contains(
            PortfolioLevel(committed).SelectMany(reasons => reasons),
            reason => reason == GateReason.PerNameCap);
    }

    /// <summary>
    /// The shared pass carries no portfolio verdict at all, whatever the book.
    /// </summary>
    /// <remarks>
    /// The direct assertion that the two halves are separated rather than merely
    /// producing separable output: what the shared evaluation returns is the
    /// contract half and nothing else, so there is no book it could have been
    /// computed against.
    /// </remarks>
    [Fact]
    public void The_shared_evaluation_carries_no_portfolio_verdict()
    {
        using var scenario = GateScenario.SharedAndBooks(Quotes(), Committed);

        Assert.All(
            scenario.Shared,
            candidate => Assert.All(
                candidate.Reasons,
                reason => Assert.True(GateReasonFamily.IsContractLevel(reason))));
    }

    private static (
        IReadOnlyList<GatedCandidate> Empty,
        IReadOnlyList<GatedCandidate> Committed) TwoBooks()
    {
        using var scenario = GateScenario.SharedAndBooks(Quotes(), Committed);

        return (scenario.Against(BookState.Empty), scenario.Against(Committed));
    }

    /// <summary>
    /// One quote the contract half refuses and one it admits, so both halves have
    /// something to say.
    /// </summary>
    private static IReadOnlyList<Core.Synthetic.ContractQuote> Quotes() =>
    [
        GateScenario.Quote(45.00m, bid: 0.10m, ask: 0.30m),
        GateScenario.Quote(50.00m),
    ];

    private static IEnumerable<IReadOnlyList<GateReason>> ContractLevel(
        IReadOnlyList<GatedCandidate> gated) =>
        gated.Select(candidate =>
            (IReadOnlyList<GateReason>)
            [.. candidate.Reasons.Where(GateReasonFamily.IsContractLevel)]);

    private static IEnumerable<IReadOnlyList<GateReason>> PortfolioLevel(
        IReadOnlyList<GatedCandidate> gated) =>
        gated.Select(candidate =>
            (IReadOnlyList<GateReason>)
            [.. candidate.Reasons.Where(reason => !GateReasonFamily.IsContractLevel(reason))]);
}
