namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// Reading a ledger as of a session, which is the point-in-time discipline
/// applied to the account itself [D-W39].
/// </summary>
/// <remarks>
/// <b>Every read filters on <c>known_on</c> and not on <c>entry_date</c>.</b> An
/// entry's two dates are the session it occurred in and the session the account
/// could act on it, and a decision made on a session may only see the second
/// [D-W39, D-W40]. Filtering on the wrong one is the leak this exists to close,
/// and it is the kind that produces plausible numbers.
/// <para>
/// A pure function over entries, so a caller with a ledger in hand needs no store
/// and the rule has one definition rather than one per query.
/// </para>
/// </remarks>
public static class LedgerReading
{
    /// <summary>
    /// The cash a trial can act on as of <paramref name="asOf"/>.
    /// </summary>
    /// <remarks>
    /// Proceeds from an assignment or a call-away settle on the first business
    /// day after the session the exercise occurred in, which is the session the
    /// account first learns of it [D-W40], so they are absent from this total on
    /// the session they were earned and present on the next.
    /// </remarks>
    public static decimal CashKnownOn(IReadOnlyList<LedgerEntry> entries, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .Where(entry => entry.KnownOn <= asOf)
            .Sum(entry => entry.Amount);
    }

    /// <summary>
    /// The state a decision on <paramref name="asOf"/> may read.
    /// </summary>
    /// <remarks>
    /// The last state that had taken effect by then, which on the session of an
    /// assignment is the state before it: no decision made on a session may
    /// depend on an assignment that occurred on it [D-W39].
    /// </remarks>
    public static TrialState AsKnownOn(IReadOnlyList<TrialState> states, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(states);

        var known = states.LastOrDefault(state => state.EffectiveFrom <= asOf);

        return known
            ?? throw new InvalidOperationException(
                $"No state had taken effect by {asOf:yyyy-MM-dd}, so there is nothing a "
                + "decision on that session could have read. The trial opened later.");
    }
}
