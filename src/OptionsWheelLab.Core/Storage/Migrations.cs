namespace OptionsWheelLab.Core.Storage;

/// <summary>One schema change, applied once and recorded.</summary>
public sealed record Migration(int Id, string Name, string Sql);

/// <summary>
/// Every migration, in order.
/// </summary>
/// <remarks>
/// Configuration at 0.3, the market-data tables at 1.1, membership at 1.3.
/// </remarks>
public static class Migrations
{
    /// <summary>
    /// The six snapshot tables migration 3 creates, frozen at the text of that
    /// migration.
    /// </summary>
    /// <remarks>
    /// <b>Not <see cref="AppendOnlyTables.All"/>, deliberately, and it is not a
    /// duplicate to be tidied away.</b> That vocabulary is the right source for
    /// what the rule covers and it grows: `watchlist_membership` joins it under
    /// D-W35 and the decision tables later. An applied migration's SQL has to be
    /// frozen, because `schema_migrations` records that migration 3 ran and
    /// nothing re-applies it [D-W32]. Composing that SQL from a growing list would
    /// emit triggers for tables that do not exist at schema 3 and would make
    /// migration 3 mean something different after the list grew.
    /// <para>
    /// So this list is local and frozen, and its job is only to save writing
    /// twelve near-identical triggers by hand. The two-way check against the
    /// vocabulary is a definition of done on this checkpoint, not a shared
    /// reference.
    /// </para>
    /// </remarks>
    private static readonly string[] MarketDataTables =
    [
        "underlying_bars",
        "corporate_actions",
        "earnings_calendar",
        "chain_snapshots",
        "contracts",
        "contract_quotes",
    ];

    public static IReadOnlyList<Migration> All { get; } =
    [
        new Migration(
            1,
            "config_rows",
            """
            CREATE TABLE config_rows (
                key     TEXT    NOT NULL,
                version INTEGER NOT NULL,
                value   TEXT    NOT NULL,
                set_at  TEXT    NOT NULL,
                note    TEXT    NULL,
                PRIMARY KEY (key, version)
            );

            -- Append-only, enforced by the store rather than by convention, so
            -- the guard holds against any writer and not only ours [D-W26].
            -- Not D-W8: that governs snapshots, which carry observed_at and
            -- correct by appending a new observation. This table carries set_at
            -- and version, and a written version is never altered.
            CREATE TRIGGER config_rows_no_update
            BEFORE UPDATE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only: a change inserts version + 1');
            END;

            CREATE TRIGGER config_rows_no_delete
            BEFORE DELETE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only: rows are never deleted');
            END;
            """),

        new Migration(
            2,
            "config_rows_set_at_monotonic",
            """
            -- version is MAX + 1 and always increases; set_at is supplied and
            -- was unconstrained. Resolution filters on set_at and then orders
            -- by version, so an out-of-order timestamp makes "in force on date
            -- T" depend on insertion order rather than on time, and the
            -- append-only guards make that permanent.
            --
            -- Equal is allowed: two versions of one key can legitimately share
            -- an instant, and version breaks the tie, which is what the as-of
            -- resolution already does [D-W26].
            --
            -- Per key, because keys are versioned independently.
            CREATE TRIGGER config_rows_set_at_not_earlier
            BEFORE INSERT ON config_rows
            WHEN NEW.set_at < (SELECT MAX(set_at) FROM config_rows WHERE key = NEW.key)
            BEGIN
                SELECT RAISE(ABORT, 'config_rows set_at moves forward: a new version cannot predate the newest version of the same key');
            END;
            """),

        // Called rather than referencing a property, because a static field
        // initialiser runs in declaration order and this array is declared first.
        new Migration(3, "market_data", BuildMarketDataSql()),

        new Migration(
            4,
            "watchlist_membership",
            """
            -- The membership record of DATA_AND_SCHEMA.md 4.2 [D-W35]: the only
            -- place watchlist facts are held, so it corrects by appending a
            -- further transition and is never rewritten. Keyed on symbol and
            -- version, config_rows' own key shape, because keying on the symbol
            -- alone cannot express re-entry.
            --
            -- kind carries a CHECK for the same reason right does in contracts:
            -- a stored form the database does not enforce has one guard.
            -- reason is nullable on config_rows.note's precedent [4.2].
            CREATE TABLE watchlist_membership (
                symbol       TEXT    NOT NULL,
                version      INTEGER NOT NULL,
                effective_on TEXT    NOT NULL,
                kind         TEXT    NOT NULL CHECK (kind IN ('joined', 'left')),
                reason       TEXT    NULL,
                observed_at  TEXT    NOT NULL,
                PRIMARY KEY (symbol, version)
            );

            CREATE TRIGGER watchlist_membership_no_update
            BEFORE UPDATE ON watchlist_membership
            BEGIN
                SELECT RAISE(ABORT, 'watchlist_membership is append-only: a correction appends a further transition');
            END;

            CREATE TRIGGER watchlist_membership_no_delete
            BEFORE DELETE ON watchlist_membership
            BEGIN
                SELECT RAISE(ABORT, 'watchlist_membership is append-only: rows are never deleted');
            END;

            -- observed_at moves forward per symbol, as set_at does per key
            -- [D-W26], and version ordering is no substitute: version constrains
            -- versions, not visibility. The as-of read filters on observed_at,
            -- so an append carrying a stamp earlier than the symbol's newest
            -- would change what was believed at a past instant after the fact.
            -- With this trigger, each symbol's visible history at any instant
            -- is a prefix of its versions. The snapshot tables deliberately
            -- carry no analogue: they have no version axis crossing the stamp,
            -- and backfill legitimately supplies historical stamps. Equal is
            -- allowed: two transitions can share an instant, and version breaks
            -- the tie.
            CREATE TRIGGER watchlist_membership_observed_at_not_earlier
            BEFORE INSERT ON watchlist_membership
            WHEN NEW.observed_at < (SELECT MAX(observed_at) FROM watchlist_membership WHERE symbol = NEW.symbol)
            BEGIN
                SELECT RAISE(ABORT, 'watchlist_membership observed_at moves forward: a new version cannot predate the newest version of the same symbol');
            END;
            """),
    ];

