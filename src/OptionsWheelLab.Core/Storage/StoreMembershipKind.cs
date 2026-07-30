using OptionsWheelLab.Core.Membership;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a membership transition, being <c>joined</c> or
/// <c>left</c>.
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape for the same reason: an enum's
/// <c>ToString</c> yields <c>Joined</c> where the schema says <c>joined</c>,
/// which is culture-independent, plausible, and wrong.
/// <para>
/// <b>The permitted values are declared, not derived from the enum's
/// spelling.</b> Renaming a member is a source-level change; it must not
/// silently change the stored form of every existing row. The SQL that filters
/// on a kind reads from these constants too, so the declared form has one
/// definition rather than a definition and its restatements: a filter carrying
/// its own literal would return empty rather than fail if the declared form
/// ever moved.
/// </para>
/// </remarks>
public static class StoreMembershipKind
{
    public const string Joined = "joined";

    public const string Left = "left";

    public static string ToStored(MembershipKind kind) => kind switch
    {
        MembershipKind.Joined => Joined,
        MembershipKind.Left => Left,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            $"'{kind}' is not a membership transition. The stored form is '{Joined}' or '{Left}'."),
    };

    public static MembershipKind ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            Joined => MembershipKind.Joined,
            Left => MembershipKind.Left,
            _ => throw new FormatException(
                $"'{stored}' is not a stored membership transition. The permitted values are "
                + $"'{Joined}' and '{Left}', lower case."),
        };
    }
}
