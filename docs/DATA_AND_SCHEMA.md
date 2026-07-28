# DATA_AND_SCHEMA

Build state: **not built**.

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
their own `observed_at`. CI greps for `DELETE FROM` and `UPDATE` against snapshot
tables.

**Membership is state.** Watchlist membership carries entry and exit dates, and a
query about a past date resolves membership as of that date [D-W9].

**Reads are as-of.** Every read path that serves a simulated date takes that date
as a parameter and filters on `observed_at <= as_of`. There is no read path that
returns "current" data to a simulated date.

## 4. Schema

Snapshot-first migrations: the migration runner takes a database snapshot before
applying, and `migrate.ps1` calls the snapshot tool internally first.

Money is stored as decimal in `TEXT` columns, never as floating point.

### 4.1 Market data

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
