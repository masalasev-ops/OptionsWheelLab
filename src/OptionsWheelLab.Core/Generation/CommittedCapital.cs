using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The capital a candidate commits, computed in one place [D-W17].
/// </summary>
/// <remarks>
/// <b>One site, because which quantity this uses is an open obligation.</b>
/// D-W17's first paragraph says the contract multiplier and its third says the
/// deliverable, and they differ for an adjusted contract: on a three-for-two
/// split taking a 90 strike to 60 with a 150-share deliverable, strike times
/// multiplier gives 6,000 and strike times deliverable gives 9,000, and only the
/// second leaves the aggregate exercise where the adjustment found it. That is
/// owed at Phase 3, which is why <see cref="EnumeratedCandidate"/> declined the
/// economics at 2.2 and named this checkpoint. The obligation stays open; what
/// this type buys is that settling it changes one place.
/// <para>
/// <b>2.4 reads the deliverable, and states why rather than deciding the
/// obligation.</b> It is the only quantity in reach: <see cref="ContractIdentity"/>
/// has carried it since 1.5, the multiplier lives on <see cref="Contract"/> and
/// never reaches a quote, and for a standard contract both are one hundred so
/// <c>WORKED_EXAMPLE.md</c> cannot adjudicate either way. Reading what is
/// reachable is not the same as choosing it, and Phase 3 chooses against OCC's
/// adjustment memos rather than against this call site.
/// </para>
/// <para>
/// <b>What a covered call commits is a further open question and nothing here
/// answers it.</b> D-W17 fixes a trial's committed capital at open and carries
/// assigned shares inside the same number, so a covered call sold against shares
/// already held plausibly commits nothing new. This returns the candidate's own
/// figure whatever the right, which is the tighter reading of a cap and
/// therefore the safe one to be wrong in [CLAUDE.md §6]. Raised for Phase 3,
/// where the state machine first has a trial to attribute capital to.
/// </para>
/// </remarks>
public static class CommittedCapital
{
    /// <summary>
    /// What <paramref name="contracts"/> of <paramref name="contract"/> commit,
    /// being strike times deliverable times quantity.
    /// </summary>
    public static decimal For(ContractIdentity contract, int contracts = 1)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contracts);

        return contract.Strike * contract.DeliverableShares * contracts;
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
