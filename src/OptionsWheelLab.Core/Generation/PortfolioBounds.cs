using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The account value and the three cap fractions in force on a simulated date,
/// resolved once [D-W11].
/// </summary>
/// <remarks>
/// <b>A second record rather than four more fields on <see cref="GateBounds"/>,
/// and the reason is that the two families are evaluated apart.</b>
/// <see cref="ContractConstraints"/> takes a quote and no book, and its tests
/// construct <see cref="GateBounds"/> directly; widening would make every
/// contract-constraint site supply four numbers that family cannot read, which
/// is the inverse of the property a bound record exists for. Both records
/// resolve at the top of one evaluation, so "once per evaluation" [D-W37] holds
/// either way.
/// <para>
/// <b>Equity is configuration, not a figure derived from cash and open
/// positions</b> [D-W11]. D-W11's own rationale is the argument: the caps are
/// structural because the sample cannot price the tail, and a denominator
/// computed from the run's own state moves with the run, so a drawdown would
/// loosen every cap at the moment it should bind. It is `rows`-classed under
/// D-W27 because a cap is read while producing a simulated decision and must
/// resolve as-of.
/// </para>
/// <para>
/// <b>Every bound is read as of the simulated date</b> [D-W26], and an
/// unresolvable one stops the evaluation naming the key and the date [D-W37].
/// The same reasoning as the contract bounds, through the same helper: admitting
/// silently drops a structural risk control and rejecting presents a
/// misconfiguration as an absence of opportunity.
/// </para>
/// <para>
/// <b>The caps are held here as fractions and multiplied where they are
/// compared</b>, rather than resolved into three amounts. A fraction is what the
/// store holds and what the operator set [CONFIG_REFERENCE], so a record of
/// amounts would be a second representation of the same three decisions, and
/// <see cref="PortfolioConstraints"/> is where the multiplication is stated
/// alongside the exposure it is compared against.
/// </para>
/// </remarks>
public sealed record PortfolioBounds(
    decimal Equity,
    decimal PerNameCapFraction,
    decimal TotalCapFraction,
    decimal SimultaneousAssignmentLimitFraction)
{
    /// <summary>
    /// The bounds in force on <paramref name="simulatedDate"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When any bound has no value in force on that date.
    /// </exception>
    public static PortfolioBounds ResolveFor(
        AsOfConfiguration configuration,
        DateOnly simulatedDate)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new PortfolioBounds(
            ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.RiskEquity, simulatedDate),
            ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.RiskPerNameCapFraction, simulatedDate),
            ResolvedBound.RequiredDecimal(
                configuration, ConfigKeys.RiskTotalCapFraction, simulatedDate),
            ResolvedBound.RequiredDecimal(
                configuration,
                ConfigKeys.RiskSimultaneousAssignmentLimitFraction,
                simulatedDate));
    }
}
