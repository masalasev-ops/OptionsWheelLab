using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

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
    /// Seven days to expiry, the session at or inside which a maker acts on an
    /// open trial [D-W54].
    /// </summary>
    /// <remarks>
    /// <b>A named constant rather than a configuration row, and that is worth
    /// stating.</b> [CLAUDE.md §3] says a value that could plausibly be tuned is a
    /// key rather than a literal, and this one plausibly could. It is not a key
    /// because [D-W54] states the number itself rather than naming a key, and
    /// inventing one would add a tunable that decision did not authorise. A row
    /// under <c>Policy:</c> would be worse still: it would be learner-writable,
    /// and rolling is the only mechanism that puts trial duration under a maker's
    /// control.
    /// <para>
    /// It is not read from <c>Gate:MinDte</c> either, though [D-W54] derives it
    /// from that floor. Reading the key would tie the two, so changing what the
    /// gate will open would silently change when a maker acts on what it holds,
    /// and those are different questions that happen to share an answer.
    /// </para>
    /// </remarks>
    internal const int ActAtDaysToExpiry = 7;

    /// <summary>
    /// What a maker does with a trial it already has open [D-W54].
    /// </summary>
    /// <remarks>
    /// One rule for all three arms. They differ in the band each selects from and
    /// not in when they act, because rolling moves trial duration and a private
    /// roll schedule would give one arm a lever on the measurement rather than on
    /// the trade [D-W4].
    /// <para>
    /// <b>Each arm rolls by the rule it opens with, which is a reading of [D-W54]
    /// rather than a quotation of it.</b> That decision says a maker "rolls to the
    /// candidate its own policy selects from that session's feasible set, by the
    /// same highest-credit-in-band rule it uses to open", and the two halves of
    /// that sentence part company at the random control, which opens by a uniform
    /// draw and not by highest credit. Selection is injected here so each arm
    /// keeps its own: a control that rolled by preference would stop being a
    /// no-skill floor on the leg it rolled.
    /// </para>
    /// <para>
    /// <b>The order of the tests is the rule's order and is not arbitrary.</b> A
    /// position outside the threshold is not looked at; one that would expire
    /// worthless is left to expire, because the wheel's ordinary outcome is a
    /// short expiring and a maker buying back every position would never be
    /// assigned; a session with no feasible set cannot be acted on at all; and
    /// only then does a bound decide between closing and rolling.
    /// </para>
    /// </remarks>
    internal static MakerDecision ForOpenTrial(
        Policy policy,
        OpenTrialContext trial,
        DateOnly session,
        IReadOnlyList<GatedCandidate> offered,
        Func<IReadOnlyList<EnumeratedCandidate>, EnumeratedCandidate> select)
    {
        // Not yet at the threshold, or it would expire worthless: leave it.
        if (trial.DaysToExpiry(session) > ActAtDaysToExpiry || !trial.IsInTheMoney(session))
        {
            return Nothing(trial, policy);
        }

        var admitted = Admitted(policy, session, offered);

        // Acting requires a feasible set, and a session with no chain has none
        // [D-W52]. The position is left as it stands rather than closed.
        if (offered.Count == 0)
        {
            return Nothing(trial, policy);
        }

        // A bound reached makes acting a close [D-W14], and so does a band with
        // nothing in it or a roll that would cost more than it collects.
        var into = admitted.Count == 0 ? null : select(admitted);

        if (trial.BoundReached(session)
            || into is null
            || into.Quote.Bid < trial.ShortAsk)
        {
            return new MakerDecision(DecisionKind.Close, trial.Short, trial.TrialId, policy.Version);
        }

        return new MakerDecision(DecisionKind.Roll, into.Quote.Contract, trial.TrialId, policy.Version);
    }

    private static MakerDecision Nothing(OpenTrialContext trial, Policy policy) =>
        new(DecisionKind.None, null, trial.TrialId, policy.Version);

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
