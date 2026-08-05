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
    /// The six snapshot tables landed at 1.1 and <c>watchlist_membership</c> at
    /// 1.3; <c>decisions</c> and <c>candidates</c> are Phase 4's. No count
    /// sentence: the created-tables sweep is what knows which exist, and the
    /// arithmetic went stale on schedule every time a checkpoint landed. That the
    /// constraint lands before the tables it guards was the point rather than a
    /// defect, and 1.1 is where it paid: the vocabulary was already right when
    /// the tables arrived, so the checkpoint added no names.
    /// <para>
    /// <b><c>watchlist_membership</c> is a record</b>: the only place its facts are
    /// held, correcting by appending a transition, so it is append-only [D-W35].
    /// <c>trials</c> and <c>positions</c> stay out for that decision's other half:
    /// they are projections of <c>ledger_entries</c> and may be rebuilt, so
    /// append-only is not their rule, conditional on the rebuild test registered at
    /// 3.3.
    /// </para>
    /// <para>
    /// <b>Two of the 3.3 entries reach their decision in two steps, and both
    /// steps are written at the entry.</b> Neither <see cref="Migrations"/> nor
    /// prose is the authority: this list's rule is that the decision states the
    /// property, and a classification carried only in a schema document is a
    /// third citation of the kind this corpus has twice found wrong. Recording
    /// the steps is what makes the citation checkable by someone who did not
    /// write it.
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

        // The membership record [D-W35], created by migration 4 at 1.3.
        "watchlist_membership",

        // The session calendar, created by migration 7 at 3.3. Two steps rather
        // than one, and the steps are recorded because that is what this list
        // exists to make checkable: [D-W46] makes the calendar a transcribed
        // stored snapshot, and [D-W8] states that a stored snapshot is never
        // rewritten. As first drafted D-W46 gave the reason and not the
        // property, which classifying it here is what found.
        "market_sessions",

        // The trial record, created by migration 8 at 3.3. Two steps again, and
        // [D-W35] never calls this a record: it says a projection is derived
        // from an append-only SOURCE, and names trials and positions as
        // projections of this table. The property is in the definition of the
        // other half.
        "ledger_entries",

        // The decision record [D-W3], built by migration 9 at 4.2. That decision
        // states the property directly and for all five: a recorded decision is
        // never rewritten, because it exists so a decision can be re-scored from
        // what stood at the time and that holds only if what stood at the time is
        // still there. The set and the reasons are inside the record rather than
        // beside it, since [D-W3] names the feasible set as it stood and the
        // features of every candidate in it as part of what is recorded.
        //
        // Five names where §4.3 carried two until 4.1 [D-W52], which split the
        // reasons by whether they are computed against a book.
        "decisions",
        "candidates",
        "feasible_sets",
        "candidate_gate_reasons",
        "decision_gate_reasons",

        // Live today.
        "config_rows",      // [D-W26]
        "schema_migrations", // [D-W32]
    };
}
