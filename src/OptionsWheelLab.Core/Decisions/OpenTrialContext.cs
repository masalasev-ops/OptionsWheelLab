using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// What a maker is told about a trial it already has open, and nothing more.
/// </summary>
/// <remarks>
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
/// it [D-W12, D-W49].
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
public sealed record OpenTrialContext(
    ContractIdentity Short,
    decimal ShortAsk,
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
