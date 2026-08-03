using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a fill point, being <c>bid</c> [D-W12].
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape and its argument: the permitted values
/// are declared rather than derived from a member's spelling.
/// <para>
/// <b>This is the first stored vocabulary with no <c>CHECK</c> behind it, and the
/// gap is structural rather than an omission.</b> Every other one lives in a
/// column of its own, so migration 6 and 8 could constrain them and
/// FX-StoredVocabulariesMatchTheirChecks compares each declaration against what
/// the store enforces. This one lives in <c>config_rows.value</c>, which is
/// polymorphic by design: it carries decimals for four sections, integers for
/// <c>Trial:</c>, and this word. A <c>CHECK</c> over that column would have to
/// know which key a row belongs to, which is a constraint on a pair rather than
/// on a value.
/// </para>
/// <para>
/// So the code is the only thing enforcing this vocabulary, where every other one
/// has two. That is worth stating rather than leaving to be discovered by someone
/// reading a green run of that fixture as covering every stored form.
/// </para>
/// </remarks>
public static class StoreFillPoint
{
    public const string Bid = "bid";

    public static string ToStored(FillPoint fillPoint) => fillPoint switch
    {
        FillPoint.Bid => Bid,
        _ => throw new ArgumentOutOfRangeException(
            nameof(fillPoint),
            fillPoint,
            $"'{fillPoint}' is not a fill point. The stored form is '{Bid}', and this is most "
            + "likely an uninitialised value: the enumeration deliberately does not start at "
            + "zero."),
    };

    public static FillPoint ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            Bid => FillPoint.Bid,
            _ => throw new FormatException(
                $"'{stored}' is not a stored fill point. The permitted value is '{Bid}', lower "
                + "case, and it is the only one: a sale fills at the bid, fixed in advance and "
                + "not a tunable [D-W12]. A store carrying anything else is configuration "
                + "asserting a rule the lab does not have."),
        };
    }
}
