namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// Every table that is derived from an append-only source and may be rebuilt
/// [D-W35].
/// </summary>
/// <remarks>
/// <b>The other half of <see cref="AppendOnlyTables"/>, and it exists because the
/// first table that is neither arrived at 3.3.</b> Until then every table in this
/// store was append-only, so "every created table is in the append-only
/// vocabulary" was a total assertion. <c>trials</c> and <c>positions</c> are
/// not, and without somewhere to declare them the fixture asserting that
/// partition would have had to lose the table names instead, which turns a check
/// into a comment.
/// <para>
/// <b>Declaring them is not permission, and this is the point of the type.</b>
/// [D-W35] permits rewriting a projection only where a test discards it, rebuilds
/// it from its source, and gets the same rows: without that test it is a
/// rewritable table with a flattering name. So this list is what
/// FX-ProjectionRebuildsFromLedger enumerates, which is what stops a projection
/// being added and left uncovered. A hand-written rebuild test naming its tables
/// inline would pass on the day a third projection landed.
/// </para>
/// <para>
/// Kept honest in the same two directions the neighbouring lists are. Every name
/// here must appear in a schema block of <c>DATA_AND_SCHEMA.md</c> §4, and must
/// not appear in <see cref="AppendOnlyTables"/>, both of which are enforceable
/// now. That every created table falls in one list or the other is the
/// per-checkpoint definition of done, since §4 is still mostly specification.
/// </para>
/// </remarks>
public static class ProjectionTables
{
    /// <summary>
    /// Projections of <c>ledger_entries</c>, created by migration 8 at 3.3.
    /// </summary>
    /// <remarks>
    /// Named by [D-W35] itself rather than classified here: it says <c>trials</c>
    /// and <c>positions</c> are projections of <c>ledger_entries</c> and may
    /// carry a nullable close column and be updated in place. One step, unlike
    /// either 3.3 entry in <see cref="AppendOnlyTables"/>.
    /// </remarks>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "trials",
        "positions",
    };
}
