using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The capital a candidate commits, computed in one place [D-W17].
/// </summary>
/// <remarks>
/// <b>Strike times the multiplier, settled at 3.1 by amending the decision</b>
/// [D-W17, as amended]. An adjustment moves the deliverable and leaves the strike
/// and the values used to calculate aggregate exercise prices where it found
/// them, so a three-for-two leaves a $50 option at $50 with a 150-share
/// deliverable and an exercising holder still pays $50 times 100. Committed
/// capital is therefore strike times multiplier in every case, and a figure
/// reading the deliverable would misprice every adjusted position.
/// <para>
/// <b>2.4 read the deliverable, stated why, and named 3.1 to decide it.</b> It
/// was the only quantity in reach and it decided the other way, and the
/// correction was one expression here. That it stayed one expression is a
/// separate claim, which this file asserted and did not hold: the state machine
/// made the same error three more times in the same checkpoint.
/// FX-NoShareCountInOptionCash holds it now, and
/// <see cref="ContractTerms"/> states why.
/// </para>
/// <para>
/// <b>The multiplier is a constant because no transcribed one is in reach, and
/// it lives on <see cref="ContractTerms"/> rather than here.</b>
/// <see cref="ContractIdentity"/> carries the deliverable and not the multiplier,
/// and <see cref="Synthetic.ContractQuote"/> excludes it deliberately: a
/// synthetic chain expresses what was quoted rather than the store's record of
/// the instrument. The transcribed multiplier lives on <see cref="Contract"/>
/// [D-W36] and reaches no path a candidate travels. The figure stood here from
/// 3.3's first commit until its review, which found the state machine computing
/// an assignment, a call-away and a forced close from the deliverable: the
/// quantity was in four places while this file claimed it was in one.
/// </para>
/// <para>
/// <b>What a covered call commits is settled and is not read here.</b> A call
/// written against shares a trial already holds commits no further capital
/// [D-W43], and the figure belongs to the trial from open to close. This returns
/// a candidate's own figure whatever the right, because a candidate has no trial;
/// the state machine is what attributes capital to one, and it is what applies
/// D-W43.
/// </para>
/// </remarks>
public static class CommittedCapital
{
    /// <summary>
    /// What <paramref name="contracts"/> of <paramref name="contract"/> commit,
    /// being the aggregate exercise price times quantity.
    /// </summary>
    public static decimal For(ContractIdentity contract, int contracts = 1)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contracts);

        return ContractTerms.AggregateExercisePrice(contract) * contracts;
    }

    /// <summary>
    /// What one contract of <paramref name="candidate"/> commits.
    /// </summary>
    /// <remarks>
    /// One contract, because nothing sizes a position yet: <c>contracts_qty</c>
    /// is §4.3's column and Phase 4's to write, and a quantity guessed here
    /// would put an unrecorded sizing rule into the decision path.
    /// </remarks>
    public static decimal For(EnumeratedCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return For(candidate.Quote.Contract);
    }
}
