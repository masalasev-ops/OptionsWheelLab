namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// An enumerated candidate and what the gate made of it.
/// </summary>
/// <remarks>
/// <b>Rejected candidates are kept, not filtered out.</b> The scorer prices
/// every candidate that was available [D-W5] and the gate's effect is auditable
/// only if what it refused is recorded with its reasons [D-W10]. A type that
/// returned survivors alone would make the gate invisible in exactly the way
/// §4.3's `gate_status` and `gate_reason` columns exist to prevent.
/// <para>
/// <b>Feasibility is derived rather than stored beside the reasons.</b> Two
/// fields that must agree are two chances to disagree, and §4.3's `gate_status`
/// is Phase 4's to write from this.
/// </para>
/// <para>
/// The reasons are both families as of 2.4, contract then portfolio, which is
/// why this is a collection rather than a reason: the two are evaluated
/// separately and appended in the vocabulary's declared order [D-W4].
/// </para>
/// <para>
/// <b>Equality compares the reasons by sequence, and the synthesised version did
/// not.</b> A record compares each member with the default comparer, which for a
/// collection is reference equality, so two candidates with the same contract and
/// the same reasons in the same order were unequal whenever the lists were
/// separate instances, which is every time the gate runs twice. That is the whole
/// question D-W4 asks, and the type would have answered it wrong: 2.5 found it by
/// asserting that one evaluation repeats, and Phase 4's
/// FX-ThreeMakersSameFeasibleSet is where it would otherwise have surfaced, as a
/// difference between three makers that did not exist.
/// </para>
/// </remarks>
public sealed record GatedCandidate(
    EnumeratedCandidate Candidate,
    IReadOnlyList<GateReason> Reasons)
{
    /// <summary>Whether the gate refused nothing.</summary>
    public bool IsFeasible => Reasons.Count == 0;

    /// <summary>
    /// The same contract refused for the same reasons in the same order.
    /// </summary>
    /// <remarks>
    /// Order is part of it rather than incidental. The vocabulary's declared
    /// order is what a candidate's reasons arrive in [GateReason], so two
    /// candidates carrying one set in two arrangements are not the same verdict
    /// and a comparison ignoring order would say they were.
    /// </remarks>
    public bool Equals(GatedCandidate? other) =>
        other is not null
        && Candidate == other.Candidate
        && Reasons.SequenceEqual(other.Reasons);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Candidate);

        foreach (var reason in Reasons)
        {
            hash.Add(reason);
        }

        return hash.ToHashCode();
    }
}
