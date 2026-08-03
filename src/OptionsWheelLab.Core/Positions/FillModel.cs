using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// One leg's cash, with the premium and the commission kept apart [D-W50].
/// </summary>
/// <remarks>
/// <b>Two figures rather than a net, because the ledger writes two entries.</b>
/// A netted cost cannot answer what a trial paid in commission without
/// recomputing it, and [D-W12]'s word is explicit. <see cref="Net"/> is derived
/// here rather than carried, which is the third arrangement D-W50 rejects: gross,
/// commission and a stored net would state one fact twice.
/// <para>
/// <b><see cref="Premium"/> is signed and <see cref="Commission"/> is not.</b> A
/// sale credits and a purchase debits, so the premium carries the direction; a
/// commission is a cost either way and is always positive. That makes
/// <c>Premium - Commission</c> the net for both without a case.
/// </para>
/// </remarks>
public sealed record Fill(decimal Premium, decimal Commission)
{
    /// <summary>What the account is left with, which is what a basis reads.</summary>
    public decimal Net => Premium - Commission;
}

/// <summary>
/// What a quote is worth once it is filled and paid for [D-W12, D-W50].
/// </summary>
/// <remarks>
/// <b>It resolves its own costs as of the simulated date</b>, on
/// <see cref="Generation.CandidateGenerator"/>'s precedent, where
/// <c>GateBounds.ResolveFor</c> is called from `src/` rather than handed in.
/// That is what makes the three <c>Costs:</c> consumers verifiable:
/// <see cref="TrialBounds"/> exists and nothing in `src/` constructs it, which
/// left two rows <b>Unverified</b> at 3.3 and which
/// <c>CONFIG_REFERENCE.md</c> calls a defect rather than a gap.
/// <para>
/// <b>A sale fills at the bid and a purchase pays the ask</b> [D-W12, D-W49],
/// because both are the side of the spread the account does not choose. The fill
/// point is read rather than assumed even though it cannot vary: a model that
/// skipped the key would honour the rule by accident while the row asserted a
/// different one.
/// </para>
/// <para>
/// <b>Cash is the price times the multiplier</b> [D-W17], never times the
/// deliverable, which FX-NoShareCountInOptionCash holds rather than this remark.
/// </para>
/// </remarks>
public sealed class FillModel
{
    private readonly AsOfConfiguration _configuration;

    public FillModel(AsOfConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <summary>The costs in force on a session, resolved as of it [D-W26].</summary>
    public CostBounds CostsOn(DateOnly session) =>
        CostBounds.ResolveFor(_configuration, session);

    /// <summary>
    /// Selling <paramref name="contracts"/> at the quote's fill point.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the fill point in force is one this model has no rule for.
    /// </exception>
    public Fill Sell(decimal bid, DateOnly session, int contracts = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contracts);

        var costs = CostsOn(session);

        var price = costs.FillPoint switch
        {
            FillPoint.Bid => bid,
            _ => throw new InvalidOperationException(
                $"'{costs.FillPoint}' is a fill point this model has no rule for. A sale fills "
                + "at the bid [D-W12], and a value reaching here that the stored form admitted "
                + "means the vocabulary grew without the model growing with it."),
        };

        return new Fill(
            ContractTerms.CashFor(price) * contracts,
            costs.CommissionPerContract * contracts);
    }

    /// <summary>
    /// Buying <paramref name="contracts"/> back at the ask [D-W49].
    /// </summary>
    /// <remarks>
    /// The fill point governs a sale and not a purchase. A short is bought back at
    /// the ask whatever the sale side reads, because the account is on the side of
    /// the spread it did not choose in both directions.
    /// </remarks>
    public Fill Buy(decimal ask, DateOnly session, int contracts = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contracts);

        var costs = CostsOn(session);

        return new Fill(
            -(ContractTerms.CashFor(ask) * contracts),
            costs.CommissionPerContract * contracts);
    }
}
