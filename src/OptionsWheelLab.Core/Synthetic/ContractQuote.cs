using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Synthetic;

/// <summary>
/// One contract's quote on one snapshot date, as a synthetic chain can express
/// it.
/// </summary>
/// <remarks>
/// Mirrors <c>contract_quotes</c> [DATA_AND_SCHEMA §4.1] minus
/// <c>observed_at</c>, and keyed on the identity rather than on a
/// <c>contract_id</c>, which is a store surrogate that does not exist until
/// Phase 1 writes one.
/// <para>
/// <b>Bid and ask are required; everything else is absent rather than zero.</b>
/// <c>WORKED_EXAMPLE.md</c> §2 supplies bid, ask and delta, and a gamma of zero
/// would be a false observation rather than a missing one.
/// </para>
/// <para>
/// <b>Delta carries the sign the chain states.</b> §2 writes a put's delta as
/// <c>-0.24</c> while §1 and §4 quote the same quantity unsigned, so a sign rule
/// here would bake that disagreement into the loader. The ceiling compares
/// absolute delta [D-W23], so the sign is the loader's to preserve and the
/// gate's to drop.
/// </para>
/// <para>
/// The vendor symbol and the multiplier are not here. They belong to
/// <see cref="Contract"/>, which is what Phase 1 writes to <c>contracts</c>; a
/// synthetic chain expresses what was quoted, not the store's record of the
/// instrument.
/// </para>
/// </remarks>
public sealed record ContractQuote(
    ContractIdentity Contract,
    DateOnly SnapshotDate,
    decimal Bid,
    decimal Ask,
    decimal? Last = null,
    long? Volume = null,
    long? OpenInterest = null,
    decimal? ImpliedVolatility = null,
    decimal? Delta = null,
    decimal? Gamma = null,
    decimal? Theta = null,
    decimal? Vega = null);
