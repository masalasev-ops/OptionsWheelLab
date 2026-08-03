using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a ledger entry's kind [D-W48].
/// </summary>
/// <remarks>
/// <see cref="StorePositionState"/>'s shape and its argument: the permitted values
/// are declared rather than derived from a member's spelling, and here as there
/// most of them are unreachable by any casing of the enum name.
/// <para>
/// Migration 8 carries the same list as a <c>CHECK</c>, and the two are asserted
/// to agree rather than assumed to.
/// </para>
/// </remarks>
public static class StoreLedgerEntryKind
{
    public const string PremiumReceived = "premium_received";

    public const string PremiumPaid = "premium_paid";

    public const string BoughtToClose = "bought_to_close";

    public const string ExpiredWorthless = "expired_worthless";

    public const string Assignment = "assignment";

    public const string CallAway = "call_away";

    public const string SharesSold = "shares_sold";

    public const string Dividend = "dividend";

    public const string Commission = "commission";

    public const string AssignmentFee = "assignment_fee";

    public const string Stopped = "stopped";

    public static string ToStored(LedgerEntryKind kind) => kind switch
    {
        LedgerEntryKind.PremiumReceived => PremiumReceived,
        LedgerEntryKind.PremiumPaid => PremiumPaid,
        LedgerEntryKind.BoughtToClose => BoughtToClose,
        LedgerEntryKind.ExpiredWorthless => ExpiredWorthless,
        LedgerEntryKind.Assignment => Assignment,
        LedgerEntryKind.CallAway => CallAway,
        LedgerEntryKind.SharesSold => SharesSold,
        LedgerEntryKind.Dividend => Dividend,
        LedgerEntryKind.Commission => Commission,
        LedgerEntryKind.AssignmentFee => AssignmentFee,
        LedgerEntryKind.Stopped => Stopped,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            $"'{kind}' is not a ledger entry kind. This is most likely an uninitialised "
            + "value: the enumeration deliberately does not start at zero."),
    };

    public static LedgerEntryKind ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            PremiumReceived => LedgerEntryKind.PremiumReceived,
            PremiumPaid => LedgerEntryKind.PremiumPaid,
            BoughtToClose => LedgerEntryKind.BoughtToClose,
            ExpiredWorthless => LedgerEntryKind.ExpiredWorthless,
            Assignment => LedgerEntryKind.Assignment,
            CallAway => LedgerEntryKind.CallAway,
            SharesSold => LedgerEntryKind.SharesSold,
            Dividend => LedgerEntryKind.Dividend,
            Commission => LedgerEntryKind.Commission,
            AssignmentFee => LedgerEntryKind.AssignmentFee,
            Stopped => LedgerEntryKind.Stopped,
            _ => throw new FormatException(
                $"'{stored}' is not a stored ledger entry kind. The permitted values are lower "
                + "case with underscores, and migration 8's CHECK carries the same list."),
        };
    }
}

/// <summary>
/// The stored form of how a trial closed [DATA_AND_SCHEMA §4.3].
/// </summary>
/// <remarks>
/// Beside <see cref="StoreLedgerEntryKind"/> because the two vocabularies are read
/// together: four of these five read straight off a ledger kind, and the fifth is
/// what <see cref="LedgerEntryKind.BoughtToClose"/> exists for.
/// <para>
/// <b>Null is not a member here.</b> An open trial carries no close kind, which is
/// the absence of a value rather than a value meaning absent, so it is the
/// column's nullability rather than a sixth name. A <c>None</c> member would let a
/// closed trial be written as closed-in-no-way.
/// </para>
/// </remarks>
public static class StoreTrialCloseKind
{
    public const string ExpiredWorthless = "expired_worthless";

    public const string CalledAway = "called_away";

    public const string ClosedAtBound = "closed_at_bound";

    public const string ClosedByChoice = "closed_by_choice";

    public const string Stopped = "stopped";

    public static string ToStored(TrialCloseKind kind) => kind switch
    {
        TrialCloseKind.ExpiredWorthless => ExpiredWorthless,
        TrialCloseKind.CalledAway => CalledAway,
        TrialCloseKind.ClosedAtBound => ClosedAtBound,
        TrialCloseKind.ClosedByChoice => ClosedByChoice,
        TrialCloseKind.Stopped => Stopped,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            $"'{kind}' is not a trial close kind. This is most likely an uninitialised value: "
            + "the enumeration deliberately does not start at zero, and an open trial carries "
            + "no close kind at all rather than one meaning none."),
    };

    public static TrialCloseKind ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            ExpiredWorthless => TrialCloseKind.ExpiredWorthless,
            CalledAway => TrialCloseKind.CalledAway,
            ClosedAtBound => TrialCloseKind.ClosedAtBound,
            ClosedByChoice => TrialCloseKind.ClosedByChoice,
            Stopped => TrialCloseKind.Stopped,
            _ => throw new FormatException(
                $"'{stored}' is not a stored trial close kind. The permitted values are lower "
                + "case with underscores, and migration 8's CHECK carries the same list."),
        };
    }
}
