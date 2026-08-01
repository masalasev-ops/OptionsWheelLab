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
/// </remarks>
public sealed record GatedCandidate(
    EnumeratedCandidate Candidate,
    IReadOnlyList<GateReason> Reasons)
{
    /// <summary>Whether the gate refused nothing.</summary>
    public bool IsFeasible => Reasons.Count == 0;
}
