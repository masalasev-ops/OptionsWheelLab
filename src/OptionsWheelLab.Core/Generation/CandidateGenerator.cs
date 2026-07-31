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
/// The gate itself is not here yet. 2.3 adds the contract constraints, 2.4 the
/// portfolio ones and 2.5 the feasible set; the gate lives inside this component
/// when it arrives [D-W10], so the survivors are what all three makers receive.
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

    public CandidateGenerator(AsOfMembership membership, AsOfMarketData marketData)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(marketData);

        _membership = membership;
        _marketData = marketData;
    }

    /// <summary>
    /// The contracts sellable on <paramref name="symbol"/> at
    /// <paramref name="simulatedDate"/> given <paramref name="state"/>, in
    /// contract identity order, empty when the name was not a member or the
    /// state makes nothing sellable.
    /// </summary>
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

        // Membership is state, not a filter [D-W9]. Asked before the chain
        // because a non-member enumerates nothing whatever the chain holds, and
        // asked on both axes: which date is being asked about, and what was
        // known then.
        if (!_membership.WasMemberOn(symbol, date: simulatedDate, asOf: simulatedDate))
        {
            return [];
        }

        var sellable = SellableRight(state);

        if (sellable is null)
        {
            return [];
        }

        var quotes = _marketData.QuotesFor(
            symbol, snapshotDate: simulatedDate, asOf: simulatedDate);

        return
        [
            .. quotes
                .Where(quote => quote.Contract.Right == sellable)
                .Select(quote => new EnumeratedCandidate(quote))
        ];
    }

    /// <summary>
    /// Which right a state makes sellable, or null when it makes none.
    /// </summary>
    /// <remarks>
    /// Cash sells puts and shares sell calls: that is the wheel, and the
    /// ownership constraint is what keeps assignment a designed leg rather than
    /// an exit to be avoided [D-W16]. The call side is the one D-W19 presupposes
    /// when it binds a covered call strike against gross basis.
    /// <para>
    /// <b>A short leg enumerates nothing, and that is a gap rather than a
    /// rule.</b> Rolling is permitted and bounded [D-W14], but no document states
    /// which contracts a roll enumerates; the bounds are Phase 3's and Phase 2's
    /// detail names no roll. Enumerating a guess here would put an unrecorded
    /// rule into the decision path, so this returns nothing until the rule is
    /// written. The test asserting that is expected to fail the day Phase 3
    /// writes it, which is the right moment to be told.
    /// </para>
    /// <para>
    /// Nothing about basis or moneyness happens here. D-W19's constraint on a
    /// call strike is the gate's, which is why FX-GrossBasisBindsCallStrike is
    /// registered against 2.4 and not against this checkpoint.
    /// </para>
    /// </remarks>
    private static OptionRight? SellableRight(PositionState state) => state switch
    {
        PositionState.Cash => OptionRight.Put,
        PositionState.HoldingShares => OptionRight.Call,
        PositionState.ShortPut or PositionState.ShortCall => null,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            $"'{state}' is not a position state. This is most likely an uninitialised value, "
            + "which would otherwise enumerate puts against an account holding shares."),
    };
}
