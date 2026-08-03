namespace OptionsWheelLab.Core.Membership;

/// <summary>One watchlist transition: a name joined, or a name left.</summary>
/// <remarks>
/// A transition, not a state. The row records what changed on a date, and
/// membership on a date is resolved from the sequence of transitions rather
/// than read off any single row [D-W35].
/// <para>
/// <b>Not starting at zero, corrected at 3.3.</b> This was the one enum in the
/// store's vocabulary set that did, so <c>default</c> read as
/// <see cref="Joined"/>: an uninitialised transition would have put a name on the
/// watchlist, and membership is what decides which names are tradeable at all
/// [D-W9, D-W16]. <see cref="Identity.OptionRight"/> set the precedent at 0.4 and
/// <see cref="Positions.PositionState"/> and <c>GateReason</c> followed it; this
/// landed at 1.3 without it. Found by the check comparing every declared
/// vocabulary against what the store enforces, which had to assume the property
/// held across all six to assert it once.
/// </para>
/// <para>
/// Renumbering is safe here and was measured rather than assumed: nothing reads
/// the ordinal, every use is by name, and the stored form is a declared mapping
/// rather than a cast [<see cref="Storage.StoreMembershipKind"/>].
/// </para>
/// </remarks>
public enum MembershipKind
{
    Joined = 1,

    Left = 2,
}
