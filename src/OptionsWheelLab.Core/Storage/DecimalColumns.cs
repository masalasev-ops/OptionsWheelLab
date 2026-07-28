namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// Every column that carries a decimal in the canonical stored form.
/// </summary>
/// <remarks>
/// Declared beside <see cref="Migrations"/>, because that is where the schema is
/// defined and where the market-data tables land at Phase 1. It is what
/// FX-NoDecimalOrderingInSql checks SQL against: the stored form is not
/// order-preserving, so no query may order, range over, or aggregate one of
/// these [D-W29].
/// <para>
/// Kept honest in two directions, at different times, for the reason the corpus
/// already gives twice. Every name here must appear in a schema block of
/// <c>DATA_AND_SCHEMA.md</c> §4, which is enforceable now. The reverse, that
/// every decimal column in §4 appears here, cannot be a standing assertion while
/// §4 is mostly specification for unbuilt phases, so it is a definition of done
/// on the checkpoint that adds each table.
/// </para>
/// </remarks>
public static class DecimalColumns
{
    /// <summary>
    /// Columns holding a canonical decimal, as of the migrations that exist.
    /// </summary>
    /// <remarks>
    /// <b><c>value</c> is polymorphic, and classing it decimal is deliberate.</b>
    /// It carries decimals for the <c>Costs</c>, <c>Risk</c>, <c>Gate</c> and
    /// <c>Policy</c> keys, integers for <c>Trial</c>, and a bare word for
    /// <c>Costs:FillPoint</c>. So the first entry here is a column that is only
    /// sometimes a decimal, and the check over-reaches on purpose: flagging a
    /// legitimate ordering on an integer-valued key is recoverable, and missing
    /// an ordering on a decimal-valued key is not.
    /// <para>
    /// Without that reasoning written down, the first false positive reads as a
    /// defect and the column gets removed from this list, which is exactly the
    /// failure the check exists to prevent.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // config_rows. Phase 1 adds the market-data columns: open, high, low,
        // close, adj_close, ratio, amount, strike, bid, ask, last, iv, delta,
        // gamma, theta and vega.
        "value",
    };
}
