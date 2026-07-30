using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a corporate-action kind, being <c>split</c> for now.
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape for the same reason: an enum's
/// <c>ToString</c> yields <c>Split</c> where the store says <c>split</c>.
/// <para>
/// <b>The permitted values are declared, not derived from the enum's
/// spelling</b>, and the vocabulary is one entry deliberately: the fuller set
/// and whether the table gains a CHECK is Phase 3's dividend decision
/// [<see cref="CorporateActionKind"/>].
/// </para>
/// </remarks>
public static class StoreCorporateActionKind
{
    public const string Split = "split";

    public static string ToStored(CorporateActionKind kind) => kind switch
    {
        CorporateActionKind.Split => Split,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            $"'{kind}' is not a corporate-action kind this checkpoint records. The stored "
            + $"form is '{Split}'; the fuller vocabulary is Phase 3's."),
    };

    public static CorporateActionKind ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            Split => CorporateActionKind.Split,
            _ => throw new FormatException(
                $"'{stored}' is not a stored corporate-action kind. The permitted value is "
                + $"'{Split}', lower case; the fuller vocabulary is Phase 3's."),
        };
    }
}
