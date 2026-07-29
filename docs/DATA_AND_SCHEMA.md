# DATA_AND_SCHEMA

Build state: **partly built**. The Time section and §4.5 are implemented at 0.3,
along with the point-in-time config rule of §3. §2's ticker and identity
paragraphs, its date-form paragraph, the money line of §4 and the permitted
values of `right` are implemented at 0.4; §2's corporate-action paragraph is
Phase 1. Every other table is specification: market data is Phase 1, decisions
and trials Phase 4, scores Phase 5, pre-registration Phase 9.

## 1. Sources

| What | Source | Plan |
|---|---|---|
| Option chains, greeks, IV | EODHD options add-on | Marketplace add-on, separate purchase |
| Underlying daily bars | EODHD base | Base subscription |
| Dividends and splits | EODHD base | Base subscription |
| Earnings calendar | EODHD base | Base subscription |
| Fundamentals for the ownership screen | EODHD base | Base subscription |

The base All-In-One plan does not include options data [D-W7]. Verify the
add-on's current price and stated history depth on the marketplace listing before
purchase; the product page and earlier notes have disagreed on depth, quoting one
year and roughly two and a half years respectively. Either way it is shallow, and
the design assumes shallow.

Purchase is not required before Phase 8. Everything up to that point runs on
synthetic fixtures.

## 2. Identity

Tickers use the EODHD dash form, for example `BRK-B`.

A ticker has two forms and they are not interchangeable. The store uses the bare
EODHD dash form, `BRK-B`. Requests to the vendor take the exchange-suffixed
form, `BRK-B.US`. The suffix is added at the boundary and never stored, so a
stored ticker is always comparable to another stored ticker.

The stored form of a date is `yyyy-MM-dd` and is rendered through the same kind
of helper as a timestamp. `InvariantGlobalization` makes the invariant short-date
form `MM/dd/yyyy`, so a date stringified without an explicit format is
culture-independent and still wrong.

An option contract's identity is the tuple of underlying, expiry, right, and
strike. The vendor's contract symbol is stored but is not the key, because
contract symbol conventions change on splits and special dividends and a stored
key that moves would silently break historical joins.

Underlying corporate actions adjust contracts. When a split or special dividend
adjusts strikes and deliverables, the adjusted contract is a **new** identity
with a recorded predecessor link, rather than an edit of the existing one. This
follows from snapshots being append-only [D-W8].

## 3. Point-in-time rules

Three, and they are the ones that make historical runs capable of failing.

**Snapshots are append-only.** A stored snapshot records what was observable that
date and is never rewritten [D-W8]. Vendor corrections arrive as new rows with
their own `observed_at`. A delete or an update against a snapshot table fails the
build.

**Membership is state.** Watchlist membership carries entry and exit dates, and a
query about a past date resolves membership as of that date [D-W9].

**Reads are as-of.** Every read path that serves a simulated date takes that date
as a parameter and filters on `observed_at <= as_of`. There is no read path that
returns "current" data to a simulated date.

## Time

Two forms, both UTC, both fixed width, because every as-of read is a string
comparison and a variable-width or local-time value misorders silently rather
than failing.

**Dates**: `yyyy-MM-dd`. Session dates, expiries, ex-dates, report dates.
**Timestamps**: `yyyy-MM-ddTHH:mm:ss.fffZ`. Every column ending `_at`.

A date and a timestamp never appear on opposite sides of a comparison. A
timestamp for any instant on a day sorts after that day's bare date, so
`set_at <= as_of` with a timestamp column and a date parameter excludes
everything written on the as-of date itself. Where a simulated date must be
compared against a timestamp column it is widened to that date's last instant
first, and the widening happens in exactly one place.

Filenames cannot carry the stored timestamp form, because `:` is illegal in a
Windows path. A timestamp used in a filename is written `yyyyMMddTHHmmssfffZ`,
the same instant with the separators removed. The two forms are never mixed:
stored columns take the first, filenames the second.

## 4. Schema

Snapshot-first migrations: the migration runner takes a database snapshot before
applying, and `migrate.ps1` is the operator entry point that invokes the runner,
so a hand-run cannot skip the snapshot.

The store runs in WAL journal mode, set once and persisted with the database. A
snapshot is taken with `VACUUM INTO` [D-W28], producing one consistent file from
the committed state including whatever has not yet checkpointed. No lock is
required and no writer is blocked.

Migrations snapshot before applying, whenever there is something to protect. The
first run against a store that does not exist yet has nothing to copy, which is
a base case rather than an exception, and the runner records that it was
skipped.

Money and every other decimal is stored as decimal in `TEXT` columns, never as
floating point, in the canonical fixed-scale form [D-W29]. The scale is a single
declared constant, wide enough for the most precise value any column carries.
Decimal columns are not ordered, ranged over, or aggregated in SQL.

### 4.0 The migration ledger

```
schema_migrations
  id INTEGER PK, name TEXT, applied_at TEXT
```

Records which migrations have been applied, which is where a store's schema
version comes from rather than from `PRAGMA user_version` [0.3]. Never rewritten
[D-W32].

