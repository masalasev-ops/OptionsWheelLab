# DATA_AND_SCHEMA

Build state: **partly built**. The Time section and §4.5 are implemented at 0.3,
along with the point-in-time config rule of §3. §2's ticker and identity
paragraphs, its date-form paragraph, the money line of §4 and the permitted
values of `right` are implemented at 0.4.
§4.0 is implemented at 0.7 and §4.1 at 1.1, with its keys, constraints,
triggers and indexes. §4.2 is implemented at 1.3, its shape settled by D-W35 as
transitions; §2's corporate-action paragraph is implemented at 1.5, its
identity paragraph corrected and implemented there too [D-W36]. The rest is
specification, and §4.3 splits across two phases rather than falling to one:
`trials`, `positions` and `ledger_entries` at 3.3, `decisions` and `candidates`
Phase 4, scores Phase 5, pre-registration Phase 9. `ledger_entries` gained
`known_on` at 3.1 while still specification, deduced from [D-W35] rather than
built. §4.1 grew at 3.3, which added `market_sessions` and rebuilt
`corporate_actions` for its `CHECK`; §4.3's `trials`, `positions` and
`ledger_entries` are implemented there too, leaving `decisions` and `candidates`
as the section's remaining specification.

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

An option contract's identity is the tuple of underlying, expiry, right and
strike, **together with its deliverable**. An adjusted series can share all
four of the first components with a standard contract, differing only in what
it delivers: a three-for-two split's successor at a 60 strike with 150 shares
lists alongside a standard 60 with 100. The store's uniqueness constraint has
carried the five components since 1.1; the identity type carries them from
1.5. The vendor's contract symbol is stored but is not the key, because symbol
conventions change on adjustment and a stored key that moves would silently
break historical joins.

Underlying corporate actions adjust contracts. When a split or special dividend
adjusts strikes and deliverables, the adjusted contract is a **new** identity
with a recorded predecessor link, rather than an edit of the existing one. This
follows from snapshots being append-only [D-W8].

## 3. Point-in-time rules

Three, and they are the ones that make historical runs capable of failing.

**Snapshots are append-only.** A stored snapshot records what was observable that
date and is never rewritten [D-W8]. Vendor corrections arrive as new rows with
their own `observed_at`. A delete or an update against a snapshot table fails the
build, and from 1.1 it also fails in the store: each of the six carries a pair of
triggers refusing `UPDATE` and `DELETE`. The two guards cover different writers. The
build check reads `src/` and cannot see a hand-written statement at a `sqlite3`
prompt; the triggers hold against any writer and say nothing about source.

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

These seven are the **snapshot tables**: they record what was observable on a date
and are never rewritten [D-W8]. `contracts` is one of them despite carrying no
`observed_at`, because a corporate action mints a new identity with a predecessor
link rather than editing the existing row [§2]. The phrase is used in four
documents and this is where it is defined. Six landed at 1.1; `market_sessions`
joined them at 3.3.

```
market_sessions
  session_date TEXT, observed_at TEXT
  PK (session_date, observed_at)

underlying_bars
  symbol TEXT, session_date TEXT, open TEXT NULL, high TEXT NULL,
  low TEXT NULL, close TEXT, adj_close TEXT NULL, volume INTEGER NULL,
  observed_at TEXT
  PK (symbol, session_date, observed_at)

corporate_actions
  symbol TEXT, ex_date TEXT, kind TEXT, ratio TEXT NULL, amount TEXT NULL,
  observed_at TEXT
  CHECK (kind IN ('ordinary_dividend', 'non_ordinary_dividend', 'split',
                  'rights_offering', 'reorganization', 'merger', 'liquidation',
                  'spin_off'))

earnings_calendar
  symbol TEXT, report_date TEXT, session TEXT, observed_at TEXT

chain_snapshots
  symbol TEXT, snapshot_date TEXT, observed_at TEXT
  PK (symbol, snapshot_date, observed_at)

contracts
  contract_id INTEGER PK, symbol TEXT, expiry TEXT, right TEXT, strike TEXT,
  vendor_symbol TEXT NULL, predecessor_contract_id INTEGER NULL -> contracts,
  multiplier INTEGER, deliverable_shares INTEGER
  CHECK (right IN ('put', 'call'))
  UNIQUE (symbol, expiry, right, strike, deliverable_shares)

contract_quotes
  contract_id INTEGER -> contracts, snapshot_date TEXT, bid TEXT, ask TEXT,
  last TEXT NULL, volume INTEGER NULL, open_interest INTEGER NULL, iv TEXT NULL,
  delta TEXT NULL, gamma TEXT NULL, theta TEXT NULL, vega TEXT NULL,
  observed_at TEXT
  PK (contract_id, snapshot_date, observed_at)
```

Unmarked columns are `NOT NULL`. **Nullability follows what a chain can express.**
Bid and ask are required; last, both counts and the five greeks are absent rather
than zero, because a gamma of zero is a false observation and not a missing one, and
`ContractQuote` is the same shape. A split carries a ratio and a special dividend an
amount, so `corporate_actions` has one of each. A chain the loader accepts is a chain
this schema holds.

