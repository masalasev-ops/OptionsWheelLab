using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// The short a maker already holds, and nothing more.
/// </summary>
/// <remarks>
/// <b>Its absence says there is no short, which is the whole of what a maker
/// needs.</b> A maker in <see cref="Positions.PositionState.Cash"/> has none
/// because it holds no trial, and a maker in
/// <see cref="Positions.PositionState.HoldingShares"/> has none because its trial
/// is holding shares. Two reasons, one consequence, and a maker needs neither
/// reason: it opens, and what it opens is a put in the first case and a covered
/// call in the second. This was called an open trial until 4.5, which made the
/// absence read as "no trial" and turned a statement into a convention; a maker
/// holding shares does hold a trial and has no short to act on.
/// <para>
/// <b>A maker need not read the state it is handed, because the offered set
/// already says.</b> Enumeration keys on the right the state makes sellable
/// [D-W52], so a maker holding shares is offered calls and a maker in cash is
/// offered puts, and its rule is the same either way. That is why passing nothing
/// here withholds nothing. This said a maker is not told which state it is in,
/// which is false of an interface whose third parameter is one; what survives is
/// that neither maker reads it.
/// </para>
/// <b>What this withholds is the design.</b> It carries no
/// <c>PremiumBanked</c>, no <c>GrossBasis</c> and no <c>NetBasis</c>, so a rule
/// that rolled to defer realising a loss cannot be written rather than being
/// discouraged from being written. That is the structural shape [D-W6] uses for
/// the leakage firewall applied one level down: a maker cannot act on a figure it
/// is never handed.
/// <para>
/// It is a parameter and never a lookup. A maker that took a store or a
/// connection to find its own trial would be one 4.5 could not drive, since the
/// composition root decides what is read and what is written
/// [<see cref="IDecisionMaker"/>].
/// </para>
/// <para>
/// <see cref="Bounds"/> arrives resolved as of the session the trial opened
/// [D-W53], not the session being decided. A maker comparing elapsed days against
/// a bound resolved on today's date would apply a limit the trial was never
/// opened under.
/// </para>
/// </remarks>
/// <param name="Short">The contract the trial is short.</param>
/// <param name="ShortAsk">
/// What buying it back costs per share, which is the ask because a purchase pays
/// it [D-W12, D-W49], and null on a session that quotes no ask for it.
/// </param>
/// <param name="UnderlyingClose">
/// The session's close, which the moneyness test reads [D-W38].
/// </param>
/// <param name="OpenedOn">The session the trial opened, for the day bound.</param>
/// <param name="RollsUsed">Rolls already spent, for the roll bound.</param>
/// <param name="Bounds">The bounds this trial opened under [D-W53].</param>
/// <param name="TrialId">
/// The trial a decision refers to. Populated so a decision can name it, because
/// a run holds more than one trial over its life.
/// </param>
public sealed record OpenShort(
    ContractIdentity Short,
    decimal? ShortAsk,
    decimal UnderlyingClose,
    DateOnly OpenedOn,
    int RollsUsed,
    TrialBounds Bounds,
    long? TrialId = null)
{
    /// <summary>
    /// Whether the short is in the money by the exercise-by-exception threshold
    /// [D-W38].
    /// </summary>
    /// <remarks>
    /// The threshold is <see cref="WheelStateMachine.ExerciseByExceptionThreshold"/>
    /// rather than a literal here, so the maker and the machine cannot disagree
    /// about what assignment means. A put is in the money below its strike and a
    /// call above it.
    /// </remarks>
    public bool IsInTheMoney(DateOnly session) =>
        Short.Right == OptionRight.Put
            ? Short.Strike - UnderlyingClose >= WheelStateMachine.ExerciseByExceptionThreshold
            : UnderlyingClose - Short.Strike >= WheelStateMachine.ExerciseByExceptionThreshold;

    /// <summary>Days from this session to the short's expiry.</summary>
    public int DaysToExpiry(DateOnly session) =>
        Short.Expiry.DayNumber - session.DayNumber;

    /// <summary>
    /// Whether a bound has been reached, which makes acting a close [D-W14].
    /// </summary>
    /// <remarks>
    /// The same arithmetic <see cref="TrialProjection"/> uses when it rebuilds the
    /// close kind, deliberately: if the maker and the rebuild computed this
    /// differently the record would disagree with the run and the disagreement
    /// would present as a ledger defect [D-W53].
    /// </remarks>
    public bool BoundReached(DateOnly session) =>
        RollsUsed >= Bounds.MaxRolls
        || session.DayNumber - OpenedOn.DayNumber >= Bounds.MaxTrialDays;
}
