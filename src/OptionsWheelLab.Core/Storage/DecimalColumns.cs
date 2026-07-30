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
    /// <para>
    /// <b>Recalibrated at 1.1, when sixteen names joined one.</b> The reasoning
    /// above was written against a single entry, so it was worth re-measuring rather
    /// than inheriting. Measured: the sixteen flag nothing in the tree, because the
    /// only vocabulary word appearing in any SQL literal is <c>value</c>, in a column
    /// definition and a select list, and neither is an ordering, an aggregate or a
    /// range. The over-reach argument therefore still stands on <c>value</c> alone,
    /// which is the only polymorphic column here.
    /// </para>
    /// <para>
    /// <b>What re-measuring found was a false NEGATIVE, not a positive.</b>
    /// FX-NoDecimalOrderingInSql filtered <c>LAST</c> as an order keyword before
    /// consulting this list, so <c>ORDER BY last</c> was dropped once <c>last</c>
    /// became a column here. Fixed in the same checkpoint, and the lesson is that
    /// adding a name to this list can change what the detector sees, not only what it
    /// matches.
    /// </para>
    /// <para>
    /// <b>Unqualified names, decided rather than defaulted.</b> Qualifying entries as
    /// <c>table.column</c> would need the detector to resolve which table a column
    /// belongs to, which is the alias problem one level harder, and the alias
    /// obligation is discharged in this checkpoint by a convention that forbids
    /// aliasing instead. A name here is a column name in any table.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // config_rows.
        "value",

        // underlying_bars [1.1].
        "open",
        "high",
        "low",
        "close",
        "adj_close",

        // corporate_actions [1.1]. A split carries a ratio, a special dividend an
        // amount.
        "ratio",
        "amount",

        // contracts [1.1]. The strike participates in contract identity, which is
        // why it is canonicalised at construction rather than at the write [D-W29].
        "strike",

        // contract_quotes [1.1]. bid and ask are required; the rest are absent
        // rather than zero, and a null orders and aggregates just as wrongly.
        "bid",
        "ask",
        "last",
        "iv",
        "delta",
        "gamma",
        "theta",
        "vega",
    };
}
