namespace OptionsWheelLab.Core.Identity;

/// <summary>
/// What a contract's terms make a quantity of cash [D-W17].
/// </summary>
/// <remarks>
/// <b>The multiplier prices the cash and the deliverable prices nothing.</b> An
/// adjustment moves the deliverable and leaves the strike and "the values used to
/// calculate aggregate exercise prices and premiums" where it found them, so a
/// three-for-two leaves a $50 option at $50 with a 150-share deliverable and an
/// exercising holder still pays $50 times 100. Every cash figure a contract
/// produces therefore multiplies by the multiplier, and the deliverable says only
/// how many shares change hands.
/// <para>
/// <b>Here rather than on <see cref="Generation.CommittedCapital"/>, where the
/// figure lived from 3.3's first commit until its review.</b> That correction
/// argued the quantity sat in one place, and then the state machine computed an
/// assignment, a call-away and a forced close from the deliverable, so it sat in
/// four. The arithmetic is a fact about a contract rather than about committed
/// capital, and putting it where contract terms live is where it belongs.
/// </para>
/// <para>
/// <b>That it sits in one place is held by FX-NoShareCountInOptionCash, not by
/// this paragraph.</b> The claim was made in a comment, was true when written,
/// was false three commits later, and was unchecked throughout. The guard scans
/// <c>src/</c> for a strike or a price multiplied by a share count, which is the
/// shape all four sites had, and it fires when the build does not. A claim about
/// the codebase that nothing asserts is what this checkpoint demonstrated twice.
/// </para>
/// <para>
/// <b>A constant because a decision states it, not because nothing could change
/// it.</b> D-W17 says one hundred is the contract multiplier and no adjustment
/// changes it, which is what makes this a stated figure rather than a magic
/// number and not a tunable [CLAUDE.md §3]. When a transcribed multiplier reaches
/// these sites the transcribed value is what to read [D-W36]; that is owed at
/// Phase 8, where vendor data first carries one.
/// </para>
/// </remarks>
public static class ContractTerms
{
    /// <summary>
    /// The contract multiplier for an equity option, which no adjustment changes
    /// [D-W17].
    /// </summary>
    public const int StandardMultiplier = 100;

    /// <summary>
    /// What exercising <paramref name="contract"/> costs or realises, being
    /// strike times the multiplier.
    /// </summary>
    /// <remarks>
    /// OCC's own term for this quantity, and the one D-W17 quotes: an adjustment
    /// leaves it alone. It is the cash an assignment moves and the figure a
    /// trial's committed capital is measured at.
    /// </remarks>
    public static decimal AggregateExercisePrice(ContractIdentity contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return contract.Strike * StandardMultiplier;
    }

    /// <summary>
    /// What a per-share option price costs or realises for one contract.
    /// </summary>
    /// <remarks>
    /// A premium is quoted per share and multiplies by the multiplier to give the
    /// cash paid for one contract, which is what <see cref="Contract.Multiplier"/>
    /// is defined as. Buying a short back at the bound is that arithmetic, not the
    /// deliverable's.
    /// </remarks>
    public static decimal CashFor(decimal perShare) => perShare * StandardMultiplier;
}
