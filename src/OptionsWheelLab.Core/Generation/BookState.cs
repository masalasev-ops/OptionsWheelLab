namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// What the book already carries, which is what the portfolio constraints ask
/// whether it can carry more of [SYSTEM_DESIGN §3.4].
/// </summary>
/// <remarks>
/// <b>A parameter, the way the constraints take their bounds.</b> Every field
/// here is a projection of <c>positions</c> [DATA_AND_SCHEMA §4.3, D-W35], which
/// is Phase 3's, so 2.4 takes them rather than reading them. That is the same
/// shape <see cref="GateBounds"/> uses for a different reason: there the point is
/// that a constraint cannot reach configuration, here it is that a constraint
/// cannot reach a table that does not exist.
/// <para>
/// <b>The gate needs current portfolio state, which is the only backward edge in
/// the daily path</b> [SYSTEM_DESIGN §3.3]. The ledger feeding back into the gate
/// is by design, and this type is where that edge arrives.
/// </para>
/// <para>
/// <b>Simultaneous-assignment exposure is not a fourth field.</b> It is
/// <see cref="CommittedTotal"/> today, derived where it is compared and stated
/// there: a cash-secured put's committed capital is its assignment exposure, and
/// every open position is one until Phase 3's state machine holds shares. A
/// field every caller sets equal to another is two chances to disagree, and the
/// derivation makes Phase 3 touch the site that states the equality rather than
/// trusting it to notice a field that has quietly been a copy.
/// </para>
/// <para>
/// <b>Two states enumerate calls and both carry a basis, which is what makes a
/// missing one a caller error.</b> Holding shares and being short a call both
/// enumerate calls from 4.4 [D-W54], and a short call is reachable only from held
/// shares, so the basis is carried forward with it. This once named holding shares
/// as the only call-bearing state, which was true when it was written and which
/// 4.4 made false; the conclusion it supports survives and the reason given for it
/// did not.
///
/// <b>A null <see cref="GrossBasis"/> means no shares are held in this name</b>,
/// which is <see cref="Positions.PositionState.Cash"/> and enumerates puts. Only
/// <see cref="Positions.PositionState.HoldingShares"/> enumerates calls, and
/// D-W19's constraint binds a call against basis, so a call candidate reaching
/// the gate with no basis is a caller error rather than a candidate to judge.
/// </para>
/// </remarks>
/// <param name="CommittedInName">
/// Capital already committed in the name being gated.
/// </param>
/// <param name="CommittedTotal">Capital already committed across all names.</param>
/// <param name="GrossBasis">
/// The gross cost basis of shares held in this name, premium tracked separately
/// [D-W19], or null when none are held.
/// </param>
public sealed record BookState(
    decimal CommittedInName,
    decimal CommittedTotal,
    decimal? GrossBasis = null)
{
    /// <summary>
    /// A book carrying nothing.
    /// </summary>
    /// <remarks>
    /// Named so a test with no book states that it has none, rather than passing
    /// zeros that read as a figure someone chose. A cap evaluated only against
    /// this passes whether or not it works, which is 1.1's empty-table shape, so
    /// every registered cap fixture supplies a book instead.
    /// </remarks>
    public static BookState Empty { get; } = new(0m, 0m);
}
