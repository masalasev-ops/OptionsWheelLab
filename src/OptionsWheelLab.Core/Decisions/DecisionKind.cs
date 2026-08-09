namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// What a maker decided to do on a session, from `DATA_AND_SCHEMA.md` §4.3.
/// </summary>
/// <remarks>
/// <b><see cref="None"/> is a decision and is recorded as one.</b> A maker that
/// declines every candidate on a session has chosen, and the scorer computes an
/// outcome for every candidate in that day's feasible set [D-W5], so the choice to
/// take none of them is exactly as scoreable as taking one. Leaving it unrecorded
/// would make a declining maker indistinguishable from a maker that was never
/// asked.
/// <para>
/// <b>Deliberately does not start at zero</b>, so <c>default</c> is not a valid
/// kind. 3.3 found the other way round: <c>MembershipKind</c> started at zero and
/// an uninitialised value read as <c>Joined</c>, which is a real membership
/// transition and passed every check until a vocabulary fixture caught it.
/// </para>
/// <para>
/// <c>OpenPut</c> and <c>OpenCall</c> are separate rather than one <c>Open</c>
/// carrying a right. The right is already on the feasible set a decision
/// references [D-W52], so a decision naming it again would state one fact twice;
/// what these two distinguish is which leg of the wheel the maker was on, which
/// the set's right does not say on its own.
/// <para>
/// That justification once read "because a call set is reachable only by holding
/// shares", which stopped being true at 4.4: a short call enumerates calls too, so
/// a call set is reachable from two states. The distinction the two kinds draw
/// survives; the reason given for it did not.
/// </para>
/// </para>
/// </remarks>
public enum DecisionKind
{
    /// <summary>Sell a put against cash.</summary>
    OpenPut = 1,

    /// <summary>Sell a call against shares held.</summary>
    OpenCall = 2,

    /// <summary>Buy back a short and sell another [D-W14].</summary>
    Roll = 3,

    /// <summary>Buy back a short and end the trial.</summary>
    Close = 4,

    /// <summary>Take no candidate, which is a choice and is scored.</summary>
    None = 5,
}
