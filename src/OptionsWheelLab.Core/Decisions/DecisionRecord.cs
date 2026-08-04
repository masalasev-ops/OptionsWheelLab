using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// One candidate as the maker was offered it, rebuilt from the record.
/// </summary>
/// <remarks>
/// The same shape <see cref="GatedCandidate"/> carries, and deliberately so: what
/// re-scoring needs is what the maker saw, so a record that rebuilds to a
/// different shape would need the difference reconciled by whoever re-scores.
/// <para>
/// <see cref="Reasons"/> merges both families back into the enumeration's declared
/// order, which is the order a candidate was offered in.
/// </para>
/// </remarks>
public sealed record RecordedCandidate(
    long CandidateId,
    ContractIdentity Contract,
    int ContractsQty,
    decimal CommittedCapital,
    decimal Credit,
    decimal Bid,
    decimal Ask,
    string FeatureJson,
    IReadOnlyList<GateReason> Reasons)
{
    /// <summary>
    /// Feasible exactly when nothing refused it, which is why no status is
    /// stored [D-W52].
    /// </summary>
    public bool IsFeasible => Reasons.Count == 0;
}

/// <summary>
/// One decision and the whole feasible set it was made against, sufficient to
/// re-score it with no access to live state [D-W3].
/// </summary>
public sealed record DecisionRecord(
    long DecisionId,
    string MakerId,
    DateOnly DecisionDate,
    Ticker Symbol,
    OptionRight Right,
    DecisionKind Kind,
    long? ChosenCandidateId,
    long? TrialId,
    int PolicyVersion,
    IReadOnlyList<RecordedCandidate> FeasibleSet)
{
    /// <summary>
    /// The candidate the maker took, or null where it took none.
    /// </summary>
    /// <remarks>
    /// A null choice is a decision and is scored [<see cref="DecisionKind.None"/>],
    /// so this being null is a fact about the decision rather than a gap in the
    /// record.
    /// </remarks>
    public RecordedCandidate? Chosen =>
        ChosenCandidateId is { } id
            ? FeasibleSet.Single(candidate => candidate.CandidateId == id)
            : null;
}