`market_sessions` carries no symbol, which is the point of it. A session is a
fact about the market and not about a name, and the only session sequence this
schema otherwise holds is `underlying_bars.session_date`, which is per symbol and
cannot tell a market holiday from a name that did not trade [D-W46]. It is
transcribed rather than derived, so it is a snapshot table like the rest and a
correction to it appends.

`corporate_actions.kind` carries OCC's own enumeration of the events that adjust
a contract, complete before the transitions that read it are written [D-W47]. The
two dividend values are the ordinary and non-ordinary split that [D-W44] draws,
and which side an event falls on is transcribed per event [D-W36]. A reverse
split is a `split` whose ratio is below one rather than a value of its own. The
`CHECK` is here for the reason `right` has one below.

`session` is `before_open`, `after_close` or `unspecified`, lower case, matching
`right` and `kind` elsewhere in this schema. It records when in the session the
report lands, which [D-W25]'s buffered constraint does not read: a buffer
measured in days is indifferent to the hour. It is carried because a narrower
buffer would need it, and because a vendor that supplies it should not have the
fact discarded on the way in. `unspecified` is the honest value when the vendor
gives none, and is not a default standing in for a guess.

`->` marks a foreign key. **They are enforced**, not decorative:
Microsoft.Data.Sqlite turns foreign keys on by default, which a bare `sqlite3`
prompt does not, so a quote cannot point at a contract that does not exist and a
predecessor link cannot dangle.

The observation stamp is part of the key because a correction appends rather than
replaces [D-W8]. Without it a second row for the same bar violates the key, the
only way to record a vendor correction is an update, and
`FX-NoRewriteOfAppendOnlyTables` refuses it. An as-of read takes the latest
`observed_at` at or before the as-of instant, which is `config_rows`' shape with
an observation stamp in place of a version.

Three indexes, and each names the query it serves:

```
corporate_actions (symbol, ex_date, observed_at)
earnings_calendar (symbol, report_date, observed_at)
contracts        (predecessor_contract_id)
```

The first two are the tables with no key of their own, and both are read as-of: the
actions in force for a name at a date, and the clearance window either side of a
report date [D-W25]. The third is the only access path to a predecessor link and
exists for the join across a split.

The three keyed tables need no separate index. A primary key carrying
`observed_at` last is already the index an as-of read wants, since "the latest
`observed_at` at or before X for this key" is a prefix scan on it.

**Deliberately not indexed yet**: every quote for a name on a date as of an instant,
which is the query the gate will run. Whether it reaches `contract_quotes` by joining
`contracts` on `symbol` or through a denormalised column is 1.2's to define, and
indexing it before then is a guess at a join. The `UNIQUE` constraint below yields a
`contracts (symbol, expiry, ...)` prefix, which serves the join half either way.

`right` is `put` or `call`, lower case, matching the house convention for
enumerated text elsewhere in this schema. The database enforces it with a `CHECK`
as well, because a stored form only the code enforces has one guard.

**`contracts` carries two quantities that are easy to read as one.**
`multiplier` is the number a quoted premium multiplies by to give the cash paid for
one contract, and an adjustment does not change it. `deliverable_shares` is what one
contract conveys on exercise, and an adjustment does change it. **The outcome
metric uses the multiplier**, settled at 3.1 against the filing that states the
method in force [D-W17, as amended]: an adjustment moves the deliverable and
leaves the strike and the aggregate exercise price where it found them, so
committed capital is strike times multiplier and a metric reading the deliverable
would misprice every adjusted position. The deliverable keeps the job the next
paragraph gives it.

A contract is unique on its identity tuple together with what it delivers. An
adjusted series can carry a strike that collides with a standard one on the same
underlying and expiry, and the deliverable is what separates them: the multiplier
stays at one hundred through an adjustment and cannot. This is deliberately weaker
than a constraint on the tuple alone, which would forbid a collision that occurs,
and it stops the same contract being inserted twice, which is the live defect.

Not `vendor_symbol`, though it is the field OCC uses. A synthetic chain carries
none, SQLite treats nulls in a unique index as distinct, and the constraint would
guard nothing until Phase 8 while the duplicate-insert bug is live from 1.4.

The residual: OCC says an option symbol without a numeric suffix will almost always
designate a standard option, and in rare instances a symbol without one may
nevertheless represent a non-standard option. So no field is perfectly
discriminating, and a constraint on the deliverable is the best available rather
than complete.

### 4.2 Universe

```
watchlist_membership
  symbol TEXT, version INTEGER, effective_on TEXT, kind TEXT,
  reason TEXT NULL, observed_at TEXT
  PK (symbol, version)
```

`reason` is nullable on `config_rows.note`'s precedent, an operator
annotation, and forcing one onto every correction manufactures noise.

Each row records one transition, not an interval. `kind` is `joined` or
`left`, lower case, matching `right` in §4.1. Membership on a date, as known
at an instant, is resolved among rows whose `effective_on` is at or before
the date and whose `observed_at` is at or before the instant: the row with
the greatest (`effective_on`, `version`) governs, and the name is a member
when that row is a `joined`. Version breaks ties on one date, so an appended
correction supersedes a transition only by tying its date; correcting a
transition's date is a compensating pair, a counter-transition at the false
date plus the true transition at the true date.

