using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Synthetic;

/// <summary>
/// One hand-written scenario: an underlying, its bars, and the quotes its chains
/// carried on each snapshot date [D-W31].
/// </summary>
/// <remarks>
/// <b>Both sequences are in a deterministic order and neither is in file
/// order.</b> Bars are by session date; quotes are by snapshot date and then by
/// <see cref="ContractIdentity"/>'s total order. A hand-written file gets
/// reordered by whoever edits it, and three makers receiving byte-identical
/// candidate sets [D-W4] cannot depend on that.
/// <para>
/// Nothing here reaches the store. Phase 1 wires these to
/// <c>underlying_bars</c>, <c>chain_snapshots</c>, <c>contracts</c> and
/// <c>contract_quotes</c>, and stamps <c>observed_at</c> as it does so.
/// </para>
/// </remarks>
public sealed record SyntheticChain(
    Ticker Symbol,
    IReadOnlyList<UnderlyingBar> Bars,
    IReadOnlyList<ContractQuote> Quotes);
