using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The enumeration half of the candidate generator [SYSTEM_DESIGN §3.3]: every
/// contract that could be sold on a name on a simulated date, given the
/// position's state.
/// </summary>
/// <remarks>
/// <b>Enumeration filters on nothing but position state and membership.</b> A
/// deep in-the-money put is sellable and will be rejected by every constraint;
/// enumerating it anyway is what makes the gate's effect auditable [D-W5,
/// D-W10]. A generator that pre-filtered would produce a smaller enumerated set
/// and a smaller record of what the gate did, which is the property Phase 4's
/// decision record exists to hold. WORKED_EXAMPLE §3 is the worked case: seven
/// strikes enumerated, three feasible, and the four rejections are the lesson.
/// <para>
/// <b>The gate lives inside this component</b> [D-W10], so the survivors are
/// what all three makers receive and a difference between them is selection
/// rather than permission. Both families are here as of 2.4, the contract
/// constraints from 2.3 and the portfolio ones from 2.4; assembling the
/// survivors into an ordered feasible set and recording what was refused is
/// 2.5's.
/// </para>
/// <para>
/// <b>The simulated date reaches four parameters, and that is a choice.</b> Both
/// reads take two independent axes, which session or transition the row
/// describes and when it was observed, and 1.2 made them independent
/// deliberately. On a simulated date the generator wants the chain for that date
/// as known at that date, so it passes one date twice to each read. That is
/// correct for a simulated run and would be wrong for a backfill, where the
/// session axis moves and the knowledge axis does not, so the collapse is
/// written out at the call sites rather than left to a variable landing in two
/// parameters unremarked.
/// </para>
/// <para>
/// <b>Order is inherited, not imposed again.</b>
/// <see cref="AsOfMarketData.QuotesFor"/> returns identity order, imposed in C#
/// on parsed identities because the stored decimal form does not sort. Sorting
/// here would be a second statement of one guarantee; instead the dependency is
/// stated here and asserted at this type's own output by
/// FX-WorkedExampleEnumerates, so a read that stopped ordering fails there
/// rather than silently.
/// </para>
/// <para>
/// It holds no clock and takes its date as a parameter [D-W30], so
/// FX-ClockIsNotADateSource covers it by shape without naming it.
/// </para>
/// </remarks>
public sealed class CandidateGenerator
{
    private readonly AsOfMembership _membership;
    private readonly AsOfMarketData _marketData;
    private readonly AsOfConfiguration? _configuration;

    public CandidateGenerator(AsOfMembership membership, AsOfMarketData marketData)
        : this(membership, marketData, configuration: null)
    {
    }

    /// <summary>
    /// A generator that can gate as well as enumerate.
    /// </summary>
    /// <remarks>
    /// Configuration is the gate's dependency and not enumeration's, which is
    /// why it arrives on a second constructor rather than being required of
    /// every caller. <see cref="EnumerateFor"/> reads no bound and 2.2's callers
    /// keep working unchanged; <see cref="GateFor"/> needs all six [D-W26].
    /// </remarks>
    public CandidateGenerator(
        AsOfMembership membership,
        AsOfMarketData marketData,
        AsOfConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(marketData);

        _membership = membership;
        _marketData = marketData;
        _configuration = configuration;
    }