**An interval per version cannot answer the question.** Stating
`entered_on` and `left_on` on each version, the way `config_rows` states a
value, breaks on re-entry. A name that joined in March, left in August and
returned in January has a newest version saying it entered in January, which
cannot say what June was; and reading every version instead returns the
March row, which says no departure and so covers September too. The fact
being recorded is a sequence of transitions rather than a single current
value, so the row records a transition.

D-W35's key is unchanged: symbol and version, and keying on the symbol alone
cannot express re-entry.

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
  shares INTEGER, gross_basis TEXT NULL, net_basis TEXT NULL,
  contract_id INTEGER NULL

ledger_entries
  entry_id INTEGER PK, trial_id INTEGER, entry_date TEXT, known_on TEXT,
  kind TEXT, amount TEXT, contract_id INTEGER NULL, note TEXT NULL
```

**A record cannot reference a projection, so `ledger_entries` carries no foreign
key into `trials` or `positions`.** The rule arrives in two steps and both are the
same decision's: `trials` and `positions` are projections of `ledger_entries` and
may be rebuilt, and the permission to rewrite one holds only where a test discards
it and rebuilds it from its source [D-W35]. A reference from the record would
refuse the discard, so the second step is what makes the first unsatisfiable. The
three arrows in §4.1 run between records, where nothing is ever discarded, which
is why the absence of arrows here is a statement rather than an omission. Written
the other way first at 3.3 and found by the test that discards a projection.

Whether `contract_id` should reference `contracts` the way
`contract_quotes.contract_id` does is a separate question and open: that target is
a record and outlives every rebuild, so it carries none of the same risk.

**Both bases are nullable, corrected at 3.3 when the table was built.** Cost basis
exists after assignment [D-W19], so a position in `cash` or `short_put` has none,
and under the unmarked convention above they would have been `NOT NULL` and made
two of the four states unwritable. A zero would be a false observation rather
than a missing one, which is the rule §4.1 states about a gamma.

`note` is nullable on `config_rows.note`'s precedent, which §4.2 already cites
for the same shape. That one is a judgement rather than a correction: no decision
says whether a ledger entry must carry a note, and a required one would make
every row invent boilerplate.

`state` is the discriminated union tag: `cash`, `short_put`, `holding_shares`,
`short_call`. Both basis conventions are stored [D-W19].

`ledger_entries.kind` is `premium_received`, `premium_paid`, `bought_to_close`,
`expired_worthless`, `assignment`, `call_away`, `shares_sold`, `dividend`,
`commission`, `assignment_fee` or `stopped`, with a `CHECK` as every other stored
vocabulary here has. **The table records events and not only cash** [D-W48], so an
expiry that pays nothing is a row carrying a zero amount: the projection rebuilt
from this table has to know the short closed and no other table says so, which is
what `WORKED_EXAMPLE.md` §6.3 already shows by giving its worthless expiry a leg of
its own. The pairs are there because one cash direction covers two events. A
short leaves by expiring, by assignment or by being bought back, and only the
last is a premium; shares leave at the strike when called away or at market when
the roll bound binds [D-W14]; and a buy-back either rolls into a new leg or ends
the trial, which the sequence cannot tell apart after the fact. `commission` and
`assignment_fee` are in the vocabulary before anything writes them, because
whether the fill model gives them entries of their own is 3.4's [D-W12] and a
value nothing writes costs nothing where a migration adding one costs a rebuild.

`trials.close_kind` is `expired_worthless`, `called_away`, `closed_at_bound`,
`closed_by_choice` or `stopped`, with its own `CHECK`. They are what returns a
trial to cash: the short expired with no shares ever held [D-W38]; shares were
taken at the strike [D-W19]; the position closed at market when `Trial:MaxRolls`
or `Trial:MaxTrialDays` bound [D-W14]; a maker bought the short back to end the
trial rather than to roll it; or an action the lab does not model ended it
[D-W47]. `closed_at_bound` is one value because D-W14 names one mechanism with two
triggers, and `rolls_used` beside `opened_on` and `closed_on` says which fired, so
two values would state one fact twice. Nothing writes `closed_by_choice` before
Phase 4 has a maker, and it is recoverable from the day one does, being a
`bought_to_close` with no `premium_received` following.

`entry_date` is the session an entry occurred in and `known_on` the session the
account could act on it. They differ because assignment is determined after the
close and is known the next morning [D-W39], and both are stored because a
projection rebuilt from this table [D-W35] must reproduce what was known when,
which one date cannot answer. `_on` rather than `_at`, because §Time reserves
that suffix for timestamps and this is a date, matching `opened_on` and
`closed_on`. `decisions` carries the same pair one block above, `decision_date`
beside `recorded_at`, so a second date on a row is this schema's convention
rather than an exception here.

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
  key TEXT, version INTEGER, value TEXT, set_at TEXT, note TEXT NULL
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
