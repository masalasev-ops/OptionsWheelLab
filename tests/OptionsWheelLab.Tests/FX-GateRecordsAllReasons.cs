using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-GateRecordsAllReasons: a candidate failing two constraints carries both
/// reasons.
/// </summary>
/// <remarks>
/// The gate evaluates every constraint rather than short-circuiting, and records
/// every failing reason rather than the first [D-W22, `CLAUDE.md` §2]. That is
/// what the screens and the audit trail need: the gate's effect is auditable only
/// if what it refused travels with everything wrong with it [D-W5, D-W10], and a
/// record naming one of two grounds would make a candidate look marginal when it
/// was not.
/// <para>
/// <b>"Two reasons" has three shapes and the cheapest is not the one that
/// matters.</b> Two constraints inside one family share an evaluation and a
/// return; one from each family crosses two calls and a concatenation, which is
/// where an order or a drop would show; and the full set is what says the
/// vocabulary's declared order survives every branch. Registered wording asks for
/// two, and two of one kind would discharge it while leaving the seam untested.
/// </para>
/// <para>
/// Each case carries a candidate with fewer reasons beside the one under test, so
/// no assertion passes for want of a counterexample. The declared order is
/// asserted as the order returned, never as a set, because three makers receive
/// byte-identical sets [D-W4] and a collection whose order varied between runs
/// would defeat the guarantee the whole feasible set exists to hold.
/// </para>
/// </remarks>
public sealed class FX_GateRecordsAllReasons
{
    /// <summary>WORKED_EXAMPLE §1's opening state.</summary>
    private static readonly BookState Section1 = new(
        CommittedInName: 19_900.00m,
        CommittedTotal: 38_000.00m);

    /// <summary>
    /// Two reasons from one family, which one evaluation produces.
    /// </summary>
    /// <remarks>
    /// Bid 0.10 against ask 0.30 is a spread of 0.20 on a mid of 0.20, being 100
    /// percent against a cap of twelve, and the same bid sits below the 0.30
    /// floor. One quote, two independent grounds [D-W22], and a gate returning
    /// the first would return the spread cap alone.
    /// </remarks>
    [Fact]
    public void A_candidate_failing_two_contract_constraints_carries_both()
    {
        var verdicts = GateScenario.Gate(
        [
            GateScenario.Quote(45.00m, bid: 0.10m, ask: 0.30m),
            GateScenario.Quote(50.00m),
        ]);

        Assert.Equal(
            [GateReason.SpreadCap, GateReason.PremiumFloor], verdicts[45.00m]);

        Assert.Empty(verdicts[50.00m]);
    }

    /// <summary>
    /// One reason from each family, which crosses the seam between two calls.
    /// </summary>
    /// <remarks>
    /// WORKED_EXAMPLE §3's 52.50 on §1's book: absolute delta 0.44 against a 0.35
    /// ceiling [D-W23], and 5,250.00 committed against 5,100.00 of per-name
    /// headroom [D-W11]. The document states both, which is why this pair rather
    /// than a constructed one. 2.4 showed the case reachable when the second
    /// family arrived; this pins it.
    /// </remarks>
    [Fact]
    public void A_candidate_failing_one_of_each_family_carries_both()
    {
        var verdicts = GateScenario.Gate(
        [
            GateScenario.Quote(50.00m),
            GateScenario.Quote(52.50m, bid: 2.05m, ask: 2.20m, delta: -0.44m),
        ],
            book: Section1);

        Assert.Equal(
            [GateReason.DeltaCeiling, GateReason.PerNameCap], verdicts[52.50m]);

        Assert.Empty(verdicts[50.00m]);
    }

    /// <summary>
    /// Every reason one evaluation can produce, in the vocabulary's declared
    /// order.
    /// </summary>
    /// <remarks>
    /// Nine of the ten, and the tenth is excluded by arithmetic rather than by
    /// omission: the spread cap is only reached for an uncrossed quote, so a
    /// candidate cannot carry both it and the crossed reason. Nine is therefore
    /// the maximum, and asserting it is what makes a member appended out of order
    /// fail here rather than reach the audit trail.
    /// <para>
    /// A covered call on a book that breaches all three caps, quoted wide and
    /// cheap, deep in delta, expiring outside the window, spanning a buffered
    /// report date, and struck below the shares' gross basis.
    /// </para>
    /// <para>
    /// <b>A second call on the same book carries four of the nine</b>, which is
    /// the counterexample this case would otherwise lack. No candidate on a book
    /// this far past its caps can carry none, so the pair brackets by degree
    /// rather than by pass and fail: the four it drops are the four its own
    /// quote and strike do not breach, which is the gate discriminating rather
    /// than a book condemning everything on it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_reason_one_evaluation_can_produce_arrives_in_declared_order()
    {
        var verdicts = Breaching();

        Assert.Equal(
            [
                GateReason.SpreadCap,
                GateReason.PremiumFloor,
                GateReason.DeltaCeiling,
                GateReason.ExpiryWindow,
                GateReason.EarningsClearance,
                GateReason.PerNameCap,
                GateReason.TotalCap,
                GateReason.AssignmentStress,
                GateReason.GrossBasis,
            ],
            verdicts[30.00m]);

        // Above basis, inside the delta ceiling and the expiry window, and
        // quoted tightly enough to clear the cap and the floor. What is left is
        // the three caps and the report date, neither of which a strike can
        // avoid on this book and this chain.
        Assert.Equal(
            [
                GateReason.EarningsClearance,
                GateReason.PerNameCap,
                GateReason.TotalCap,
                GateReason.AssignmentStress,
            ],
            verdicts[70.00m]);
    }

    /// <summary>
    /// The order returned is the vocabulary's, not the order the families were
    /// called in and not an accident of the collection.
    /// </summary>
    /// <remarks>
    /// Asserted against the enum's own declaration rather than against the list
    /// above, so the two cannot agree by having been written from each other.
    /// </remarks>
    [Fact]
    public void The_reasons_come_back_in_the_vocabularys_declared_order()
    {
        var reasons = Breaching()[30.00m];

        Assert.Equal(reasons.OrderBy(reason => reason), reasons);
        Assert.Equal(reasons.Distinct(), reasons);

        // Nine of ten, the crossed reason being unreachable beside the spread
        // cap. A vocabulary that grew without this fixture growing would leave
        // the maximum unasserted rather than wrong.
        Assert.Equal(Enum.GetValues<GateReason>().Length - 1, reasons.Count);
    }

    /// <summary>
    /// Two covered calls on a book past all three caps, one breaching
    /// everything reachable and one breaching only what the book forces.
    /// </summary>
    private static IReadOnlyDictionary<decimal, IReadOnlyList<GateReason>> Breaching() =>
        GateScenario.Gate(
        [
            GateScenario.Quote(
                30.00m,
                bid: 0.10m,
                ask: 5.00m,
                delta: 0.90m,
                expiry: GateScenario.Simulated.AddDays(400),
                right: OptionRight.Call),
            GateScenario.Quote(70.00m, right: OptionRight.Call),
        ],
            earnings:
            [
                new EarningsReport(
                    GateScenario.Simulated.AddDays(10), EarningsSession.AfterClose),
            ],
            book: new BookState(
                CommittedInName: 24_000.00m,
                CommittedTotal: 59_000.00m,
                GrossBasis: 60.00m),
            state: PositionState.HoldingShares);
}