    /// <summary>
    /// The contracts sellable on <paramref name="symbol"/> at
    /// <paramref name="simulatedDate"/> given <paramref name="state"/>, in
    /// contract identity order, empty when the name was not a member or the chain
    /// quotes none of that right.
    /// </summary>
    /// <remarks>
    /// It said "or the state makes nothing sellable" until 4.5, which was true
    /// while two states enumerated nothing. Every state makes one right sellable
    /// from 4.4 [D-W54], so that branch was unreachable and is gone.
    /// </remarks>
    /// <remarks>
    /// A pure function of its three arguments over a fixed store: nothing else
    /// varies the answer, and a later observation is excluded by the as-of axis
    /// rather than by this method.
    /// </remarks>
    public IReadOnlyList<EnumeratedCandidate> EnumerateFor(
        Ticker symbol,
        DateOnly simulatedDate,
        PositionState state)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        return EnumerateOf(symbol, simulatedDate, SellableRight(state));
    }

    /// <summary>
    /// Every contract of one right this name could sell on this date.
    /// </summary>
    /// <remarks>
    /// The right rather than the state, because enumeration depends on nothing
    /// else about the state [D-W52]. Four states map to two rights, and a state
    /// carried this far would be a parameter the filter never reads.
    /// </remarks>
    internal IReadOnlyList<EnumeratedCandidate> EnumerateOf(
        Ticker symbol,
        DateOnly simulatedDate,
        OptionRight right)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        // Membership is state, not a filter [D-W9]. Asked before the chain
        // because a non-member enumerates nothing whatever the chain holds, and
        // asked on both axes: which date is being asked about, and what was
        // known then.
        if (!_membership.WasMemberOn(symbol, date: simulatedDate, asOf: simulatedDate))
        {
            return [];
        }

        var quotes = _marketData.QuotesFor(
            symbol, snapshotDate: simulatedDate, asOf: simulatedDate);

        return
        [
            .. quotes
                .Where(quote => quote.Contract.Right == right)
                .Select(quote => new EnumeratedCandidate(quote))
        ];
    }

    /// <summary>
    /// Every candidate <see cref="EnumerateFor"/> yields, each with the reasons
    /// the gate refused it, in contract identity order.
    /// </summary>
    /// <remarks>
    /// <b>Rejected candidates are returned, not removed.</b> The gate's effect
    /// is auditable only if what it refused travels with its reasons [D-W5,
    /// D-W10]. Assembling the feasible set out of these, ordering it and
    /// recording it is 2.5's.
    /// <para>
    /// Bounds resolve once here rather than per candidate [D-W37], so an
    /// unresolvable bound raises once for the evaluation rather than once per
    /// contract, and every candidate on this date is judged against the same
    /// numbers.
    /// </para>
    /// <para>
    /// The report dates are read once too, over the widest window any candidate
    /// on this chain could need, and narrowed per contract in memory. A read per
    /// contract would be the same rows fetched once per expiry.
    /// </para>
    /// <para>
    /// <b>The book is a required parameter rather than a defaulted one</b>
    /// [D-W11]. An omitted book would default to carrying nothing, and a cap
    /// against an empty book admits everything, so forgetting to pass one would
    /// drop three structural risk controls while every test still passed. The
    /// gate needing current portfolio state is the only backward edge in the
    /// daily path [SYSTEM_DESIGN §3.3], and it arrives here.
    /// </para>
    /// </remarks>
    public IReadOnlyList<GatedCandidate> GateFor(
        Ticker symbol,
        DateOnly simulatedDate,
        PositionState state,
        BookState book)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(book);

        return Against(
            SharedFor(symbol, simulatedDate, SellableRight(state)), simulatedDate, book);
    }

    /// <summary>
    /// The half of the gate every maker acting on this key shares [D-W52].
    /// </summary>
    /// <remarks>
    /// <b>Keyed on the right rather than the state, because that is what the
    /// evaluation depends on.</b> Four position states map to two rights
    /// [<see cref="SellableRight"/>] and
    /// <see cref="ContractConstraints.Evaluate"/> takes neither a state nor a
    /// book, so two makers whose states share a right receive the same contracts
    /// carrying the same verdicts. Computing this three times while storing it
    /// once would make [D-W4]'s property a thing three evaluations agree about
    /// rather than a thing there is one of [D-W52]. The one-ness is that
    /// decision's; what [D-W4] asks for is agreement, and it is the refusal
    /// guarding the stored set that would have to catch a failure of it, which it
    /// cannot: it compares contract identities rather than verdicts.
    /// <para>
    /// <b>Rejected candidates are returned, not removed.</b> The gate's effect
    /// is auditable only if what it refused travels with its reasons [D-W5,
    /// D-W10].
    /// </para>
    /// <para>
    /// Bounds resolve once here rather than per candidate [D-W37], so an
    /// unresolvable bound raises once for the evaluation rather than once per
    /// contract, and every candidate on this date is judged against the same
    /// numbers. The report dates are read once too, over the widest window any
    /// candidate on this chain could need, and narrowed per contract in memory.
    /// </para>
    /// </remarks>
    public IReadOnlyList<GatedCandidate> SharedFor(
        Ticker symbol,
        DateOnly simulatedDate,
        OptionRight right)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var configuration = Gating();
        var candidates = EnumerateOf(symbol, simulatedDate, right);

        if (candidates.Count == 0)
        {
            return [];
        }

        var bounds = GateBounds.ResolveFor(configuration, simulatedDate);
        var reports = ReportDatesAcross(symbol, simulatedDate, candidates, bounds);

        return
        [
            .. candidates.Select(candidate =>
            {
                var window = ContractConstraints.ClearanceWindow(
                    simulatedDate,
                    candidate.Quote.Contract.Expiry,
                    bounds.EarningsClearanceDays);

                var inWindow = reports
                    .Where(date => date >= window.From && date <= window.To)
                    .ToList();

                return new GatedCandidate(
                    candidate,
                    [
                        .. ContractConstraints.Evaluate(
                            candidate.Quote, simulatedDate, bounds, inWindow),
                    ]);
            })
        ];
    }

    /// <summary>
    /// One maker's caps applied over the shared evaluation [D-W11, D-W52].
    /// </summary>
    /// <remarks>
    /// <b>The reasons append rather than merge.</b> Contract reasons then
    /// portfolio reasons is the vocabulary's declared order and therefore the
    /// order a candidate carries them in [D-W4, <see cref="GatedCandidate"/>], so
    /// appending here produces exactly what one pass produced before the split.
    /// The record already stores the two halves apart [D-W52]; computing them
    /// together while storing them apart was the half that made a shared set a
    /// claim rather than a fact.
    /// <para>
    /// <b>The book is a required parameter rather than a defaulted one</b>
    /// [D-W11]. An omitted book would default to carrying nothing, and a cap
    /// against an empty book admits everything, so forgetting to pass one would
    /// drop three structural risk controls while every test still passed. The
    /// gate needing current portfolio state is the only backward edge in the
    /// daily path [SYSTEM_DESIGN §3.3], and it arrives here.
    /// </para>
    /// </remarks>
    public IReadOnlyList<GatedCandidate> Against(
        IReadOnlyList<GatedCandidate> shared,
        DateOnly simulatedDate,
        BookState book)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(book);

        if (shared.Count == 0)
        {
            return [];
        }

        var caps = PortfolioBounds.ResolveFor(Gating(), simulatedDate);

        return
        [
            .. shared.Select(candidate => new GatedCandidate(
                candidate.Candidate,
                [
                    .. candidate.Reasons,
                    .. PortfolioConstraints.Evaluate(candidate.Candidate, caps, book),
                ]))
        ];
    }

    /// <summary>
    /// The configuration surface, or the refusal for a generator built without
    /// one.
    /// </summary>
    private AsOfConfiguration Gating() =>
        _configuration
        ?? throw new InvalidOperationException(
            "This generator was built without configuration and can enumerate but not gate. "
            + "Every constraint reads its bound as of the simulated date [D-W26], so the "
            + "gate cannot run without a configuration surface to read.");

    /// <summary>
    /// The reports falling in the widest clearance window any of these
    /// candidates could need, read once.
    /// </summary>
    private IReadOnlyList<DateOnly> ReportDatesAcross(
        Ticker symbol,
        DateOnly simulatedDate,
        IReadOnlyList<EnumeratedCandidate> candidates,
        GateBounds bounds)
    {
        var latestExpiry = candidates.Max(candidate => candidate.Quote.Contract.Expiry);
        var widest = ContractConstraints.ClearanceWindow(
            simulatedDate, latestExpiry, bounds.EarningsClearanceDays);

        return _marketData.ReportDatesFor(
            symbol, widest.From, widest.To, asOf: simulatedDate);
    }

    /// <summary>
    /// Which right a state makes sellable.
    /// </summary>
    /// <remarks>
    /// Cash sells puts and shares sell calls: that is the wheel, and the
    /// ownership constraint is what keeps assignment a designed leg rather than
    /// an exit to be avoided [D-W16]. The call side is the one D-W19 presupposes
    /// when it binds a covered call strike against gross basis.
    /// <para>
    /// <b>A short leg enumerates the right it is short, from 4.4.</b> It returned
    /// nothing until then, because rolling was permitted and bounded [D-W14] with
    /// no document stating which contracts a roll enumerates, and enumerating a
    /// guess would have put an unrecorded rule into the decision path. [D-W54]
    /// wrote the rule, so a roll now draws from the ordinary gated set for the
    /// right the trial is short and needs no enumeration of its own.
    /// <para>
    /// The remark this replaces said the gap would close at Phase 3, and the test
    /// beside it was written to fail then. Phase 3 did not write the rule; 4.4
    /// did, two phases later than predicted, which is why that test was replaced
    /// deliberately rather than discovered red.
    /// </para>
    /// <para>
    /// [D-W52]'s stated property survives, and it was checked rather than assumed:
    /// <see cref="ContractConstraints.Evaluate"/> takes neither a state nor a
    /// book, so two makers in different states on one right are offered identical
    /// contracts carrying identical contract-level verdicts.
    /// </para>
    /// </para>
    /// <para>
    /// Nothing about basis or moneyness happens here. D-W19's constraint on a
    /// call strike is the gate's, which is why FX-GrossBasisBindsCallStrike is
    /// registered against 2.4 and not against this checkpoint.
    /// </para>
    /// </remarks>
    internal static OptionRight SellableRight(PositionState state) => state switch
    {
        PositionState.Cash => OptionRight.Put,
        PositionState.HoldingShares => OptionRight.Call,
        PositionState.ShortPut => OptionRight.Put,
        PositionState.ShortCall => OptionRight.Call,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            $"'{state}' is not a position state. This is most likely an uninitialised value, "
            + "which would otherwise enumerate puts against an account holding shares."),
    };
}
