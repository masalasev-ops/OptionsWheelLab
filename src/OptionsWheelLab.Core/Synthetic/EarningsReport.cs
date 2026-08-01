using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Core.Synthetic;

/// <summary>
/// One scheduled report on one date, as a synthetic chain can express it.
/// </summary>
/// <remarks>
/// Mirrors <c>earnings_calendar</c> [DATA_AND_SCHEMA §4.1] minus
/// <c>observed_at</c> and minus the symbol, which the enclosing
/// <see cref="SyntheticChain"/> carries once for the whole scenario, the way
/// <see cref="UnderlyingBar"/> does not because a bar is keyed on its own
/// symbol in the store.
/// <para>
/// The session is required rather than optional, because the column is
/// <c>NOT NULL</c> and <see cref="EarningsSession.Unspecified"/> is the stated
/// way to say the vendor gave none. An optional property here would produce two
/// spellings of one absence.
/// </para>
/// </remarks>
public sealed record EarningsReport(DateOnly ReportDate, EarningsSession Session);
