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
/// that computes committed capital was the open Phase 3 obligation, and 3.1
/// settled it as the multiplier [D-W17, as amended]. Declining the economics here
/// is what left that settlement one expression to change rather than every record
/// that had copied a figure.
/// </para>
/// <para>
/// <b>2.4 needed committed capital and still did not put it here.</b> It is a
/// function of the identity this record already carries, so storing it would be
/// a second copy of a derived fact, and the obligation is better served by one
/// computing site than by one field: see <see cref="CommittedCapital"/>, which
/// is where 3.3 changed it.
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
