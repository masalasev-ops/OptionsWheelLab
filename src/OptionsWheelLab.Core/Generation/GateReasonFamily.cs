namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// Which evaluator raises a <see cref="GateReason"/>, and therefore which side of
/// the decision record it is stored on [D-W52].
/// </summary>
/// <remarks>
/// <b>The split is not a presentation choice, it is what the inputs are.</b>
/// <see cref="ContractConstraints"/> reads the candidate, the bounds and the
/// report dates, so every maker sharing a feasible set earns the same six
/// verdicts. <see cref="PortfolioConstraints"/> reads a book, and two makers do
/// not share one once their positions diverge, so those four belong to the maker
/// whose book produced them.
/// <para>
/// <b>Declared rather than inferred from the member values.</b> The contract
/// reasons happen to be one to six today and a range test would work, but the
/// property is which evaluator raises the reason and not where it sits in the
/// enumeration, so a reason added in the middle would silently change families.
/// FX-DecisionsShareOneFeasibleSet holds this against what the two evaluators
/// actually raise, read off their source rather than assumed.
/// </para>
/// <para>
/// <see cref="GatedCandidate"/> carries both families in one list, contract
/// reasons then portfolio ones, because that is the enumeration's declared order
/// and a candidate is offered whole. Splitting happens where the record is
/// written, which is the only place the difference matters.
/// </para>
/// </remarks>
public static class GateReasonFamily
{
    /// <summary>
    /// The six a contract earns from its own quote, the bounds and the report
    /// dates, stored beside the shared feasible set.
    /// </summary>
    public static readonly IReadOnlySet<GateReason> ContractLevel =
        new HashSet<GateReason>
        {
            GateReason.SpreadCap,
            GateReason.PremiumFloor,
            GateReason.CrossedMarket,
            GateReason.DeltaCeiling,
            GateReason.ExpiryWindow,
            GateReason.EarningsClearance,
        };

    /// <summary>
    /// The four computed against a book, stored per decision.
    /// </summary>
    public static readonly IReadOnlySet<GateReason> PortfolioLevel =
        new HashSet<GateReason>
        {
            GateReason.PerNameCap,
            GateReason.TotalCap,
            GateReason.AssignmentStress,
            GateReason.GrossBasis,
        };

    /// <summary>
    /// Whether this reason is shared by every maker holding the same position.
    /// </summary>
    public static bool IsContractLevel(GateReason reason) =>
        ContractLevel.Contains(reason)
            ? true
            : PortfolioLevel.Contains(reason)
                ? false
                : throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    $"'{reason}' is in neither family. Every gate reason is raised by one of the "
                    + "two evaluators and is stored on the side its inputs put it, so a reason "
                    + "added without a family has nowhere to be written.");
}
