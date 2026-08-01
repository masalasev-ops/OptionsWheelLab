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
    /// The assignment limit reads its own fraction, shown at a configuration
    /// where the two differ.
    /// </summary>
    /// <remarks>
    /// <b>Found by mutation, and it is the seeded values that hide it.</b> The
    /// limit is held equal to the total cap [CONFIG_REFERENCE], so a limit
    /// reading <c>TotalCapFraction</c> instead of its own key passed all 490
    /// tests. Nothing forbids an operator setting them apart: CONFIG_REFERENCE
    /// records no invariant between the two, deliberately, because the
    /// relationship changes at Phase 3 rather than being wrong now. So this
    /// asserts against a configuration the store does not currently hold and
    /// could, which is what makes the third cap's own key load-bearing.
    /// </remarks>
    [Fact]
    public void The_assignment_limit_reads_its_own_fraction()
    {
        var apart = Seeded with { SimultaneousAssignmentLimitFraction = 0.30m };
        var book = new BookState(CommittedInName: 0m, CommittedTotal: 28_000.00m);

        // 30,000.00 against 28,000.00 leaves 2,000.00, where the total cap's
        // 60,000.00 leaves 32,000.00 and the per-name cap is untouched.
        Assert.Equal(2_000.00m, PortfolioConstraints.AssignmentHeadroom(apart, book));
        Assert.Equal(32_000.00m, PortfolioConstraints.TotalHeadroom(apart, book));

        Assert.Equal(
            [GateReason.AssignmentStress],
            PortfolioConstraints.Evaluate(Put(25.00m), apart, book));
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
    /// Committed capital is strike times deliverable, and an adjusted
    /// deliverable moves it.
    /// </summary>
    /// <remarks>
    /// The quantity is the open Phase 3 obligation and this asserts what 2.4
    /// reads rather than settling it. A standard contract cannot show the
    /// difference, both quantities being one hundred, so the adjusted case is
    /// what makes the choice visible at all: a 60 strike with a 150-share
    /// deliverable commits 9,000.00 where the multiplier would give 6,000.00.
    /// </remarks>
    [Fact]
    public void Committed_capital_reads_the_deliverable()
    {
        Assert.Equal(5_000.00m, CommittedCapital.For(Identity(50.00m, OptionRight.Put)));

        Assert.Equal(
            9_000.00m,
            CommittedCapital.For(
                ContractIdentity.Of(Symbol, Expiry, OptionRight.Put, 60.00m, 150)));

        Assert.Equal(
            10_000.00m, CommittedCapital.For(Identity(50.00m, OptionRight.Put), contracts: 2));
    }

    /// <summary>
    /// An adjusted deliverable reaches the cap, not only the arithmetic.
    /// </summary>
    /// <remarks>
    /// The same strike admitted at a standard deliverable is refused at 150
    /// shares, so the identity's fifth component [1.5] is what the cap divides
    /// the headroom against. A cap hardcoding one hundred would pass every other
    /// assertion in this file.
    /// </remarks>
    [Fact]
    public void An_adjusted_deliverable_reaches_the_cap()
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

        Assert.Equal(
            [GateReason.PerNameCap],
            PortfolioConstraints.Evaluate(adjusted, Seeded, book));
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
