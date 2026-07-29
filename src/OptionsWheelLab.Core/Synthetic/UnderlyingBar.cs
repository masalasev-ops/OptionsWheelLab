using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Synthetic;

/// <summary>
/// One session's bar for an underlying, as a synthetic chain can express it.
/// </summary>
/// <remarks>
/// Mirrors the fields of <c>underlying_bars</c> [DATA_AND_SCHEMA §4.1] minus
/// <c>observed_at</c>, which is the store's stamp rather than an observation and
/// is applied at Phase 1 from a clock read at an entry point [D-W30].
/// <para>
/// <b>Only the close is required, and the rest are absent rather than zero.</b>
/// <c>WORKED_EXAMPLE.md</c> §5 supplies closes and nothing else. A zero open is a
/// meaningful and false observation, so a chain that does not state one says
/// nothing rather than saying zero.
/// </para>
/// </remarks>
public sealed record UnderlyingBar(
    Ticker Symbol,
    DateOnly SessionDate,
    decimal Close,
    decimal? Open = null,
    decimal? High = null,
    decimal? Low = null,
    decimal? AdjustedClose = null,
    long? Volume = null);
