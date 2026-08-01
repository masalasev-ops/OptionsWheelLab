namespace OptionsWheelLab.Core.MarketData;

/// <summary>
/// When in the trading session a scheduled report lands
/// [DATA_AND_SCHEMA §4.1].
/// </summary>
/// <remarks>
/// <b>Carried, and not read by the constraint that consumes this table.</b>
/// D-W25's buffer is measured in days, so it is indifferent to the hour. The
/// column exists because a narrower buffer would need it, and because a vendor
/// that supplies the fact should not have it discarded on the way in.
/// <para>
/// <see cref="Unspecified"/> is the honest value when the vendor gives none, and
/// is not a default standing in for a guess. It is a stated absence, which is
/// why it is a member rather than a null.
/// </para>
/// <para>
/// <b>Deliberately not starting at zero</b>, on <see cref="Identity.OptionRight"/>'s
/// precedent. Here the default would read as <see cref="BeforeOpen"/>, which is
/// a claim about a report nobody made.
/// </para>
/// </remarks>
public enum EarningsSession
{
    BeforeOpen = 1,
    AfterClose = 2,
    Unspecified = 3,
}
