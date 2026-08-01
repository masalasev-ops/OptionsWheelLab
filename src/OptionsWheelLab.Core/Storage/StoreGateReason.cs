using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a gate reason, being the value that reaches
/// <c>candidates.gate_reason</c> [DATA_AND_SCHEMA §4.3].
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape, declared now rather than when
/// <c>candidates</c> lands at Phase 4, on the same argument
/// <see cref="StorePositionState"/> made at 2.2: a tag whose stored form arrives
/// with its table is a tag whose stored form was never a decision.
/// <para>
/// <b>The permitted values are declared, not derived from the enum's
/// spelling.</b> Eight of these ten are unreachable from the member name by any
/// casing rule, so a derivation would have to be a mapping, which is what this
/// is.
/// </para>
/// <para>
/// <b>These are not the phrases WORKED_EXAMPLE §3 uses.</b> That document writes
/// "spread cap" and "delta ceiling" for a reader; the store holds
/// <c>spread_cap</c> and <c>delta_ceiling</c>. The fixture that gates the worked
/// example maps one to the other and asserts the mapping covers every declared
/// reason, so a reason with no phrase fails rather than passing unnoticed. Three
/// representations of one vocabulary would be two too many, which is why the
/// mapping is asserted total rather than written twice.
/// </para>
/// </remarks>
public static class StoreGateReason
{
    public const string SpreadCap = "spread_cap";

    public const string PremiumFloor = "premium_floor";

    public const string CrossedMarket = "crossed_market";

    public const string DeltaCeiling = "delta_ceiling";

    public const string ExpiryWindow = "expiry_window";

    public const string EarningsClearance = "earnings_clearance";

    public const string PerNameCap = "per_name_cap";

    public const string TotalCap = "total_cap";

    public const string AssignmentStress = "assignment_stress";

    public const string GrossBasis = "gross_basis";

    /// <summary>
    /// Every permitted value, for the refusal messages below.
    /// </summary>
    /// <remarks>
    /// The mapping stays declared; this is only how the two messages name what
    /// they would otherwise spell out. At six the sentence was written twice and
    /// stayed right by inspection; at ten a hand-written list is a third
    /// statement of the vocabulary that goes stale silently, which is the defect
    /// this file's own remark warns about.
    /// </remarks>
    private static readonly string[] Permitted =
    [
        SpreadCap,
        PremiumFloor,
        CrossedMarket,
        DeltaCeiling,
        ExpiryWindow,
        EarningsClearance,
        PerNameCap,
        TotalCap,
        AssignmentStress,
        GrossBasis,
    ];

    public static string ToStored(GateReason reason) => reason switch
    {
        GateReason.SpreadCap => SpreadCap,
        GateReason.PremiumFloor => PremiumFloor,
        GateReason.CrossedMarket => CrossedMarket,
        GateReason.DeltaCeiling => DeltaCeiling,
        GateReason.ExpiryWindow => ExpiryWindow,
        GateReason.EarningsClearance => EarningsClearance,
        GateReason.PerNameCap => PerNameCap,
        GateReason.TotalCap => TotalCap,
        GateReason.AssignmentStress => AssignmentStress,
        GateReason.GrossBasis => GrossBasis,
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            $"'{reason}' is not a gate reason. The stored forms are {Listed()}."),
    };

    public static GateReason ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            SpreadCap => GateReason.SpreadCap,
            PremiumFloor => GateReason.PremiumFloor,
            CrossedMarket => GateReason.CrossedMarket,
            DeltaCeiling => GateReason.DeltaCeiling,
            ExpiryWindow => GateReason.ExpiryWindow,
            EarningsClearance => GateReason.EarningsClearance,
            PerNameCap => GateReason.PerNameCap,
            TotalCap => GateReason.TotalCap,
            AssignmentStress => GateReason.AssignmentStress,
            GrossBasis => GateReason.GrossBasis,
            _ => throw new FormatException(
                $"'{stored}' is not a stored gate reason. The permitted values are "
                + $"{Listed()}, lower case."),
        };
    }

    private static string Listed() =>
        string.Join(", ", Permitted.Select(value => $"'{value}'"));
}
