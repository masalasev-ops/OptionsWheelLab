using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// One `positions` row: a state and the span it was in force for
/// [DATA_AND_SCHEMA §4.3].
/// </summary>
/// <remarks>
/// <c>EffectiveTo</c> is null while the state is current, which is the nullable
/// close column [D-W35] names as a thing a projection may carry.
/// </remarks>
public sealed record ProjectedPosition(
    PositionState State,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int Shares,
    decimal? GrossBasis,
    decimal? NetBasis,
    ContractIdentity? Contract);

/// <summary>
/// One `trials` row and its positions, rebuilt from the ledger.
/// </summary>
/// <remarks>
/// <b><c>maker_id</c> is not here, and its absence is the finding rather than an
/// omission.</b> Every other column is recoverable from the ledger and the
/// contracts it references, both append-only. Which maker opened a trial is not:
/// it is a fact about a decision, and <c>decisions</c> carries
/// <c>maker_id</c> beside <c>trial_id</c> [§4.3] and lands at Phase 4. So a
/// complete rebuild reads the ledger and the decision record, and until the
/// second exists the projection reconstructs everything except attribution.
/// </remarks>
public sealed record ProjectedTrial(
    Ticker Symbol,
    DateOnly OpenedOn,
    DateOnly? ClosedOn,
    decimal OpenStrike,
    decimal CommittedCapital,
    int RollsUsed,
    TrialCloseKind? CloseKind,
    IReadOnlyList<ProjectedPosition> Positions);

/// <summary>
/// Rebuilds `trials` and `positions` from `ledger_entries` [D-W35].
/// </summary>
/// <remarks>
/// <b>The condition on rewriting them at all.</b> A projection may be rewritten
/// only where a test discards it, rebuilds it from its source, and gets the same
/// rows; without that test it is a rewritable table with a flattering name. This
/// is the rebuild that test runs, and it is also what proves the ledger's kind
/// vocabulary carries enough to rebuild from, which nothing else checks.
/// <para>
/// <b>It replays into <see cref="TrialState"/> rather than into a shape of its
/// own.</b> The state machine produces those and this consumes them, so what a
/// state is has one definition. A projection with its own idea of a state would
/// agree with the machine until the day it did not, and the rebuild test would
/// pass either way because it compares the projection against itself.
/// </para>
/// <para>
/// <b>It needs the trial bounds, and that is a coupling worth naming.</b> A
/// buy-back that ends a trial is <c>bought_to_close</c> whether the bound forced
/// it or a maker chose it [D-W48], so telling <see cref="TrialCloseKind.ClosedAtBound"/>
/// from <see cref="TrialCloseKind.ClosedByChoice"/> means asking whether a bound
/// had been reached. The bounds are configuration, so a rebuild resolves them as
/// of the simulated date the original run used and never as-now [D-W26]. A
/// rebuild reading current configuration would disagree with the run it is
/// rebuilding and the disagreement would look like a ledger defect.
/// </para>
/// </remarks>
public static class TrialProjection
{
    /// <summary>
    /// The states a trial passed through, in order, one per change.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the entries do not begin with the sale that opens a trial.
    /// </exception>
    public static IReadOnlyList<TrialState> Replay(
        IReadOnlyList<LedgerEntry> entries,
        TrialBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(bounds);

        if (entries.Count == 0)
        {
            throw new InvalidOperationException(
                "A trial with no ledger entries cannot be rebuilt. A trial runs from first "
                + "open through to return to cash [D-W14], and the open is a sale that "
                + "writes an entry.");
        }

        var opening = entries[0];

        if (opening.Kind is not LedgerEntryKind.PremiumReceived
            || opening.Contract is not { } put)
        {
            throw new InvalidOperationException(
                $"The first entry is a '{opening.Kind}' and a trial opens by selling a "
                + "cash-secured put [D-W16], which is a premium_received against a contract. "
                + "This ledger cannot be replayed.");
        }

        var state = TrialState.OpenShortPut(put, opening.Amount, opening.EntryDate);
        var states = new List<TrialState> { state };

        foreach (var entry in entries.Skip(1))
        {
            var next = Apply(state, entry, bounds);

            if (next is null)
            {
                continue;
            }

            state = next;
            states.Add(state);
        }

        return states;
    }

