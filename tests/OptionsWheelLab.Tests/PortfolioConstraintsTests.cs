using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The cap arithmetic, tested without a store because it is a function of its
/// arguments.
/// </summary>
/// <remarks>
/// Not a registered fixture, on <see cref="ContractConstraintsTests"/>' argument:
/// each cap has its own registered check against a real chain, and what is here
/// is the behaviour those checks cannot isolate.
/// <para>
/// <b>2.4 is the first checkpoint where a contract constraint and a cap can fail
/// the same candidate</b>, so the cross-family order test lands here, with the
/// family that made it reachable. 2.5's FX-GateRecordsAllReasons asserts the
/// property that a candidate failing two constraints carries both; this asserts
/// that two families can produce it, which was untestable while only one
/// existed.
/// </para>
/// </remarks>
public sealed class PortfolioConstraintsTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Simulated = new(2026, 3, 2);
    private static readonly DateOnly Expiry = new(2026, 4, 17);

    /// <summary>The seeded caps [CONFIG_REFERENCE].</summary>
    private static readonly PortfolioBounds Seeded = new(
        Equity: 100_000.00m,
        PerNameCapFraction: 0.25m,
        TotalCapFraction: 0.60m,
        SimultaneousAssignmentLimitFraction: 0.60m);

    /// <summary>The seeded contract bounds, for the cross-family case.</summary>
    private static readonly GateBounds SeededContract = new(
        MaxSpreadFractionOfMid: 0.12m,
        MinPremium: 0.30m,
        MaxDelta: 0.35m,
        MinDte: 7,
        MaxDte: 70,
        EarningsClearanceDays: 7);

    /// <summary>WORKED_EXAMPLE §1's opening state.</summary>
    private static readonly BookState Section1 = new(
        CommittedInName: 19_900.00m,
        CommittedTotal: 38_000.00m);

    [Fact]
    public void A_candidate_the_book_can_carry_has_no_reasons()
    {
        Assert.Empty(Evaluate(Put(45.00m), Section1));
    }

    /// <summary>
    /// The seeded values as amounts, which is how an operator checks them.
    /// </summary>
    /// <remarks>
    /// A fraction is what the store holds and what an operator set, and the
    /// amount is what binds. Asserting both against §1's figures is what makes
    /// the three ratios checkable rather than only readable.
    /// </remarks>
    [Fact]
    public void The_seeded_caps_are_the_amounts_section_one_derives()
    {
        Assert.Equal(
            5_100.00m, PortfolioConstraints.PerNameHeadroom(Seeded, Section1));

        Assert.Equal(22_000.00m, PortfolioConstraints.TotalHeadroom(Seeded, Section1));
        Assert.Equal(22_000.00m, PortfolioConstraints.AssignmentHeadroom(Seeded, Section1));

        // The caps themselves, being the headroom against an empty book.
        Assert.Equal(
            25_000.00m, PortfolioConstraints.PerNameHeadroom(Seeded, BookState.Empty));

        Assert.Equal(
            60_000.00m, PortfolioConstraints.TotalHeadroom(Seeded, BookState.Empty));

        Assert.Equal(
            60_000.00m, PortfolioConstraints.AssignmentHeadroom(Seeded, BookState.Empty));
    }

    /// <summary>
    /// Two names at the full per-name cap do not reach the total, and a third
    /// cannot reach its own.
    /// </summary>
    /// <remarks>
    /// 60,000.00 over 25,000.00 is 2.4 names, so the total binds part-way
    /// through a third name rather than at a whole number of them. Asserted
    /// because the relationship between the two fractions is the thing an
    /// operator revising either one needs to see, and reading the ratios does
    /// not show it.
    /// </remarks>
    [Fact]
    public void The_total_cap_binds_partway_through_a_third_name_at_the_per_name_cap()
    {
        var twoNamesFull = new BookState(CommittedInName: 0m, CommittedTotal: 50_000.00m);

        Assert.Equal(10_000.00m, PortfolioConstraints.TotalHeadroom(Seeded, twoNamesFull));

        Assert.True(
            PortfolioConstraints.TotalHeadroom(Seeded, twoNamesFull)
                < PortfolioConstraints.PerNameHeadroom(Seeded, twoNamesFull));
    }

    /// <summary>
    /// The two account-wide headrooms are separately computed, shown at a
    /// configuration where the fractions differ.
    /// </summary>
    /// <remarks>
    /// The arithmetic half of a property whose verdict half is registered as
    /// FX-AssignmentStressRejects. Found by mutation: a limit reading
    /// <c>TotalCapFraction</c> instead of its own key passed all 490 tests,
    /// because the seeded values hold the two equal and either was readable from
    /// the other.
    /// </remarks>
    [Fact]
    public void The_two_account_wide_headrooms_are_computed_apart()
    {
        var apart = Seeded with { SimultaneousAssignmentLimitFraction = 0.30m };
        var book = new BookState(CommittedInName: 0m, CommittedTotal: 28_000.00m);

        Assert.Equal(2_000.00m, PortfolioConstraints.AssignmentHeadroom(apart, book));
        Assert.Equal(32_000.00m, PortfolioConstraints.TotalHeadroom(apart, book));
    }

    /// <summary>
    /// Every cap reason, on one candidate, in the enum's declared order.
    /// </summary>
    /// <remarks>
    /// The order is what three makers receiving byte-identical sets depends on
    /// [D-W4], so it is asserted rather than assumed.
    /// </remarks>
    [Fact]
    public void Several_breaches_come_back_in_declared_order()
    {
        var book = new BookState(
            CommittedInName: 24_000.00m,
            CommittedTotal: 59_000.00m,
            GrossBasis: 60.00m);

        var reasons = PortfolioConstraints.Evaluate(Call(55.00m), Seeded, book);

        Assert.Equal(
            [
                GateReason.PerNameCap,
                GateReason.TotalCap,
                GateReason.AssignmentStress,
                GateReason.GrossBasis,
            ],
            reasons);

        Assert.Equal(reasons.OrderBy(reason => reason), reasons);
    }

    /// <summary>
    /// A contract reason and a cap reason on one candidate, in declared order.
    /// </summary>
    /// <remarks>
    /// WORKED_EXAMPLE §3's 52.50, which breaches the delta ceiling at 0.44
    /// against a 0.35 ceiling and commits 5,250.00 against 5,100.00 of headroom.
    /// The two families are evaluated separately and appended, so this is where
    /// the seam between them would show if either produced its reasons out of
    /// order or the concatenation reversed them.
    /// </remarks>
    [Fact]
    public void A_contract_reason_and_a_cap_reason_arrive_in_declared_order()
    {
        var candidate = Put(52.50m, bid: 2.05m, ask: 2.20m, delta: -0.44m);

        IReadOnlyList<GateReason> reasons =
        [
            .. ContractConstraints.Evaluate(candidate.Quote, Simulated, SeededContract, []),
            .. PortfolioConstraints.Evaluate(candidate, Seeded, Section1),
        ];

        Assert.Equal([GateReason.DeltaCeiling, GateReason.PerNameCap], reasons);
        Assert.Equal(reasons.OrderBy(reason => reason), reasons);
    }

    /// <summary>
    /// Committed capital is strike times the multiplier, and an adjusted
    /// deliverable does not move it.
    /// </summary>
    /// <remarks>
    /// Inverted at 3.3 from what 2.4 read, per [D-W17] as amended at 3.1. A
    /// standard contract cannot tell the two quantities apart, both being one
    /// hundred, so the adjusted case is the whole assertion: a 60 strike with a
    /// 150-share deliverable commits 6,000.00, where reading the deliverable
    /// would give 9,000.00 and misprice the position.
    /// </remarks>
    [Fact]
    public void Committed_capital_reads_the_multiplier()
    {
        Assert.Equal(5_000.00m, CommittedCapital.For(Identity(50.00m, OptionRight.Put)));

        Assert.Equal(
            6_000.00m,
            CommittedCapital.For(
                ContractIdentity.Of(Symbol, Expiry, OptionRight.Put, 60.00m, 150)));

        Assert.Equal(
            10_000.00m, CommittedCapital.For(Identity(50.00m, OptionRight.Put), contracts: 2));
    }

    /// <summary>
    /// The strike reaches the cap and the deliverable does not.
    /// </summary>
    /// <remarks>
    /// Inverted at 3.3 with the quantity it asserts [D-W17, as amended]. The
    /// discriminating power is unchanged and points the other way: an adjusted
    /// contract at the same strike is admitted exactly as the standard one is,
    /// which fails the moment anything reads the deliverable again, and a higher
    /// strike on a standard deliverable is refused, which fails if the cap reads
    /// no quantity at all. The headroom is §1's 5,100.00, so 50.00 commits 5,000
    /// and clears it either way while 55.00 commits 5,500 and does not.
    /// </remarks>
    [Fact]
    public void The_strike_reaches_the_cap_and_the_deliverable_does_not()
    {
        var book = new BookState(CommittedInName: 19_900.00m, CommittedTotal: 19_900.00m);

        Assert.Empty(PortfolioConstraints.Evaluate(Put(50.00m), Seeded, book));

        var adjusted = new EnumeratedCandidate(
            new ContractQuote(
                ContractIdentity.Of(Symbol, Expiry, OptionRight.Put, 50.00m, 150),
                Simulated,
                0.95m,
                1.01m,
                Delta: -0.24m));

        Assert.Empty(PortfolioConstraints.Evaluate(adjusted, Seeded, book));

        Assert.Equal(
            [GateReason.PerNameCap],
            PortfolioConstraints.Evaluate(Put(55.00m), Seeded, book));
    }

    /// <summary>
    /// A put is never compared against basis, whatever the book holds.
    /// </summary>
    [Fact]
    public void A_put_is_not_bound_by_gross_basis()
    {
        var book = new BookState(0m, 0m, GrossBasis: 60.00m);

        Assert.Empty(PortfolioConstraints.Evaluate(Put(45.00m), Seeded, book));
    }

    /// <summary>
    /// A call with no basis stops rather than resolving either way.
    /// </summary>
    [Fact]
    public void A_call_with_no_basis_stops_the_evaluation()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => PortfolioConstraints.Evaluate(Call(55.00m), Seeded, BookState.Empty));

        Assert.Contains("no gross basis", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("does not guess", thrown.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<GateReason> Evaluate(EnumeratedCandidate candidate, BookState book) =>
        PortfolioConstraints.Evaluate(candidate, Seeded, book);

    private static EnumeratedCandidate Put(
        decimal strike,
        decimal bid = 0.95m,
        decimal ask = 1.01m,
        decimal? delta = -0.24m) =>
        new(new ContractQuote(
            Identity(strike, OptionRight.Put), Simulated, bid, ask, Delta: delta));

    private static EnumeratedCandidate Call(decimal strike) =>
        new(new ContractQuote(
            Identity(strike, OptionRight.Call), Simulated, 0.95m, 1.01m, Delta: 0.24m));

    private static ContractIdentity Identity(decimal strike, OptionRight right) =>
        ContractIdentity.Of(Symbol, Expiry, right, strike);
}
