using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The six contract-constraint bounds in force on a simulated date, resolved
/// once.
/// </summary>
/// <remarks>
/// <b>Resolved once per evaluation, not once per candidate</b> [D-W37]. A chain
/// is tens of contracts and the bounds are the same for all of them, so
/// resolving per candidate would run six queries per contract and, when a bound
/// is missing, would raise once per contract instead of once.
/// <para>
/// <b>Every bound is read as of the simulated date</b> [D-W26], never as-now.
/// This type holds only resolved values, so a constraint cannot reach
/// configuration at all: it is handed numbers, and the only thing that can read
/// a key is this factory.
/// </para>
/// <para>
/// <b>An unresolvable bound stops the evaluation</b> [D-W37]. Admitting would
/// silently drop a structural risk control [D-W11] and leave a run that looks
/// normal and is unconstrained; rejecting would present a misconfiguration as an
/// absence of opportunity, and a run of empty feasible sets is indistinguishable
/// from a quiet market. Neither is recoverable from the record, which is what
/// the record exists for [D-W5].
/// </para>
/// <para>
/// This is reachable in ordinary use rather than only in tests: `SeedCommand`
/// stamps `set_at` from the wall clock, so every bound resolves null for any
/// simulated date before the seed ran. That collision is a carried obligation
/// owed at Phase 9, and it surfaces here loudly rather than silently, which is
/// the point.
/// </para>
/// </remarks>
public sealed record GateBounds(
    decimal MaxSpreadFractionOfMid,
    decimal MinPremium,
    decimal MaxDelta,
    int MinDte,
    int MaxDte,
    int EarningsClearanceDays)
{
    /// <summary>
    /// The bounds in force on <paramref name="simulatedDate"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When any bound has no value in force on that date.
    /// </exception>
    public static GateBounds ResolveFor(AsOfConfiguration configuration, DateOnly simulatedDate)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new GateBounds(
            ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.GateMaxSpreadFractionOfMid, simulatedDate),
            ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.GateMinPremium, simulatedDate),
            ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.GateMaxDelta, simulatedDate),
            ResolvedBound.RequiredInt(
                configuration, ConfigKeys.GateMinDte, simulatedDate),
            ResolvedBound.RequiredInt(
                configuration, ConfigKeys.GateMaxDte, simulatedDate),
            ResolvedBound.RequiredInt(
                configuration, ConfigKeys.GateEarningsClearanceDays, simulatedDate));
    }
}
