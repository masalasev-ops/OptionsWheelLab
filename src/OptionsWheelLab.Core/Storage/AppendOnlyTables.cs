namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// Every table that is never rewritten, so no statement may delete from it or
/// update it.
/// </summary>
/// <remarks>
/// Declared beside <see cref="Migrations"/>, because that is where the schema is
/// defined and where the tables this names arrive. It is what
/// FX-NoRewriteOfAppendOnlyTables checks SQL against.
/// <para>
/// Kept honest in two directions, at different times, exactly as
/// <see cref="DecimalColumns"/> is. Every name here must appear in a schema
/// block of <c>DATA_AND_SCHEMA.md</c> §4, which is enforceable now. The reverse,
/// that every append-only table in §4 appears here, cannot be a standing
/// assertion while §4 is mostly specification for unbuilt phases, so it is a
/// definition of done on the checkpoint that adds each table.
/// </para>
/// <para>
/// <b>Every entry rests on a decision that states the property.</b> Not on this
/// list's own existence, and not on prose. Two citations in this corpus named a
/// decision for a property it did not state, and both were found by building the
/// check that rested on them [`CLAUDE.md` §1], so the authority is recorded here
/// per entry rather than assumed.
/// </para>
/// <para>
/// The reasons genuinely differ and no general rule follows from there being
/// several [D-W32]. A snapshot is never rewritten because it records what was
/// observable on a date; a decision because it must stay re-scorable from what
/// stood at the time; a config row because as-of resolution answers what was in
/// force then; the migration ledger because a store's schema version is derived
/// from it rather than stated anywhere.
/// </para>
/// </remarks>
public static class AppendOnlyTables
{
    /// <summary>
    /// Tables that are never rewritten, as of the schema that is documented.
    /// </summary>
    /// <remarks>
    /// <b>Eight of these exist and three do not.</b> The six snapshot tables landed
    /// at 1.1; `watchlist_membership` is 1.3, `decisions` and `candidates` Phase 4.
    /// That the constraint lands before the tables it guards was the point rather
    /// than a defect, and 1.1 is where it paid: the vocabulary was already right
    /// when the tables arrived, so the checkpoint added no names.
    /// <para>
    /// <b><c>watchlist_membership</c> is a record</b>: the only place its facts are
    /// held, correcting by appending a transition, so it is append-only [D-W35].
    /// <c>trials</c> and <c>positions</c> stay out for that decision's other half:
    /// they are projections of <c>ledger_entries</c> and may be rebuilt, so
    /// append-only is not their rule, conditional on the rebuild test registered at
    /// Phase 3.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // The snapshot tables of §4.1 [D-W8], created by migration 3 at 1.1, which
        // also gave each a pair of triggers refusing UPDATE and DELETE. This list is
        // what the source detector checks against; those triggers are what holds
        // against a writer the detector cannot see.
        "underlying_bars",
        "corporate_actions",
        "earnings_calendar",
        "chain_snapshots",
        "contracts",
        "contract_quotes",

        // The membership record [D-W35]. 1.3 creates it.
        "watchlist_membership",

        // The decision record [D-W3]. Phase 4 adds them.
        "decisions",
        "candidates",

        // Live today.
        "config_rows",      // [D-W26]
        "schema_migrations", // [D-W32]
    };
}
