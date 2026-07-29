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
    /// <b>Two of these exist and eight do not.</b> That is the point rather than
    /// a defect: the constraint lands before the tables it guards, so Phase 1
    /// inherits it instead of rediscovering it.
    /// <para>
    /// <b><c>watchlist_membership</c> is deliberately absent.</b> §4.2 says its
    /// rows are never deleted, while a nullable <c>left_on</c> makes a departure
    /// an update, so the schema and the rule disagree. Putting it here would
    /// settle that by implication, which is not this list's to settle; it is
    /// owed at Phase 1. The seven tables the corpus says nothing about are absent
    /// for the weaker version of the same reason.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // The snapshot tables of §4.1 [D-W8]. None exists yet; Phase 1 adds them.
        "underlying_bars",
        "corporate_actions",
        "earnings_calendar",
        "chain_snapshots",
        "contracts",
        "contract_quotes",

        // The decision record [D-W3]. Phase 4 adds them.
        "decisions",
        "candidates",

        // Live today.
        "config_rows",      // [D-W26]
        "schema_migrations", // [D-W32]
    };
}
