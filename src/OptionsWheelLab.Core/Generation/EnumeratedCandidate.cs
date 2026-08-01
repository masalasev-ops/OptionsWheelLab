using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// One contract the position state makes sellable on a simulated date, before
/// the gate has looked at it.
/// </summary>
/// <remarks>
/// <b>Not the <c>candidates</c> row</b> [DATA_AND_SCHEMA §4.3]. That table is
/// Phase 4's, keyed on a decision, and nothing persists at 2.2.
/// <para>
/// <b>It carries the quote and declines the economics, deliberately.</b> None of
/// 2.3's four constraint families needs anything else: the spread cap, the
/// premium floor and the delta ceiling read bid, ask and delta off the quote,
/// and the expiry window reads a date off the identity. What the row carries and
/// this does not is <c>contracts_qty</c>, <c>committed_capital</c>, <c>credit</c>
/// and <c>feature_json</c>, and only 2.4's caps need the first two. The quantity
/// that computes committed capital is the open Phase 3 obligation: D-W17's first
/// paragraph says the contract multiplier and its third says the deliverable,
/// and they differ for an adjusted contract. Building the economics here means
/// choosing between them at the checkpoint with no reason to, three checkpoints
/// before the obligation that settles it.
/// </para>
/// <para>
/// <b>2.4 needed committed capital and still did not put it here.</b> It is a
/// function of the identity this record already carries, so storing it would be
/// a second copy of a derived fact, and the obligation is better served by one
/// computing site than by one field: see <see cref="CommittedCapital"/>, which
/// is what Phase 3 changes.
/// </para>
/// <para>
/// <b>The quote rather than the identity alone</b>, because 2.3 reads it four
/// times over and re-reading the chain per constraint would put one read on four
/// paths. <see cref="ContractQuote"/> already carries the identity and the
/// snapshot date, so neither is a second copy here.
/// </para>
/// <para>
/// <b>No action and no copy of the position state.</b> Which action a candidate
/// stands for is a function of the right, which the identity carries, and the
/// state the caller passed in. 1.5 is the precedent: the deliverable left
/// <see cref="Identity.Contract"/> when identity gained it, because a fact in
/// two places drifts.
/// </para>
/// </remarks>
public sealed record EnumeratedCandidate(ContractQuote Quote);