    /// <summary>
    /// The `trials` row and its `positions` rows, rebuilt from the ledger.
    /// </summary>
    public static ProjectedTrial Rebuild(
        IReadOnlyList<LedgerEntry> entries,
        TrialBounds bounds)
    {
        var states = Replay(entries, bounds);
        var last = states[^1];

        return new ProjectedTrial(
            states[0].Contract!.Underlying,
            last.OpenedOn,
            last.ClosedOn,
            states[0].Contract!.Strike,
            last.CommittedCapital,
            last.RollsUsed,
            last.CloseKind,
            Collapse(states));
    }

    /// <summary>
    /// States into spans: each is in force until the next one takes effect.
    /// </summary>
    /// <remarks>
    /// <b>States sharing an effective date collapse to the last of them</b>, which
    /// is what a roll produces: the short is bought back and another sold on one
    /// session, and the account was never in an intermediate state anyone could
    /// have observed. A span of zero length in <c>positions</c> would be a
    /// position that existed for no time, which is a row about the replay rather
    /// than about the account.
    /// <para>
    /// <b>It is also why the worked example's trial never observably holds bare
    /// shares.</b> §6.3 writes its covered call on the session the assignment
    /// becomes known, and again on the session the first call expires, so
    /// <c>holding_shares</c> begins and ends within one session both times. The
    /// shares are not lost: they are carried on the <c>short_call</c> row, which
    /// is what the account held at that session's close. This lab observes a
    /// close [D-W12], so a state that began and ended between two closes is not
    /// one it can claim to have seen.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<ProjectedPosition> Collapse(IReadOnlyList<TrialState> states)
    {
        var positions = new List<ProjectedPosition>();

        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            var effectiveTo = index + 1 < states.Count
                ? states[index + 1].EffectiveFrom
                : (DateOnly?)null;

            if (effectiveTo == state.EffectiveFrom)
            {
                continue;
            }

            positions.Add(new ProjectedPosition(
                state.State,
                state.EffectiveFrom,
                effectiveTo,
                state.Shares,
                state.GrossBasis,
                state.NetBasis,
                state.Contract));
        }

