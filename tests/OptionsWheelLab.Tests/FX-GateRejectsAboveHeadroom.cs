using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-GateRejectsAboveHeadroom: candidates breaching per-name headroom are
/// rejected with a reason.
/// </summary>
/// <remarks>
/// The cap is structural and outside what the learner may propose [D-W11],
/// because the available sample almost certainly contains no crash and any
/// conclusion drawn about tail risk comes from data that lacks the tail. Risk the
/// sample cannot price is controlled by structure.
/// <para>
/// <b>Every case here carries a non-zero book.</b> A cap tested against an empty
/// portfolio passes whether or not it works, which is 1.1's empty-table shape, so
/// the exposure is stated and one test turns it off to show the constraint sees
/// it.
/// </para>
/// <para>
/// The book is WORKED_EXAMPLE §1's: 19,900.00 already committed in this name
/// against a cap of 25,000.00, which is the 5,100.00 headroom §3's verdicts rest
/// on.
/// </para>
/// </remarks>
public sealed class FX_GateRejectsAboveHeadroom
{
    /// <summary>WORKED_EXAMPLE §1's opening state.</summary>
    private static readonly BookState Book = new(
        CommittedInName: 19_900.00m,
        CommittedTotal: 38_000.00m);

    /// <summary>
    /// 5,250.00 committed against 5,100.00 of headroom. §3's 52.50.
    /// </summary>
    private const decimal Above = 52.50m;

    /// <summary>
    /// 5,000.00 committed, inside the headroom by 100.00. §3's 50.00.
    /// </summary>
    private const decimal Within = 50.00m;

    /// <summary>
    /// Both directions on one chain, so neither passes for want of a
    /// counterexample.
    /// </summary>
    /// <remarks>
    /// Everything else about both quotes passes, so a reason other than the
    /// per-name cap means the constraint leaked.
    /// </remarks>
    [Fact]
    public void A_candidate_above_the_headroom_is_rejected_and_one_within_it_is_not()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(Above), GateScenario.Quote(Within)],
            book: Book);

        Assert.Equal([GateReason.PerNameCap], verdicts[Above]);
        Assert.Empty(verdicts[Within]);
    }

    /// <summary>
    /// The cap rejects capital that exceeds the headroom, not capital that
    /// reaches it.
    /// </summary>
    /// <remarks>
    /// 51.00 commits exactly 5,100.00 and takes the name to its cap; 51.50
    /// commits 5,150.00 and passes it. The pair brackets the bound from both
    /// sides rather than asserting one point, and it is the same comparison the
    /// spread cap and the delta ceiling make [D-W22, D-W23].
    /// </remarks>
    [Fact]
    public void The_headroom_brackets_from_both_sides()
    {
        var verdicts = GateScenario.Gate(
            [GateScenario.Quote(51.00m), GateScenario.Quote(51.50m)],
            book: Book);

        Assert.Empty(verdicts[51.00m]);
        Assert.Equal([GateReason.PerNameCap], verdicts[51.50m]);
    }

    /// <summary>
    /// The book reaches the constraint, shown by removing it.
    /// </summary>
    /// <remarks>
    /// The same candidate that breaches against §1's book is admitted against an
    /// empty one, so the rejection above is the exposure acting rather than the
    /// strike. Without this, a cap comparing committed capital against the whole
    /// cap and ignoring the book would pass every other assertion in this file
    /// that matters: 5,250.00 is under 25,000.00.
    /// </remarks>
    [Fact]
    public void The_same_candidate_is_admitted_against_an_empty_book()
    {
        var verdicts = GateScenario.Gate([GateScenario.Quote(Above)], book: BookState.Empty);

        Assert.Empty(verdicts[Above]);
    }

    /// <summary>
    /// §3's two rejected strikes, on the document's own chain.
    /// </summary>
    /// <remarks>
    /// WORKED_EXAMPLE §10 registers this fixture against the claim that 52.50
    /// and 55.00 are rejected with the reason recorded. Both also breach the
    /// delta ceiling, which is why they carry two reasons rather than one and
    /// why the assertion is containment rather than equality [D-W22].
    /// </remarks>
    [Fact]
    public void Section_threes_two_rejected_strikes_carry_the_cap()
    {
        var verdicts = GateScenario.Gate(
            [
                GateScenario.Quote(50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
                GateScenario.Quote(52.50m, bid: 2.05m, ask: 2.20m, delta: -0.44m),
                GateScenario.Quote(55.00m, bid: 3.60m, ask: 3.85m, delta: -0.62m),
            ],
            book: Book);

        Assert.Equal(
            [GateReason.DeltaCeiling, GateReason.PerNameCap], verdicts[52.50m]);

        Assert.Equal(
            [GateReason.DeltaCeiling, GateReason.PerNameCap], verdicts[55.00m]);

        Assert.Empty(verdicts[50.00m]);
    }
}
