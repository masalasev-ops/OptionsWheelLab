namespace OptionsWheelLab.Core.Storage;

/// <summary>One schema change, applied once and recorded.</summary>
public sealed record Migration(int Id, string Name, string Sql);

/// <summary>
/// Every migration, in order.
/// </summary>
/// <remarks>
/// Configuration at 0.3, the market-data tables at 1.1, membership at 1.3,
/// the bars nullability rebuild at 1.4, and at 3.3 the corporate-action CHECK,
/// the session calendar, and §4.3's three.
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

        new Migration(
            5,
            "underlying_bars_nullability",
            """
            -- underlying_bars relaxed to what UnderlyingBar can express: only
            -- the close is required, and open, high, low, adj_close and volume
            -- are absent rather than zero. Five columns, enumerated from the
            -- record; the 1.2 finding that raised this named four, and the
            -- record's fifth is volume.
            --
            -- SQLite cannot alter a column's nullability in place, so this is
            -- a rebuild: create the replacement, copy rows across, drop the
            -- old, rename, and recreate both triggers, which DROP TABLE takes
            -- with it. Rebuilding an append-only table is not a rewrite: no
            -- writer for bars existed before this checkpoint, so no store can
            -- hold rows, and the copy is carried anyway so a hand-populated
            -- store survives. DROP TABLE sits outside the append-only rule's
            -- banned statements deliberately: the rule governs observations,
            -- not schema.
            CREATE TABLE underlying_bars_relaxed (
                symbol       TEXT    NOT NULL,
                session_date TEXT    NOT NULL,
                open         TEXT    NULL,
                high         TEXT    NULL,
                low          TEXT    NULL,
                close        TEXT    NOT NULL,
                adj_close    TEXT    NULL,
                volume       INTEGER NULL,
                observed_at  TEXT    NOT NULL,
                PRIMARY KEY (symbol, session_date, observed_at)
            );

            INSERT INTO underlying_bars_relaxed
                (symbol, session_date, open, high, low, close, adj_close, volume, observed_at)
            SELECT symbol, session_date, open, high, low, close, adj_close, volume, observed_at
            FROM underlying_bars;

            DROP TABLE underlying_bars;

            ALTER TABLE underlying_bars_relaxed RENAME TO underlying_bars;

            CREATE TRIGGER underlying_bars_no_update
            BEFORE UPDATE ON underlying_bars
            BEGIN
                SELECT RAISE(ABORT, 'underlying_bars is append-only: a correction appends a row with its own observed_at');
            END;

            CREATE TRIGGER underlying_bars_no_delete
            BEFORE DELETE ON underlying_bars
            BEGIN
                SELECT RAISE(ABORT, 'underlying_bars is append-only: rows are never deleted');
            END;
            """),

        new Migration(
            6,
            "corporate_actions_kind_check",
            """
            -- corporate_actions.kind gains the CHECK it has gone without since
            -- 1.1. The reason is the one `right` and watchlist_membership.kind
            -- already carry: a stored form the database does not enforce has one
            -- guard rather than two. It was left off while the vocabulary was one
            -- value; it is eight from here [D-W47], and an unenforced vocabulary
            -- starts costing at the second value, not the eighth.
            --
            -- The values are OCC's own enumeration of what adjusts a contract,
            -- complete before the transitions that read it exist. The two
            -- dividend values are D-W44's ordinary and non-ordinary split, and a
            -- reverse split is a `split` whose ratio is below one rather than a
            -- value of its own: the ratio is a recorded fact about the event
            -- [D-W36], and a second name for one event is a second place to get
            -- it wrong.
            --
            -- SQLite cannot add a CHECK in place, so this is migration 5's
            -- rebuild: create the replacement, copy across, drop, rename, and
            -- recreate what DROP TABLE takes with it, which here is both triggers
            -- AND the as-of index.
            --
            -- What differs from migration 5, and it is the whole risk. That one
            -- rebuilt a table no writer had ever touched, so its copy could not
            -- lose a row that existed. This table has had CorporateActionWriter
            -- since 1.5, so the copy carries rows a real store really holds. It
            -- is asserted rather than trusted: a test seeds through the writer,
            -- migrates, and reads the rows back.
            CREATE TABLE corporate_actions_checked (
                symbol      TEXT NOT NULL,
                ex_date     TEXT NOT NULL,
                kind        TEXT NOT NULL CHECK (kind IN (
                                'ordinary_dividend',
                                'non_ordinary_dividend',
                                'split',
                                'rights_offering',
                                'reorganization',
                                'merger',
                                'liquidation',
                                'spin_off')),
                ratio       TEXT NULL,
                amount      TEXT NULL,
                observed_at TEXT NOT NULL
            );

            INSERT INTO corporate_actions_checked
                (symbol, ex_date, kind, ratio, amount, observed_at)
            SELECT symbol, ex_date, kind, ratio, amount, observed_at
            FROM corporate_actions;

            DROP TABLE corporate_actions;

            ALTER TABLE corporate_actions_checked RENAME TO corporate_actions;

            CREATE INDEX corporate_actions_as_of
                ON corporate_actions (symbol, ex_date, observed_at);

            CREATE TRIGGER corporate_actions_no_update
            BEFORE UPDATE ON corporate_actions
            BEGIN
                SELECT RAISE(ABORT, 'corporate_actions is append-only: a correction appends a row with its own observed_at');
            END;

            CREATE TRIGGER corporate_actions_no_delete
            BEFORE DELETE ON corporate_actions
            BEGIN
                SELECT RAISE(ABORT, 'corporate_actions is append-only: rows are never deleted');
            END;
            """),

        new Migration(
            7,
            "market_sessions",
            """
            -- The session calendar of §4.1, transcribed and never derived
            -- [D-W46]. It answers what the next session after a date is, which
            -- settlement needs [D-W40] and nothing in this store could answer.
            --
            -- No symbol, which is the point of it. A session is a fact about the
            -- market rather than about a name, and underlying_bars.session_date
            -- is per symbol and cannot tell a market holiday from a name that
            -- did not trade.
            --
            -- A snapshot table, so a correction appends a row carrying its own
            -- observed_at [D-W8, D-W46] and the stamp is in the key. A
            -- transcribed session that could be edited would move a past date's
            -- answer, which is exactly what the decision refuses to let a derived
            -- one do.
            --
            -- No monotonic third trigger. That one is set_at moving forward per
            -- key [D-W26] and observed_at per symbol [1.3]; this table has no
            -- axis crossing the stamp, and backfill legitimately supplies
            -- historical stamps, which is the reason the six snapshot tables
            -- carry no analogue either.
            CREATE TABLE market_sessions (
                session_date TEXT NOT NULL,
                observed_at  TEXT NOT NULL,
                PRIMARY KEY (session_date, observed_at)
            );

            CREATE TRIGGER market_sessions_no_update
            BEFORE UPDATE ON market_sessions
            BEGIN
                SELECT RAISE(ABORT, 'market_sessions is append-only: a correction appends a row with its own observed_at');
            END;

            CREATE TRIGGER market_sessions_no_delete
            BEFORE DELETE ON market_sessions
            BEGIN
                SELECT RAISE(ABORT, 'market_sessions is append-only: rows are never deleted');
            END;
            """),

        new Migration(
            8,
            "trials_positions_ledger",
            """
            -- §4.3's three, the first tables Phase 3 writes.
            --
            -- This migration was edited in place after it was first written,
            -- adding two CHECK vocabularies. 0.3 took the other course, a new
            -- migration rather than amending migration 1, and stated the rule
            -- that decides between them: an amended migration never re-runs, so
            -- amending is only available while nothing has run it. That is a
            -- condition rather than a prohibition, and it was measured absent
            -- here rather than assumed. main carried five migrations; no store
            -- file existed in the tree; Storage__Path was unset at every scope,
            -- so StoreLocation refused and migrate.ps1 could not run; and 3.3's
            -- detail carries no demonstration bullet, both its tests running
            -- against per-test stores that are created and destroyed. Once this
            -- branch merges the same change is a new migration.
            --
            -- ledger_entries is the record and carries both triggers; trials and
            -- positions are projections of it and deliberately carry none. That
            -- is [D-W35]'s two halves in one migration: a record is the only
            -- place a fact is held, and a projection is derived from an
            -- append-only source and may be rebuilt. The permission to rebuild is
            -- conditional on the test that discards and rebuilds them, which is
            -- this checkpoint's definition of done rather than a comment here.
            --
            -- entry_date is the session an entry occurred in and known_on the
            -- session the account could act on it [D-W39]. Both are stored
            -- because a projection rebuilt from this table has to reproduce what
            -- was known when, and one date cannot answer both.
            --
            -- kind carries a CHECK and records events rather than only cash
            -- [D-W48], so an expiry that pays nothing is a row with a zero
            -- amount. commission and assignment_fee are in the vocabulary before
            -- anything writes them: whether the fill model gives them entries of
            -- their own is 3.4's, and a value nothing writes costs nothing where
            -- a migration adding one costs the rebuild above.
            --
            -- Both bases are nullable, corrected at 3.3 against §4.3's unmarked
            -- convention. Cost basis exists after assignment [D-W19], so cash and
            -- short_put have none, and NOT NULL would have made two of the four
            -- states unwritable. A zero basis would be a false observation, not a
            -- missing one.
            --
            -- close_kind carries its own CHECK. Its values are what returns a
            -- trial to cash rather than what the schema found convenient, and
            -- closed_at_bound is one value because D-W14 names one mechanism with
            -- two triggers: rolls_used beside opened_on and closed_on says which
            -- of them fired, so two values would state one fact twice.
            --
            -- closed_by_choice is in the CHECK before anything writes it. No
            -- maker exists until Phase 4, and it is recoverable from the day one
            -- does, being a bought_to_close with no premium_received following.
            -- That is why the ledger has both kinds: a roll pays a premium and
            -- opens a position, a close pays a premium and ends one, and a trial
            -- closed at its last permitted roll and one closed by choice look
            -- identical in the sequence alone.
            --
            -- No foreign keys, which §4.3 already said by carrying no arrows
            -- where §4.1 carries three. It read as an omission and is not, and
            -- one of them would have been a defect: a reference from
            -- ledger_entries into trials points the record at the projection
            -- derived from it, so discarding trials to rebuild it would be
            -- refused by the store. Written that way first and found by the test
            -- that discards a projection, which is the rebuild condition [D-W35]
            -- earning its place before the rebuild exists.
            CREATE TABLE trials (
                trial_id          INTEGER PRIMARY KEY,
                maker_id          TEXT    NOT NULL,
                symbol            TEXT    NOT NULL,
                opened_on         TEXT    NOT NULL,
                closed_on         TEXT    NULL,
                open_strike       TEXT    NOT NULL,
                committed_capital TEXT    NOT NULL,
                rolls_used        INTEGER NOT NULL,
                close_kind        TEXT    NULL CHECK (close_kind IN (
                                      'expired_worthless',
                                      'called_away',
                                      'closed_at_bound',
                                      'closed_by_choice',
                                      'stopped'))
            );

            -- No key of its own, on corporate_actions' precedent: a trial carries
            -- several positions and what would distinguish them is a question
            -- the state machine answers, not the schema.
            CREATE TABLE positions (
                trial_id       INTEGER NOT NULL,
                state          TEXT    NOT NULL CHECK (state IN (
                                   'cash', 'short_put', 'holding_shares', 'short_call')),
                effective_from TEXT    NOT NULL,
                effective_to   TEXT    NULL,
                shares         INTEGER NOT NULL,
                gross_basis    TEXT    NULL,
                net_basis      TEXT    NULL,
                contract_id    INTEGER NULL
            );

            CREATE TABLE ledger_entries (
                entry_id    INTEGER PRIMARY KEY,
                trial_id    INTEGER NOT NULL,
                entry_date  TEXT    NOT NULL,
                known_on    TEXT    NOT NULL,
                kind        TEXT    NOT NULL CHECK (kind IN (
                                'premium_received',
                                'premium_paid',
                                'bought_to_close',
                                'expired_worthless',
                                'assignment',
                                'call_away',
                                'shares_sold',
                                'dividend',
                                'commission',
                                'assignment_fee',
                                'stopped')),
                amount      TEXT    NOT NULL,
                contract_id INTEGER NULL,
                note        TEXT    NULL
            );

            CREATE TRIGGER ledger_entries_no_update
            BEFORE UPDATE ON ledger_entries
            BEGIN
                SELECT RAISE(ABORT, 'ledger_entries is append-only: it is the record trials and positions are rebuilt from');
            END;

            CREATE TRIGGER ledger_entries_no_delete
            BEFORE DELETE ON ledger_entries
            BEGIN
                SELECT RAISE(ABORT, 'ledger_entries is append-only: rows are never deleted');
            END;

            -- The rebuild reads every entry for a trial in order, which is the
            -- only query these tables have until the state machine has more.
            CREATE INDEX ledger_entries_by_trial
                ON ledger_entries (trial_id, entry_date, entry_id);

            CREATE INDEX positions_by_trial
                ON positions (trial_id, effective_from);
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