    /// <summary>
    /// The six snapshot tables of §4.1, their indexes, and the twelve triggers that
    /// make them append-only in the store.
    /// </summary>
    /// <remarks>
    /// <b>observed_at is in three of the keys because a correction appends</b>
    /// [D-W8]. Without it a second row for the same bar violates the key and the
    /// only way to record a vendor correction is an update, which the triggers below
    /// refuse and D-W8 forbids. An as-of read takes the latest observed_at at or
    /// before the as-of instant, which is config_rows' shape with a stamp in place
    /// of a version.
    /// <para>
    /// <b>Nullability follows what a chain can express.</b> Bid and ask are
    /// required; last, the two counts and the five greeks are absent rather than
    /// zero, because a gamma of zero is a false observation and not a missing one.
    /// <see cref="Synthetic.ContractQuote"/> is the same shape, so a chain the
    /// loader accepts is a chain this schema can hold.
    /// </para>
    /// <para>
    /// <b><c>right</c> is unquoted, measured rather than assumed.</b> RIGHT became a
    /// keyword when RIGHT JOIN landed in SQLite 3.39 and the bundled engine is
    /// 3.53.3, so it was worth checking; a probe confirmed the bare identifier
    /// parses in a column definition, in the CHECK, and in a WHERE clause.
    /// </para>
    /// </remarks>
    private static string BuildMarketDataSql()
    {
        var sql = new System.Text.StringBuilder(
            """
            -- The six snapshot tables of DATA_AND_SCHEMA.md 4.1. Never rewritten
            -- [D-W8]: a correction appends a row carrying its own observed_at,
            -- which is why observed_at is in the key of every table that has one.
            CREATE TABLE underlying_bars (
                symbol       TEXT    NOT NULL,
                session_date TEXT    NOT NULL,
                open         TEXT    NOT NULL,
                high         TEXT    NOT NULL,
                low          TEXT    NOT NULL,
                close        TEXT    NOT NULL,
                adj_close    TEXT    NOT NULL,
                volume       INTEGER NOT NULL,
                observed_at  TEXT    NOT NULL,
                PRIMARY KEY (symbol, session_date, observed_at)
            );

            -- No key of its own: a name can carry several actions on one ex-date.
            CREATE TABLE corporate_actions (
                symbol      TEXT NOT NULL,
                ex_date     TEXT NOT NULL,
                kind        TEXT NOT NULL,
                ratio       TEXT NULL,
                amount      TEXT NULL,
                observed_at TEXT NOT NULL
            );

            CREATE TABLE earnings_calendar (
                symbol      TEXT NOT NULL,
                report_date TEXT NOT NULL,
                session     TEXT NOT NULL,
                observed_at TEXT NOT NULL
            );

            CREATE TABLE chain_snapshots (
                symbol        TEXT NOT NULL,
                snapshot_date TEXT NOT NULL,
                observed_at   TEXT NOT NULL,
                PRIMARY KEY (symbol, snapshot_date, observed_at)
            );

            -- multiplier is what a quoted premium multiplies by and an adjustment
            -- does not change it. deliverable_shares is what one contract conveys
            -- on exercise and an adjustment does. Which one the outcome metric uses
            -- is open and owed at Phase 3; neither column is named for its consumer.
            --
            -- UNIQUE on the identity tuple AND the deliverable, not the tuple alone.
            -- An adjusted series can carry a strike that collides with a standard
            -- one on the same underlying and expiry, and the deliverable is what
            -- separates them. A constraint on the tuple would forbid a collision
            -- that occurs. Not on vendor_symbol, though that is the field OCC uses:
            -- a synthetic chain carries none, and NULLs are distinct in a SQLite
            -- unique index (measured), so it would guard nothing until Phase 8.
            -- DATA_AND_SCHEMA.md 2 records that the tuple is not identity.
            CREATE TABLE contracts (
                contract_id             INTEGER PRIMARY KEY,
                symbol                  TEXT    NOT NULL,
                expiry                  TEXT    NOT NULL,
                right                   TEXT    NOT NULL CHECK (right IN ('put', 'call')),
                strike                  TEXT    NOT NULL,
                vendor_symbol           TEXT    NULL,
                predecessor_contract_id INTEGER NULL REFERENCES contracts (contract_id),
                multiplier              INTEGER NOT NULL DEFAULT 100,
                deliverable_shares      INTEGER NOT NULL DEFAULT 100,
                UNIQUE (symbol, expiry, right, strike, deliverable_shares)
            );

            -- bid and ask are required. Everything else is absent rather than zero:
            -- a gamma of zero is a false observation, not a missing one.
            CREATE TABLE contract_quotes (
                contract_id   INTEGER NOT NULL REFERENCES contracts (contract_id),
                snapshot_date TEXT    NOT NULL,
                bid           TEXT    NOT NULL,
                ask           TEXT    NOT NULL,
                last          TEXT    NULL,
                volume        INTEGER NULL,
                open_interest INTEGER NULL,
                iv            TEXT    NULL,
                delta         TEXT    NULL,
                gamma         TEXT    NULL,
                theta         TEXT    NULL,
                vega          TEXT    NULL,
                observed_at   TEXT    NOT NULL,
                PRIMARY KEY (contract_id, snapshot_date, observed_at)
            );

            -- The two tables with no key of their own, both read as-of, plus the
            -- only access path to a predecessor link. The three keyed tables need
            -- nothing: a primary key ending in observed_at is already the index an
            -- as-of read wants.
            CREATE INDEX corporate_actions_as_of
                ON corporate_actions (symbol, ex_date, observed_at);

            CREATE INDEX earnings_calendar_as_of
                ON earnings_calendar (symbol, report_date, observed_at);

            CREATE INDEX contracts_predecessor
                ON contracts (predecessor_contract_id);

            """);

        // Twelve triggers rather than trust in the detector.
        // FX-NoRewriteOfAppendOnlyTables reads src/ only, so it cannot see a writer
        // at a sqlite3 prompt. These hold against any writer, and cost nothing on
        // INSERT, which is all an ingest does. No monotonic third trigger: that one
        // is set_at moving forward for a key [D-W26] and has no analogue here,
        // because these tables correct by appending an observation.
        foreach (var table in MarketDataTables)
        {
            // contracts is the one snapshot table with no observed_at [4.1], so the
            // message that fits the other five would overclaim for it: a corporate
            // action mints a new identity rather than restating an old row.
            var correction = table == "contracts"
                ? "a corporate action mints a new identity rather than editing this row"
                : "a correction appends a row with its own observed_at";

            sql.Append(
                $"""

                CREATE TRIGGER {table}_no_update
                BEFORE UPDATE ON {table}
                BEGIN
                    SELECT RAISE(ABORT, '{table} is append-only: {correction}');
                END;

                CREATE TRIGGER {table}_no_delete
                BEFORE DELETE ON {table}
                BEGIN
                    SELECT RAISE(ABORT, '{table} is append-only: rows are never deleted');
                END;

                """);
        }

        return sql.ToString();
    }
}