        return positions;
    }

    /// <summary>
    /// One entry applied, or null when the entry moves cash without moving the
    /// position.
    /// </summary>
    private static TrialState? Apply(TrialState state, LedgerEntry entry, TrialBounds bounds) =>
        entry.Kind switch
        {
            // A closed trial has returned to cash [D-W14] and sells nothing more.
            // Replaying this as a roll resurrected it silently, producing a trial
            // no machine wrote and a projection this rebuild would then agree with.
            LedgerEntryKind.PremiumReceived when state.IsClosed =>
                throw new InvalidOperationException(
                    $"The trial closed on {state.ClosedOn:yyyy-MM-dd} and the ledger carries a "
                    + $"premium received on {entry.EntryDate:yyyy-MM-dd}. A closed trial sells "
                    + "nothing, so this ledger cannot have been written by the state machine "
                    + "and the rebuild will not invent a trial to fit it."),

            // A credit against a contract is either the second leg of a roll or a
            // covered call, and which one the state says: only held shares can
            // carry a call [D-W16, D-W43].
            LedgerEntryKind.PremiumReceived when entry.Contract is { } contract =>
                state.State is PositionState.HoldingShares
                    ? state.ShortCallFrom(
                        entry.EntryDate, contract, state.PremiumBanked + entry.Amount)
                    : state.RolledInto(
                        entry.EntryDate, contract, state.PremiumBanked + entry.Amount),

            // The paying leg of a roll. The credit that follows it carries the
            // roll count, so this banks the debit and changes nothing else.
            LedgerEntryKind.PremiumPaid =>
                state.WithPremiumBanked(state.PremiumBanked + entry.Amount),

            // The shares are the deliverable and the basis is what was paid for
            // them, which is the aggregate exercise price [D-W17]. Reading the
            // strike as the basis is the standard case of the same arithmetic and
            // was how this was written until 3.3's review.
            LedgerEntryKind.Assignment when entry.Contract is { } put =>
                state.HoldingSharesFrom(
                    entry.KnownOn,
                    put.DeliverableShares,
                    ContractTerms.AggregateExercisePrice(put) / put.DeliverableShares,
                    state.PremiumBanked),

            LedgerEntryKind.ExpiredWorthless when entry.Contract is { Right: OptionRight.Put } =>
                state.ClosedTo(
                    entry.KnownOn,
                    entry.EntryDate,
                    TrialCloseKind.ExpiredWorthless,
                    state.PremiumBanked),

            // A call expiring leaves the shares behind, so there have to be some.
            LedgerEntryKind.ExpiredWorthless when state.GrossBasis is null =>
                throw new InvalidOperationException(
                    $"The ledger carries a call expiring on {entry.EntryDate:yyyy-MM-dd} against "
                    + "a position that never held shares, so there is no basis for it to leave "
                    + "behind. A covered call is written against held shares [D-W16, D-W19]."),

            LedgerEntryKind.ExpiredWorthless =>
                state.HoldingSharesFrom(
                    entry.KnownOn, state.Shares, state.GrossBasis!.Value, state.PremiumBanked),

            LedgerEntryKind.CallAway =>
                state.ClosedTo(
                    entry.KnownOn, entry.EntryDate, TrialCloseKind.CalledAway, state.PremiumBanked),

            LedgerEntryKind.Stopped =>
                state.ClosedTo(
                    entry.KnownOn, entry.EntryDate, TrialCloseKind.Stopped, state.PremiumBanked),

            // A buy-back that ends a trial. Which of the two close kinds it is
            // depends on whether a bound had been reached [D-W48], which is why
            // this reads them.
            LedgerEntryKind.BoughtToClose =>
                state.ClosedTo(
                    entry.KnownOn,
                    entry.EntryDate,
                    CloseKindFor(state, entry, bounds),
                    state.PremiumBanked + entry.Amount),

            // Shares sold at market. When a covered call was bought back on the
            // same session the trial is already closed and this only confirms it.
            LedgerEntryKind.SharesSold when state.IsClosed => null,

            LedgerEntryKind.SharesSold =>
                state.ClosedTo(
                    entry.KnownOn,
                    entry.EntryDate,
                    CloseKindFor(state, entry, bounds),
                    state.PremiumBanked),

            // A commission moves no position and does move the basis [D-W50].
            // Net basis is what the account paid per share and the account paid
            // the commission, so a replay that treated this as cash-only would be
            // wrong rather than coarse: WORKED_EXAMPLE 6.3 states 49.0565, and
            // ignoring the entry gives 49.05. The amount is already negative.
            LedgerEntryKind.Commission =>
                state.WithPremiumBanked(state.PremiumBanked + entry.Amount),

            // Cash that moves neither the position nor the basis: a dividend the
            // shares earned [D-W41], and the fee an exercise is charged, which is
            // a cost of the event rather than of the shares.
            _ => null,
        };

    /// <summary>
    /// Whether a bound forced the close or a maker chose it [D-W48].
    /// </summary>
    /// <remarks>
    /// Nothing writes <see cref="TrialCloseKind.ClosedByChoice"/> until Phase 4
    /// has a maker, and this reads it correctly from the day one does rather than
    /// breaking on it.
    /// </remarks>
    private static TrialCloseKind CloseKindFor(
        TrialState state,
        LedgerEntry entry,
        TrialBounds bounds)
    {
        var days = entry.EntryDate.DayNumber - state.OpenedOn.DayNumber;

        return state.RollsUsed >= bounds.MaxRolls || days >= bounds.MaxTrialDays
            ? TrialCloseKind.ClosedAtBound
            : TrialCloseKind.ClosedByChoice;
    }
}