### 4.1 Market data

These six are the **snapshot tables**: they record what was observable on a date
and are never rewritten [D-W8]. `contracts` is one of them despite carrying no
`observed_at`, because a corporate action mints a new identity with a predecessor
link rather than editing the existing row [§2]. The phrase is used in four
documents and this is where it is defined.

```
underlying_bars
  symbol TEXT, session_date TEXT, open TEXT, high TEXT, low TEXT, close TEXT,
  adj_close TEXT, volume INTEGER, observed_at TEXT
  PK (symbol, session_date)

corporate_actions
  symbol TEXT, ex_date TEXT, kind TEXT, ratio TEXT, amount TEXT, observed_at TEXT

earnings_calendar
  symbol TEXT, report_date TEXT, session TEXT, observed_at TEXT

chain_snapshots
  symbol TEXT, snapshot_date TEXT, observed_at TEXT
  PK (symbol, snapshot_date)

contracts
  contract_id INTEGER PK, symbol TEXT, expiry TEXT, right TEXT, strike TEXT,
  vendor_symbol TEXT, predecessor_contract_id INTEGER NULL, multiplier INTEGER

contract_quotes
  contract_id INTEGER, snapshot_date TEXT, bid TEXT, ask TEXT, last TEXT,
  volume INTEGER, open_interest INTEGER, iv TEXT, delta TEXT, gamma TEXT,
  theta TEXT, vega TEXT, observed_at TEXT
  PK (contract_id, snapshot_date)
```

`right` is `put` or `call`, lower case, matching the house convention for
enumerated text elsewhere in this schema.

### 4.2 Universe

```
watchlist_membership
  symbol TEXT, entered_on TEXT, left_on TEXT NULL, reason TEXT, observed_at TEXT
```

`left_on` null means currently a member. Rows are never deleted.

### 4.3 Decisions and trials

```
decisions
  decision_id INTEGER PK, maker_id TEXT, decision_date TEXT, symbol TEXT,
  kind TEXT, chosen_candidate_id INTEGER NULL, trial_id INTEGER NULL,
  policy_version INTEGER, recorded_at TEXT

candidates
  candidate_id INTEGER PK, decision_id INTEGER, contract_id INTEGER,
  contracts_qty INTEGER, committed_capital TEXT, credit TEXT,
  feature_json TEXT, gate_status TEXT, gate_reason TEXT NULL
```

`kind` is one of `open_put`, `open_call`, `roll`, `close`, `none`.
`gate_status` is `feasible` or `rejected`; rejected candidates are recorded so
the gate's effect is auditable [D-W10].
`chosen_candidate_id` null means the maker chose to do nothing, which is a
decision and is scored.

```
trials
  trial_id INTEGER PK, maker_id TEXT, symbol TEXT, opened_on TEXT,
  closed_on TEXT NULL, open_strike TEXT, committed_capital TEXT,
  rolls_used INTEGER, close_kind TEXT NULL

positions
  trial_id INTEGER, state TEXT, effective_from TEXT, effective_to TEXT NULL,
  shares INTEGER, gross_basis TEXT, net_basis TEXT, contract_id INTEGER NULL

ledger_entries
  entry_id INTEGER PK, trial_id INTEGER, entry_date TEXT, kind TEXT,
  amount TEXT, contract_id INTEGER NULL, note TEXT
```

`state` is the discriminated union tag: `cash`, `short_put`, `holding_shares`,
`short_call`. Both basis conventions are stored [D-W19].

### 4.4 Scores

```
outcomes
  trial_id INTEGER PK, return_on_committed TEXT, duration_days INTEGER,
  max_adverse_excursion TEXT, computed_at TEXT

candidate_outcomes
  candidate_id INTEGER PK, fast_score TEXT, slow_score TEXT,
  fast_rank INTEGER, slow_rank INTEGER, computed_at TEXT

decision_scores
  decision_id INTEGER PK, chosen_rank INTEGER, regret TEXT,
  feasible_count INTEGER, computed_at TEXT
```

`return_on_committed` is the raw period return, not annualized [D-W18].
`duration_days` is the separate field that makes report-time annualization
possible.

### 4.5 Configuration

```
config_rows
  key TEXT, version INTEGER, value TEXT, set_at TEXT, note TEXT
  PK (key, version)
```

Append-only and versioned; current is `MAX(version)` for a key. This is what
allows a parameter change to be explained after the fact rather than discovered.

### 4.6 Pre-registration

```
preregistrations
  prereg_id INTEGER PK, committed_at TEXT, content_hash TEXT, body TEXT
```

The forward run refuses to start unless a row exists whose `committed_at`
predates the first forward decision [D-W15].

## 5. Storage volume

Chains are wide. Roughly two hundred contracts per name per snapshot date, times
twenty watchlist names, times two hundred and fifty sessions a year, is on the
order of one million quote rows a year. That is comfortable for SQLite, and it is
the reason the design is watchlist-driven rather than universe-wide. The
watchlist is a storage decision as much as a strategy decision.
