using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of an earnings session, being <c>before_open</c>,
/// <c>after_close</c> or <c>unspecified</c> [DATA_AND_SCHEMA §4.1].
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape. The vocabulary is stated in §4.1 as
/// of v1.32.0 and was stated nowhere before it: the column has been
/// <c>NOT NULL</c> since migration 3 with no document naming what could go in
/// it, which a writer discovers the moment it needs a value.
/// <para>
/// <b>Declared, not derived.</b> No casing of <c>BeforeOpen</c> produces
/// <c>before_open</c>, so a derivation would have to be a mapping.
/// </para>
/// </remarks>
public static class StoreEarningsSession
{
    public const string BeforeOpen = "before_open";

    public const string AfterClose = "after_close";

    public const string Unspecified = "unspecified";

    public static string ToStored(EarningsSession session) => session switch
    {
        EarningsSession.BeforeOpen => BeforeOpen,
        EarningsSession.AfterClose => AfterClose,
        EarningsSession.Unspecified => Unspecified,
        _ => throw new ArgumentOutOfRangeException(
            nameof(session),
            session,
            $"'{session}' is not an earnings session. The stored form is '{BeforeOpen}', "
            + $"'{AfterClose}' or '{Unspecified}'."),
    };

    public static EarningsSession ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            BeforeOpen => EarningsSession.BeforeOpen,
            AfterClose => EarningsSession.AfterClose,
            Unspecified => EarningsSession.Unspecified,
            _ => throw new FormatException(
                $"'{stored}' is not a stored earnings session. The permitted values are "
                + $"'{BeforeOpen}', '{AfterClose}' and '{Unspecified}', lower case."),
        };
    }
}
