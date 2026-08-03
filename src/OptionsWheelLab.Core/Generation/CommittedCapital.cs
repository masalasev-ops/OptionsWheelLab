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
/// was the only quantity in reach and it decided the other way, which is what
/// that reasoning was for: the choice sat in one place, so this correction is one
/// expression rather than a sweep.
/// </para>
/// <para>
/// <b>The multiplier is a constant here because no transcribed one is in
/// reach.</b> <see cref="ContractIdentity"/> carries the deliverable and not the
/// multiplier, and <see cref="Synthetic.ContractQuote"/> excludes it deliberately:
/// a synthetic chain expresses what was quoted rather than the store's record of
/// the instrument. The transcribed multiplier lives on <see cref="Contract"/>
/// [D-W36] and reaches no path a candidate travels. So
/// <see cref="ContractMultiplier"/> stands in, on D-W17's own statement that one
/// hundred is the contract multiplier and no adjustment changes it. That
/// statement is what makes this a stated figure rather than a magic number, and
/// it is not a tunable [CLAUDE.md §3].
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
    /// The contract multiplier for an equity option, which no adjustment changes
    /// [D-W17].
    /// </summary>
    /// <remarks>
    /// Named rather than written at the expression below, so the one place this
    /// figure appears is the place that cites the decision stating it. When a
    /// transcribed multiplier reaches this site the transcribed value is what to
    /// read [D-W36]; that is owed at Phase 8, where vendor data first carries one.
    /// </remarks>
    public const int ContractMultiplier = 100;

    /// <summary>
    /// What <paramref name="contracts"/> of <paramref name="contract"/> commit,
    /// being strike times the multiplier times quantity.
    /// </summary>
    public static decimal For(ContractIdentity contract, int contracts = 1)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contracts);

        return contract.Strike * ContractMultiplier * contracts;
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
