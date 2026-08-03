using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a corporate-action kind, being OCC's enumeration of what
/// adjusts a contract [D-W47].
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape for the same reason: an enum's
/// <c>ToString</c> yields <c>Split</c> where the store says <c>split</c>, and
/// <c>NonOrdinaryDividend</c> is unreachable from any casing of
/// <c>non_ordinary_dividend</c>, so a derivation would have to be a mapping,
/// which is what this is.
/// <para>
/// <b>The permitted values are declared, not derived from the enum's
/// spelling</b>, and from 3.3 the database enforces them too: migration 6 rebuilt
/// <c>corporate_actions</c> to add the <c>CHECK</c> it went without while the
/// vocabulary was one value. The two are asserted to agree rather than assumed
/// to, because a stored form the database and the code both name is a stored form
/// they can disagree about.
/// </para>
/// </remarks>
public static class StoreCorporateActionKind
{
    public const string OrdinaryDividend = "ordinary_dividend";

    public const string NonOrdinaryDividend = "non_ordinary_dividend";

    public const string Split = "split";

    public const string RightsOffering = "rights_offering";

    public const string Reorganization = "reorganization";

    public const string Merger = "merger";

    public const string Liquidation = "liquidation";

    public const string SpinOff = "spin_off";

    public static string ToStored(CorporateActionKind kind) => kind switch
    {
        CorporateActionKind.OrdinaryDividend => OrdinaryDividend,
        CorporateActionKind.NonOrdinaryDividend => NonOrdinaryDividend,
        CorporateActionKind.Split => Split,
        CorporateActionKind.RightsOffering => RightsOffering,
        CorporateActionKind.Reorganization => Reorganization,
        CorporateActionKind.Merger => Merger,
        CorporateActionKind.Liquidation => Liquidation,
        CorporateActionKind.SpinOff => SpinOff,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            $"'{kind}' is not a corporate-action kind. This is most likely an uninitialised "
            + "value: the enumeration deliberately does not start at zero."),
    };

    public static CorporateActionKind ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            OrdinaryDividend => CorporateActionKind.OrdinaryDividend,
            NonOrdinaryDividend => CorporateActionKind.NonOrdinaryDividend,
            Split => CorporateActionKind.Split,
            RightsOffering => CorporateActionKind.RightsOffering,
            Reorganization => CorporateActionKind.Reorganization,
            Merger => CorporateActionKind.Merger,
            Liquidation => CorporateActionKind.Liquidation,
            SpinOff => CorporateActionKind.SpinOff,
            _ => throw new FormatException(
                $"'{stored}' is not a stored corporate-action kind. The permitted values are "
                + "OCC's own enumeration of what adjusts a contract, lower case with "
                + "underscores, and the store's CHECK carries the same list [D-W47]."),
        };
    }
}
