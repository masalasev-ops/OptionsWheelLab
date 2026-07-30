namespace OptionsWheelLab.Core.Membership;

/// <summary>One watchlist transition: a name joined, or a name left.</summary>
/// <remarks>
/// A transition, not a state. The row records what changed on a date, and
/// membership on a date is resolved from the sequence of transitions rather
/// than read off any single row [D-W35].
/// </remarks>
public enum MembershipKind
{
    Joined,

    Left,
}
