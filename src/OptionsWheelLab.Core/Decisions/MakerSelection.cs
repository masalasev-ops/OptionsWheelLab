using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// What every maker does before it chooses, and how a choice becomes a decision.
/// </summary>
/// <remarks>
/// <b>The filter is the policy band and nothing else.</b> A maker may not re-run
/// a gate constraint or apply one of its own: the survivors of the gate are the
/// feasible set and all three arms receive it, so gating after the choice would
/// give the three different effective opportunity sets and a difference between
/// them would partly be permission rather than judgement [D-W10].
/// <para>
/// The signed delta is passed through, because <see cref="Policy.Admits"/> takes
/// the magnitude itself. Absolute-ing at the call site would state the convention
/// twice and let the two drift.
/// </para>
/// </remarks>
internal static class MakerSelection
{
    /// <summary>
    /// The feasible candidates this policy's band admits, in the order offered.
    /// </summary>
    internal static IReadOnlyList<EnumeratedCandidate> Admitted(
        Policy policy,
        DateOnly session,
        IReadOnlyList<GatedCandidate> offered) =>
        [
            .. offered
                .Where(candidate => candidate.IsFeasible)
                .Select(candidate => candidate.Candidate)
                .Where(candidate => policy.Admits(
                    candidate.Quote.Delta,
                    candidate.Quote.Contract.Expiry.DayNumber - session.DayNumber))
        ];

    /// <summary>
    /// The decision that takes this candidate, its kind following the right.
    /// </summary>
    /// <remarks>
    /// The right is on the contract the maker chose, so the kind is read off the
    /// choice rather than passed alongside it and the two cannot disagree. A put
    /// is sold against cash and a call against shares held, which is the wheel's
    /// two legs and is why [D-W52] keys a feasible set on the right.
    /// </remarks>
    internal static MakerDecision Taking(EnumeratedCandidate candidate, int policyVersion) =>
        new(
            candidate.Quote.Contract.Right == OptionRight.Put
                ? DecisionKind.OpenPut
                : DecisionKind.OpenCall,
            candidate.Quote.Contract,
            TrialId: null,
            policyVersion);
}
