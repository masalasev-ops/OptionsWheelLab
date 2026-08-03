using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// What a fill costs, in force on a simulated date [D-W12, D-W50].
/// </summary>
/// <remarks>
/// <see cref="Generation.GateBounds"/>'s shape and its arguments. Every value is
/// read as of the simulated date and never as-now [D-W26], the record holds only
/// resolved values so a fill model cannot reach configuration at all, and a key
/// with no value in force stops the evaluation rather than defaulting [D-W37].
/// <para>
/// <b>The fill point is resolved rather than assumed, though it cannot vary.</b>
/// A sale fills at the bid and that is not a tunable [D-W12], so this reads a key
/// whose answer is known. What the read buys is that a store saying otherwise is
/// refused: a fill model that skipped the key would honour the rule by accident
/// while the row asserted a different one, and configuration that nothing reads
/// is configuration nothing can be wrong about.
/// </para>
/// <para>
/// <b>The assignment fee is zero and is still read</b> [D-W50]. Its value is one
/// broker's schedule rather than a market rule, so the key is what makes a broker
/// that charges a change to a stored value rather than to code. A model that
/// hardcoded the zero would make that change a code change and would put an
/// unstated cost assumption inside the assignment path.
/// </para>
/// </remarks>
public sealed record CostBounds(
    decimal CommissionPerContract,
    decimal AssignmentFee,
    FillPoint FillPoint)
{
    /// <summary>The costs in force on <paramref name="simulatedDate"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// When any of the three has no value in force on that date.
    /// </exception>
    /// <exception cref="FormatException">
    /// When the fill point in force is not a word this lab admits.
    /// </exception>
    public static CostBounds ResolveFor(AsOfConfiguration configuration, DateOnly simulatedDate)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new CostBounds(
            Generation.ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.CostsCommissionPerContract, simulatedDate),
            Generation.ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.CostsAssignmentFee, simulatedDate),
            StoreFillPoint.ParseStored(
                Generation.ResolvedBound.RequiredWord(
                    configuration, ConfigKeys.CostsFillPoint, simulatedDate)));
    }
}
