using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The four contract-constraint families of [D-W22] to [D-W25], asking whether
/// a contract belongs in the opportunity set at all [SYSTEM_DESIGN §3.4].
/// </summary>
/// <remarks>
/// <b>Every constraint is evaluated and every failing reason recorded, never
/// the first</b> [D-W22]. A candidate failing two constraints shows both, which
/// is what the screens and the audit trail need and what 2.5 asserts.
/// <para>
/// <b>The comparisons are deliberately not uniform, and each comes from the
/// decision that states it</b> [D-W25's note]. The spread cap and the delta
/// ceiling reject on "exceeds" [D-W22, D-W23]; the premium floor rejects a bid
/// strictly below it [D-W22]; the expiry window admits its own bounds [D-W24];
/// the earnings buffer includes its edge [D-W25]. They differ because the
/// quantities differ, so none of them is derived from a house convention.
/// </para>
/// <para>
/// <b>Pure, and handed its bounds.</b> Nothing here reads configuration or a
/// clock: <see cref="GateBounds"/> resolves once per evaluation [D-W37] and the
/// report dates arrive already windowed, so this type is a function of its
/// arguments and can be tested without a store.
/// </para>
/// <para>
/// The portfolio constraints are not here. They ask whether the book can carry
/// the position rather than whether the contract belongs in the set, and they
/// need ledger state, so they live in <see cref="PortfolioConstraints"/> with
/// their own bound record [D-W11, D-W19].
/// </para>
/// </remarks>
public static class ContractConstraints
{
    /// <summary>
    /// Every reason <paramref name="quote"/> fails, in
    /// <see cref="GateReason"/>'s declared order, empty when it passes all four
    /// families.
    /// </summary>
    /// <param name="reportDates">
    /// The scheduled reports already restricted to the buffered window, which
    /// the caller computes because the buffer is the caller's to apply.
    /// </param>
    public static IReadOnlyList<GateReason> Evaluate(
        ContractQuote quote,
        DateOnly simulatedDate,
        GateBounds bounds,
        IReadOnlyList<DateOnly> reportDates)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(reportDates);

        var reasons = new List<GateReason>();

        // Liquidity [D-W22]. The crossed check comes first because a crossed
        // quote's mid is not a mid, so the spread ratio below it is arithmetic
        // on a quantity that does not mean anything.
        if (quote.Bid > quote.Ask)
        {
            reasons.Add(GateReason.CrossedMarket);
        }
        else if (SpreadFractionOfMid(quote) > bounds.MaxSpreadFractionOfMid)
        {
            reasons.Add(GateReason.SpreadCap);
        }

        if (quote.Bid < bounds.MinPremium)
        {
            reasons.Add(GateReason.PremiumFloor);
        }

        // The ceiling compares absolute delta [D-W23]: the loader carries the
        // sign the chain states and the gate drops it. A quote with no delta is
        // not tested, because an absent value is not a breach.
        if (quote.Delta is { } delta && Math.Abs(delta) > bounds.MaxDelta)
        {
            reasons.Add(GateReason.DeltaCeiling);
        }

        var dte = quote.Contract.Expiry.DayNumber - simulatedDate.DayNumber;

        if (dte < bounds.MinDte || dte > bounds.MaxDte)
        {
            reasons.Add(GateReason.ExpiryWindow);
        }

        if (reportDates.Count != 0)
        {
            reasons.Add(GateReason.EarningsClearance);
        }

        return reasons;
    }

    /// <summary>
    /// The window of report dates that would breach clearance for a contract
    /// opened on <paramref name="simulatedDate"/> and expiring on
    /// <paramref name="expiry"/>.
    /// </summary>
    /// <remarks>
    /// The contract's life widened by the buffer on both sides, because the
    /// calendar date itself moves [D-W25]. Both ends are inclusive, so a report
    /// exactly the buffer's distance from either end is inside the window and
    /// rejects: a window excluding its own edge would admit a report at
    /// precisely the distance the buffer was sized for.
    /// <para>
    /// <b>At a buffer of zero this window collapses to the contract's life</b>,
    /// and whether that life includes its own endpoints becomes live. It does
    /// not bind while `Gate:EarningsClearanceDays` is at least one, which the
    /// seeded seven satisfies and nothing forbids changing.
    /// </para>
    /// </remarks>
    public static (DateOnly From, DateOnly To) ClearanceWindow(
        DateOnly simulatedDate,
        DateOnly expiry,
        int bufferDays) =>
        (simulatedDate.AddDays(-bufferDays), expiry.AddDays(bufferDays));

    /// <summary>
    /// The quoted spread as a fraction of the mid [D-W22].
    /// </summary>
    /// <remarks>
    /// Only reached for an uncrossed quote, so the mid is a real midpoint. A
    /// zero mid means both sides are zero, which the premium floor rejects on
    /// its own ground; treating it as an infinite spread here would put a second
    /// reason on one defect.
    /// </remarks>
    private static decimal SpreadFractionOfMid(ContractQuote quote)
    {
        var mid = (quote.Bid + quote.Ask) / 2m;

        return mid == 0m ? 0m : (quote.Ask - quote.Bid) / mid;
    }
}
