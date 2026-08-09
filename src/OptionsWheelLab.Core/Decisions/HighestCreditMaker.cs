using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// Takes the highest credit inside its band [WORKED_EXAMPLE §1]. The frozen
/// baseline and the learner are both this, on different rows.
/// </summary>
/// <remarks>
/// <b>One algorithm and two policies, because a learner with a rule in code
/// cannot learn.</b> The learning channel writes to the learner and nothing else
/// [D-W6], and what a channel can write is a row, so a rule compiled in is a rule
/// no channel can change and the learner would be a second frozen arm with a
/// different band. What separates the two arms is entirely
/// <c>Policy:Baseline:</c> against <c>Policy:Learner:</c>.
/// <para>
/// <b>Credit is the bid times the multiplier, gross of commission.</b> The lab
/// sells at the bid and never the mid [D-W12], and the worked example cannot
/// settle gross against net: its commission is flat per contract and every
/// candidate is one contract, so the two rank identically there. Gross is what
/// <see cref="DecisionStore"/> already writes to <c>candidates.credit</c>, so
/// ranking on it ranks on a figure the record holds rather than one only the
/// maker computed.
/// </para>
/// <para>
/// <b>The tie-break is the order the maker was given, and no document states
/// one.</b> Nothing in the corpus says what happens when two in-band candidates
/// carry the same bid. The order handed in is contract identity order, which is
/// the order the generator emits and the order the record stores, so taking the
/// first is taking the order it was given rather than imposing one. Stated here
/// because it is a build-time convention with no authored source, and reported at
/// sign-off rather than left in a remark.
/// </para>
/// <para>
/// <b>The worked example cannot exercise any of this.</b> Its baseline band
/// admits exactly one of the three feasible candidates, so the maximum never
/// discriminates and the tie-break is never reached. Only the learner's
/// two-candidate band exercises the comparison at all, and its two bids differ.
/// </para>
/// </remarks>
public sealed class HighestCreditMaker : IDecisionMaker
{
    private readonly AsOfConfiguration _configuration;
    private readonly Func<AsOfConfiguration, DateOnly, Policy> _policy;

    private HighestCreditMaker(
        string makerId,
        Func<AsOfConfiguration, DateOnly, Policy> policy,
        AsOfConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        MakerId = makerId;
        _policy = policy;
        _configuration = configuration;
    }

    public string MakerId { get; }

    /// <summary>The frozen baseline, which never changes [D-W4].</summary>
    public static HighestCreditMaker Baseline(AsOfConfiguration configuration) =>
        new(MakerIds.Baseline, Policy.ForBaseline, configuration);

    /// <summary>The learner, which acts from its current policy rows.</summary>
    public static HighestCreditMaker Learner(AsOfConfiguration configuration) =>
        new(MakerIds.Learner, Policy.ForLearner, configuration);

    public MakerDecision Decide(
        Ticker symbol,
        DateOnly session,
        PositionState state,
        BookState book,
        IReadOnlyList<GatedCandidate> offered,
        OpenShort? openShort = null)
    {
        ArgumentNullException.ThrowIfNull(offered);

        var policy = _policy(_configuration, session);

        if (openShort is { } held)
        {
            return MakerSelection.ForOpenShort(policy, held, session, offered, HighestCredit);
        }

        var admitted = MakerSelection.Admitted(policy, session, offered);

        if (admitted.Count == 0)
        {
            return new MakerDecision(DecisionKind.None, null, null, policy.Version);
        }

        return MakerSelection.Taking(HighestCredit(admitted), policy.Version);
    }

    /// <summary>
    /// The highest credit among these, ties taking the order offered.
    /// </summary>
    /// <remarks>
    /// <c>MaxBy</c> keeps the first of equal values, so a tie takes the order the
    /// maker was given rather than one this comparison invents. Shared by the
    /// opening path and the roll, because [D-W54] rolls by the rule the maker
    /// opens with.
    /// </remarks>
    private static EnumeratedCandidate HighestCredit(IReadOnlyList<EnumeratedCandidate> among) =>
        among.MaxBy(candidate => ContractTerms.CashFor(candidate.Quote.Bid))!;
}
