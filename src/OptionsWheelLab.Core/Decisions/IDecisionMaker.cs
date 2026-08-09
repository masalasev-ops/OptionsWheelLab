using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// The three maker identifiers, declared once.
/// </summary>
/// <remarks>
/// <c>decisions.maker_id</c> carries no <c>CHECK</c>, unlike <c>kind</c> and both
/// reason columns beside it, so nothing in the store refuses a fourth value or a
/// misspelling of one of these three. A typo would not fail: it would split one
/// maker's decisions into two arms and the improvement curve would be computed
/// over part of a history. One declaration site is what stands between that and a
/// green run. [D-W4] fixes the set at three.
/// </remarks>
public static class MakerIds
{
    public const string Baseline = "baseline";

    public const string Random = "random";

    public const string Learner = "learner";

    /// <summary>All three, in the order [D-W4] names them.</summary>
    public static readonly string[] All = [Baseline, Random, Learner];
}

/// <summary>
/// What a maker decided on one session, sufficient for the record.
/// </summary>
/// <remarks>
/// <b>A decision rather than a contract or a fill.</b> A maker returning
/// <see cref="ContractIdentity"/> alone could not express a close or a roll, and
/// one returning a <see cref="TrialChoice"/> could express neither a close nor
/// taking nothing, since that type has three subtypes and no member for either.
/// 4.4 adds roll and close and 4.5 drives a run, so the shape has to carry all
/// five <see cref="DecisionKind"/> members from the start.
/// <para>
/// <b>It names no <see cref="TrialChoice"/> deliberately.</b> 4.5 writes the
/// adapter from a decision to a choice; if this type were one, the adapter would
/// have nowhere to live and the coupling would be permanent.
/// </para>
/// </remarks>
public sealed record MakerDecision(
    DecisionKind Kind,
    ContractIdentity? Chosen,
    long? TrialId,
    int PolicyVersion);

/// <summary>
/// One arm of the experiment [D-W4].
/// </summary>
/// <remarks>
/// <b>The offered set is a parameter, not something a maker fetches.</b> One
/// <see cref="CandidateGenerator.SharedFor"/> call per symbol, session and right
/// produces the contract-level half and every maker acting against that key is
/// handed it; each maker's caps are applied over it by
/// <see cref="CandidateGenerator.Against"/>, against its own book [D-W11, D-W52].
/// Three makers each evaluating the shared half would satisfy 4.3's test and fail
/// its definition of done, which asks that the byte-identical property hold by
/// construction rather than by three evaluations agreeing.
/// <para>
/// This said one <c>GateFor</c> call per symbol, session and right until 4.5, and
/// that method takes a book as a fourth argument, which [D-W11] makes per maker.
/// So a single call per key had to pick one maker's book and there is none to
/// pick. The split is what makes the sentence true rather than aspirational, and
/// <c>GateFor</c> remains the composition of the two for a caller holding both.
/// </para>
/// <para>
/// <b>No maker takes a store.</b> The caller records, because 4.5 needs a
/// composition root that drives a run and decides what is written, and a maker
/// coupled to a store is one that cannot be used without one.
/// </para>
/// <para>
/// <b>No maker holds a resolved policy.</b> Configuration is read as of the
/// session being decided [D-W26], so the policy is resolved per call. A maker
/// holding one would be a maker that read configuration once, which is the shape
/// that decision exists to prevent, and 4.4 varies configuration under a run.
/// </para>
/// </remarks>
public interface IDecisionMaker
{
    /// <summary>Which arm this is, for the record.</summary>
    string MakerId { get; }

    /// <summary>
    /// The decision this maker makes on this session, given what it was offered
    /// and what it already holds.
    /// </summary>
    /// <param name="openShort">
    /// The short this maker already holds in this name, or null when it holds
    /// none. A maker with one decides what to do about it [D-W54] rather than
    /// selling another; a maker without one opens, and the offered set says
    /// whether that is a put or a covered call [<see cref="OpenShort"/>].
    /// </param>
    MakerDecision Decide(
        Ticker symbol,
        DateOnly session,
        PositionState state,
        BookState book,
        IReadOnlyList<GatedCandidate> offered,
        OpenShort? openShort = null);
}
