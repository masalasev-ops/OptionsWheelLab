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
/// spelling.</b> Four of these six are unreachable from the member name by any
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

    public static string ToStored(GateReason reason) => reason switch
    {
        GateReason.SpreadCap => SpreadCap,
        GateReason.PremiumFloor => PremiumFloor,
        GateReason.CrossedMarket => CrossedMarket,
        GateReason.DeltaCeiling => DeltaCeiling,
        GateReason.ExpiryWindow => ExpiryWindow,
        GateReason.EarningsClearance => EarningsClearance,
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            $"'{reason}' is not a gate reason. The stored forms are '{SpreadCap}', "
            + $"'{PremiumFloor}', '{CrossedMarket}', '{DeltaCeiling}', '{ExpiryWindow}' and "
            + $"'{EarningsClearance}'."),
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
            _ => throw new FormatException(
                $"'{stored}' is not a stored gate reason. The permitted values are "
                + $"'{SpreadCap}', '{PremiumFloor}', '{CrossedMarket}', '{DeltaCeiling}', "
                + $"'{ExpiryWindow}' and '{EarningsClearance}', lower case."),
        };
    }
}
