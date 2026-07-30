# Phase 1 Chain store and point-in-time invariants: spent prompts

Current state below is the whole state of the repository and the only description
of the present. Phase 0's file holds the state as it stood at that phase's close and
is not corrected further.

One prompt per checkpoint, being the prompt that produces the checkpoint as it now
stands. Corrections found while building are folded back into the checkpoint's
prompt rather than appended as further entries, so replaying the prompts in order
against the corpus reproduces the current state without replaying the mistakes.

One file per phase. It closes when Phase 1 signs off; Phase 2 opens its own.

---

# Current state

Corpus v1.25.0.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| Phase 1 | 1.1 to 1.4 built and signed off; 1.5 not built |
| CI | green, 365 tests, guards then restore then build then test, on push to `main` and every pull request |

Which branch the work sits on and which pull requests have merged are not recorded
here. Git holds both exactly, and a fact kept in two places drifts.

## Build

.NET 10. Four projects: `Core`, `Worker`, `Api`, `Tests`. Warnings are errors,
nullable is on, `InvariantGlobalization` is on, code style is enforced in the build.
Central package management with transitive pinning.

`Core` has seven folders: `Configuration`, `Storage`, `Identity`, `Time`,
`Synthetic`, `MarketData` and `Membership`.

Repository root holds `README.md`, `CLAUDE.md`, `migrate.ps1`, `seed.ps1` and
`guards.ps1`. Every document is in `docs/`, spent prompts in `prompts/spent/`,
hand-written synthetic chains in `synthetic/`.

## Store

SQLite, WAL, one writer. The Worker is the sole writer [D-W1] and the Api opens
read-only, which is a property of the connection string rather than a convention.
Pooling is off on every connection, which is load-bearing on Windows: with pooling
on the native handle outlives `Dispose` and the snapshot copy fails on a locked file.

Foreign keys are enforced. Microsoft.Data.Sqlite turns them on by default, which a
bare `sqlite3` prompt does not, so a reference cannot dangle through the code path
and can through a hand-written one.

Snapshot-first migrations. The runner takes a `VACUUM INTO` snapshot before applying
[D-W28] and `migrate.ps1` is the operator entry point, so a hand-run cannot skip it.
Schema version comes from `schema_migrations` rather than `PRAGMA user_version`
[D-W32].

**Schema 5.** Migration 1 is `config_rows`, 2 its monotonic `set_at` trigger, 3 the
six market-data tables of §4.1 [1.1], 4 the membership record [1.3], 5 the bars
nullability rebuild [1.4].

## Market data

Six tables: `underlying_bars`, `corporate_actions`, `earnings_calendar`,
`chain_snapshots`, `contracts`, `contract_quotes`. Two of the six carry no key of
their own and have an index instead; three carry `observed_at` in the key, because a
correction appends rather than replaces [D-W8].

`contracts` carries no `observed_at`, since a corporate action mints a new identity
rather than restating a row. It carries two quantities that read as one until 1.1
split them: `multiplier`, what a quoted premium multiplies by, which an adjustment
does not change, and `deliverable_shares`, what one contract conveys, which it does.

Nullability follows what a chain can express rather than what the schema document
leaves unmarked. Bid and ask are required; last, both counts and the five greeks are
absent rather than zero, matching `ContractQuote`. Only a bar's close is required as
of migration 5, matching `UnderlyingBar`, and a standing test compares the table's
pragma nullability against the record's optional properties, so a record change
names the migration owed.

Twelve triggers refuse `UPDATE` and `DELETE`, two per table, generated from a list
frozen at migration 3 rather than from `AppendOnlyTables`, because an applied
migration's SQL cannot change when that vocabulary grows. They hold against a writer
the source detector cannot see; the detector reads `src/`.

**Identity is unsettled and the schema records it.** §2 says a contract's identity is
the tuple of underlying, expiry, right and strike. It is not: an adjusted series can
share all four with a standard contract and differ only in the deliverable, which
sources confirm and the built schema demonstrates. §2 carries a banner. The
uniqueness constraint is on the tuple plus the deliverable, which is the strongest
the answer allows and a floor under the decision rather than an answer to it.

**One read surface, `AsOfMarketData`, and no current-value counterpart exists at
all** [1.2]. Market data has no operational current-read consumer anywhere in the
design, so the strongest form of "cannot read current" is that no current-reading
type exists to cast to. Every value-returning member takes the as-of date by name
and type, asserted by a shape suite, because a read here filters on two independent
axes, which session the row describes and when it was observed, and a member taking
only the session date would leak the latest observation. A correction recorded
after a simulated date is invisible to a read at that date and visible after it.

The chain read reaches identity through `contracts` on `contract_id`, the latest
observation per contract is a CTE with declared column names, and the uniqueness
constraint's own index serves the symbol lookup, measured with `EXPLAIN QUERY
PLAN`. Identity order is imposed in C# on the parsed identities, because the
stored decimal form does not sort. `corporate_actions` and `earnings_calendar`
have no reads yet; their first consumers are 1.5 and Phase 2.

**`ChainWriter` persists one chain, whole, at one instant** [1.4]. The chain
carries no instant and the writer takes one, stamped on every row. Same-instant
re-ingest is refused with the correction path named, the primary keys remaining
the enforcer; a new instant appends alongside, each observation visible to its
own as-of. Contracts are found or created on the unique tuple, an upsert being
impossible by construction under the append-only trigger, and a multi-match on
the four-tuple refuses rather than guesses, naming §2's unsettled identity
question. FX-WorkedExampleChainPersists holds the round trip against the
document's own tables, to the cent.

Every rendered value binds through `AddStored`, the write-side seam [D-W29]:
decimals through the refusing entry point, dates, instants and rights through
their stored forms, nulls as DBNull. Exclusivity is review's to hold, a
type-level check having been declined [D-W33].

## Membership

`watchlist_membership` records transitions, `joined` or `left` effective on a
date, keyed on symbol and version [D-W35]; an interval per version cannot answer
the membership question, which §4.2 demonstrates on re-entry. Three triggers
hold it: two append-only refusals and a monotonic `observed_at` per symbol,
which is `config_rows`' geometry, because version order crosses stamp order here
and does not in the snapshot tables.

`MembershipWriter` is `ConfigWriter`'s shape: `MAX(version) + 1` computed inside
the insert, `RETURNING`, both instants as parameters. The kind renders through
`StoreMembershipKind`, declared not derived, and the read's filter is rendered
through the same declaration rather than restating it as a literal.

`AsOfMembership.MembersOn(date, asOf)` resolves the sequence: among rows visible
at the as-of instant and effective at or before the date, the greatest
(`effective_on`, `version`) governs. Latest-version resolution fails when a
correction carries an earlier effective date than a later genuine transition,
measured by a test that fails under it. A correction supersedes a transition
only by tying its date; a wrong date is a compensating pair. Its own type
rather than a member of the market-data surface, because the one-surface
guarantee there rests on a premise never argued for membership, and its shape
suite and no-current tripwire are mirrored copies.

## Configuration

Two sections bound, `Eodhd` and `Storage`, both verified. Six sections deliberately
unbound because `CONFIG_REFERENCE.md` classes them `rows` and a registered options
type is itself a current-value accessor.

Nineteen of the 23 `rows`-classed keys hold a value at version 1, written by the
`seed` verb. Four carry an `Unset` marker and each names the phase that owes it. The
store is the authority on what is in force, not the document.

Both directions of the key contract are standing checks for `app` keys. For `rows`
keys only the types-to-document direction holds, because most are deliberately
unbound until their phase.

Configuration is readable two ways, as separate types so neither can be reached from
the other. `ConfigWriter` appends `MAX(version) + 1` computed inside the insert's own
transaction and reports the version through `RETURNING`, so the number returned is
the one that statement wrote. Both cross-key invariants run on every write, and a
write leaving one unevaluable is refused [D-W34].

## Stored forms

Dates are `yyyy-MM-dd`, timestamps `yyyy-MM-ddTHH:mm:ss.fffZ`, filenames
`yyyyMMddTHHmmssfffZ`. Decimals are fixed-scale at 8 places [D-W29], with a refusing
entry point and a rounding one, lenient on padding and strict on precision.

The form is not order-preserving, so no SQL orders, ranges over or aggregates a
decimal column. `DecimalColumns` holds seventeen names as of 1.1.

Decimals reach `TEXT` columns through `AddStored`, rendering through the refusing
entry point; rounding is a deliberate call visible at a site, never a default
inside the seam. `ConfigWriter` still takes strings, `config_rows.value` being
polymorphic by design.

## Guards and detectors

`guards.ps1` runs before restore, so it reports on a tree where nothing else can. Two
named checks, no exemption mechanism, self-testing on their own samples.

Three SQL detectors, all reading `src/` only: no decimal ordering, no rewrite of an
append-only table, and no alias of a table or a column. The third is the convention
that discharges the alias obligation, and it is what makes the other two sound
without either resolving aliases. Its source arm admits a parenthesised expression
as of 1.2, so an aggregate acquiring a name is reported; a CTE header stays clean
because the alias group requires an identifier and the token after `AS` there is
`(`, which no identifier can match.

**A declared vocabulary is checked standing in the direction in which absence causes
the bad outcome.** `DecimalColumns` and `AppendOnlyTables` run list to document, a
name with no table being the error. `PolicyBandCeilings` runs document to list, a
band with no entry being the error, because the ceiling is compared only against the
bands the list names.

## Tests

365: 250 across twenty-six fixtures, and 115 across twenty-two unregistered
suites. The two guards are checks rather than tests and are counted in neither.

| Fixture | Tests |
|---|---|
| FX-ClockIsNotADateSource | 34 |
| FX-SnapshotNeverRewritten | 23 |
| FX-NoRewriteOfAppendOnlyTables | 20 |
| FX-MalformedChainFailsWhole | 17 |
| FX-MoneyRoundTrip | 17 |
| FX-ConfigWriteRefusesInvariantBreach | 16 |
| FX-NoSqlAliases | 15 |
| FX-NoDecimalOrderingInSql | 14 |
| FX-ConfigStoreClassHonoured | 12 |
| FX-TickerDashForm | 12 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-ConfigResolvesAsOf | 6 |
| FX-EveryConfigSectionBinds | 6 |
| FX-MigrateFromEmpty | 6 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-EveryPolicyBandIsChecked | 5 |
| FX-RegistryMatchesDisk | 5 |
| FX-ChainLoadsInIdentityOrder | 4 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-WorkedExampleChainLoads | 4 |
| FX-ApiCannotWrite | 3 |
| FX-EveryAppKeyBinds | 3 |
| FX-NoCurrentConfigReadOnSimulatedPath | 3 |
| FX-PitMembershipExcludesLaterJoiner | 3 |
| FX-SnapshotRestoresIdentically | 3 |
| FX-WorkedExampleChainPersists | 3 |

The suite parses `CONFIG_REFERENCE.md`, `FIXTURES.md`, `DATA_AND_SCHEMA.md`,
`WORKED_EXAMPLE.md` and `guards.ps1`, so all five are load-bearing rather than
descriptive. **Nothing parses §4.1 as a schema**, which is why the schema document
and the migration diverged in six places at 1.1 and no check caught it.

Every store test creates its own database in a temp directory, because the
append-only triggers make the tables impossible to clean between cases.

## Not built

Every table beyond the nine that exist. `decisions` and `candidates` are
Phase 4's.

`corporate_actions` and `earnings_calendar` have no writer and no reads; their
first consumers are 1.5 and Phase 2.

No operator entry point ingests a chain. `ChainWriter`'s only callers are tests
until Phase 8's vendor ingest needs a verb, and a verb nothing calls is
speculation.

Nothing runs, so nothing produces output. Determinism is asserted over stored rows.

## Owed

Work deferred out of a checkpoint is registered in `BUILD_PLAN.md` carried
obligations, which is where planning for the phase that owns it will look. It is not
copied here: two registers of one list is how an obligation comes to exist in the one
nobody reads.

Entries stand against Phase 1, 2, 3, 4, 8 and 11. The count is not restated here.
1.1 discharged the SQL alias obligation, 1.2 closed the effective-dating question
by decision [D-W35], and 1.4 closed the write-side seam. One Phase 1 row remains,
the adjusted strike, which 1.5 owns. 1.3 raised the dividend obligation, owed at
Phase 3.

## Working rules in force

- Commit subjects are prefixed with the phase and stage, as
  `Phase 1 / 1.1 - <type>: <subject>`.
- The pull request description describes the change as it stands rather than
  accumulating a section per review round.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.
- A checkpoint's pull request is merged as a merge commit, never squashed.

---

# Prompts

## 1.1 The market-data schema

Read `CLAUDE.md`, `BUILD_PLAN.md` §1.1 and the carried obligations,
`DATA_AND_SCHEMA.md` §2, §3 and §4.1, D-W8, D-W9, D-W17, D-W29, D-W32, D-W35,
`AppendOnlyTables`, `DecimalColumns`, `Migrations`, the `FIXTURES.md` rows at 1, and
Current state above.

Six tables have been specified since v1.0.0 and declared forward since 0.7 and
nothing has created them. This makes them real, which is the first time either
declared vocabulary has a live subject.

### Measure the things the detail assumes, and expect the measurement to answer a
### different question than the one asked

- **`AppendOnlyTables` gains nothing.** 0.7 declared all six forward. What this owes
  is the other direction, the definition of done that every table it adds is in the
  vocabulary, which was unmeetable when none of them existed.
- **Run the extended decimal vocabulary over the tree and report what it flags**,
  rather than predicting it. Then decide whether the names stay unqualified, and
  record the decision with the measurement behind it.
- **A vocabulary that grows can change what a detector SEES and not only what it
  matches.** An order-keyword filter that runs before the vocabulary is consulted
  will drop a column whose name is also a keyword.
- Whether a bare `right` needs quoting is a question about the parser, and whether
  nulls are distinct in a unique index is a question about the engine. Probe both.

### Identity, which is not what the corpus says it is

- **Verify the adjusted-strike collision against a source before designing for it**,
  and against the vendor's own memos rather than a secondary account.
- If it is real, the identity tuple is not identity and §2 is wrong. Record it where
  planning will read it; do not fix it. It reaches D-W29, `ContractIdentity` and 1.5,
  so it wants a decision rather than a migration.
- Add the strongest constraint the answer allows and say in the schema why it is that
  one. A constraint that forbids a collision which occurs is worse than none.
- **A migration is not cheap to undo**, so a constraint that pre-empts the pending
  decision costs more than one that sits under it.

### The metric question is not this checkpoint's

- D-W17 uses one word for two quantities. Splitting them is here; deciding which one
  committed capital uses is not, and the arithmetic that frames it belongs in the
  obligation rather than in a decision.
- **`WORKED_EXAMPLE.md` cannot adjudicate it.** Both quantities are one hundred for a
  standard contract, so its figure is silent on which one it means.

### The schema

- The six tables of §4.1 exactly, with the stamps in three keys.
- **Nullability follows what a chain can express**, not what the document leaves
  unmarked, or a chain the loader accepts is one the schema refuses.
- A stored form the database does not enforce is a rule with one guard. The same
  argument reaches the enumerated column and the append-only rule.
- **Twelve triggers, generated from a list frozen at this migration.** The vocabulary
  is the right source in every respect but one: an applied migration's SQL cannot
  change when that list grows.
- Indexes for the reads §3 already defines. Report what cannot be indexed because a
  later checkpoint has not defined the read, rather than guessing at it.
- **Test**: a correction appends and both rows survive; an as-of read between the
  stamps sees the first; an `UPDATE` and a `DELETE` against each table are refused by
  the store.
- **A trigger is per row.** A refusal asserted against an empty table fires nothing
  and passes.
- **DoD**: migrating from empty, and from the previous schema, applying only what is
  new and snapshotting first.

### The alias obligation

- Resolve aliases in both detectors, or adopt and check a convention. Deleting both
  known-miss tests is part of closing it either way.
- **Run the convention against the real statements, not synthetic ones.** A
  convention whose first run flags a legitimate statement gets narrowed rather than
  believed, and the statements with an alias's shape are already in the tree.
- Record what the convention costs against a named checkpoint rather than a
  hypothetical, and check whether the cost is real before recording it as real.

### Definitions of done carried from 0.2

- Every check registered against 1.1 exists in its kind.
- Every table this checkpoint adds is in `AppendOnlyTables`, and every decimal column
  it adds is in `DecimalColumns`.
- Every key its sections introduce is bound and verified. It introduces none, so this
  is discharged with that reason rather than empty.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Reconcile the
detail and the archive at sign-off, not during the build.

## 1.2 As-of reads

Read `CLAUDE.md`, `BUILD_PLAN.md` §1.2 and the carried obligations,
`DATA_AND_SCHEMA.md` §3, §4.1 and §4.2, D-W8, D-W26, D-W35, `AsOfConfiguration`,
`AsOfBoundary`, `ConfigRowQuery`, the alias fixture, the `FIXTURES.md` rows at 1,
and Current state above.

1.1 put `observed_at` into three keys so a correction could append, and nothing
reads it: the only as-of read over a market-data table is inside a fixture's own
SQL. Give the read a home in `src/`, which is also where the join 1.1 deferred
gets its answer.

### One surface, argued rather than copied

- Configuration has two types because two consumers exist: operational paths read
  current [D-W26] and simulated paths read as-of. Market data has no operational
  current-read consumer anywhere in the design, so do not build the counterpart.
  The strongest form of "cannot read current" is that no current-reading type
  exists to cast to.
- **"Every member takes a date" is too weak here.** A market-data read filters on
  two independent axes, which session the row describes and when it was observed,
  so a member taking the session date alone would satisfy "takes a date" while
  leaking the latest observation. Assert the as-of parameter by name and type on
  every value-returning member, and assert the absence of a current type by a
  tripwire that says what it can and cannot catch.

### Fix the detector before writing the read that would trip on it

- **The column-alias rule's source arm is an identifier class**, and the character
  before `AS` in `MAX(observed_at) AS latest` is `)`, which that class cannot
  match. The aggregate form is exactly what a naive chain read writes, so widen
  the arm first and write the read against the honest detector.
- What keeps a CTE clean is the alias group, not an exemption: the token after
  `AS` in a CTE header is `(`, which no identifier can match. Test both CTE forms
  against the widened rule, sweep the tree, and report what it flags rather than
  predicting zero.
- **A comment beside a detector describes what the detector does, not what it was
  meant to do.** The loop comment named two forms as ignored that the rule would
  report. Correct it to say both would be reported, neither appears, and the rule
  stands on absence rather than on narrowness, which is the recoverable direction.

### The reads

- One folder `Core/MarketData`, one type. `BarFor(symbol, sessionDate, asOf)` and
  `QuotesFor(symbol, snapshotDate, asOf)`, returning the `Synthetic` records so
  1.4's round trip uses one vocabulary and the same oracle 0.6's fixture parses.
- The latest observation per contract is a CTE with declared column names, the
  convention's own shape. The chain reaches identity through `contracts` on
  `contract_id` filtered by symbol; measure with `EXPLAIN QUERY PLAN` whether the
  uniqueness constraint's index serves the lookup before adding any index.
- **Identity order is imposed in C# on the parsed identities, never in SQL**, and
  the test pins a 9-versus-10 strike pair whose stored forms sort backwards as
  text.
- Read the optional bar fields null-tolerantly, so the nullability correction 1.4
  owes does not touch the read.
- `corporate_actions` and `earnings_calendar` reads are deliberately absent: their
  first consumers are 1.5 and Phase 2, and a member nothing calls is speculation.
  Say so in the detail rather than leaving it to be read as an omission.
- `AsOfBoundary` is expected to fit unchanged, one widening of `asOf` to its last
  instant, the session axis being date against date. If it does not fit, that is a
  finding.
- **Test**: a correction recorded after a simulated date is invisible at that date
  and visible after it, for a bar and for a quote; three as-of dates spanning two
  corrections return three answers in order; before the first observation there is
  nothing rather than the earliest row; an observation on the as-of date itself is
  visible.

### The membership schema, carried

- Apply §4.2's replacement in its own commit: each row records one transition,
  `joined` or `left` effective on a date, keyed `(symbol, version)` [D-W35], with
  the re-entry case showing why an interval per version cannot answer the
  membership question. `watchlist_membership` joins `AppendOnlyTables` as a
  forward declaration. Close the effective-dating obligation by decision rather
  than by lapsing, and record the counts before and after.

### 1.4, read rather than guessed

- `ResolveAtOrBefore`'s remark predicts 1.4 may need a transaction. Read 1.4's
  detail and answer it: its read-back is verification after commit, not a read
  inside the write, and membership resolution would never pass through
  `ConfigRowQuery` because it is not a config read. Correct the remark at the
  site.

### Definitions of done carried from 0.2

- No check is registered against 1.2; the detail says so per rule 2, and the
  behaviour and shape suites land unregistered.
- 1.2 adds no tables, no decimal columns and no keys, checked and empty.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. A finding
goes where planning for the work will read it, never only in the pull request,
and it is read back off disk after the edit. Reconcile the detail and the archive
at sign-off, not during the build.

## 1.3 Watchlist membership as state

Read `CLAUDE.md`, `BUILD_PLAN.md` §1.3 and the carried obligations,
`DATA_AND_SCHEMA.md` §4.2 and the Time section, D-W9, D-W26, D-W30, D-W35,
`ConfigWriter`, `AsOfMarketData` and its surface tests, `Migrations`, the
`FIXTURES.md` rows at 1.3, and Current state above.

### Docs before any code

- **The dividend gap becomes an obligation, carried by this checkpoint.**
  Reconfirm the premise by grep before writing the row: dividends appear as an
  ingest source, a `corporate_actions` kind and an early-exercise risk, and
  nowhere as a ledger entry, while D-W13 names capital and window only. Owed at
  Phase 3. Report the obligation count before and after, measured.
- **Correct 1.4's migration ordinal while its detail is live intent.** This
  checkpoint takes migration 4, so "Migration 4 first" becomes "A migration
  first, before any ingest code": the property is the ordering, not the number,
  which changes whenever a checkpoint between them adds one.
- **Author §4.2's two corrections now rather than carrying them as findings**,
  so no landed state has the DDL contradicting the document: name the governing
  axis as the greatest (`effective_on`, `version`) with the correction-semantics
  sentences, and mark `reason` nullable, which the document's own convention
  otherwise denies.
- 1.3's detail names what it carries, per the standing rule.

### Migration 4, and it carries three triggers

- Frozen literal DDL per §4.2 exactly, with the `CHECK` on `kind` for the
  reason `right` has one, and two append-only triggers carrying this table's
  own correction story.
- **The monotonic `observed_at` trigger goes in this migration, answered rather
  than deferred**, because an applied migration's SQL is frozen and deferring
  costs a migration. Version ordering is no substitute: it constrains versions,
  not visibility, and a backdated stamp changes what was believed at a past
  instant after the fact. Per symbol, equal allowed. The snapshot tables
  deliberately carry no analogue, because they have no version axis crossing
  the stamp; membership has `config_rows`' geometry exactly.
- **Test**: migrating a previous-schema store applies only the new migration,
  snapshot first, with the previous-schema store built from the frozen
  migration list itself. Check whether a from-previous-schema test exists
  before assuming it does; until this checkpoint every store in the tree was
  either empty or current, so none did.
- **Test**: the refusals, seeded first, per the trigger-is-per-row lesson; the
  `CHECK`; the backdated stamp refused by the store; the equal stamp allowed; a
  second symbol unbound by another symbol's stamp.
- Drop the vocabulary count sentence in `AppendOnlyTables` rather than editing
  it a second time: the by-name listing carries the information and the
  created-tables sweep is what knows which exist.
- **Expect the alias detector to read prose in SQL comments.** It flagged
  "version as config_rows" in this migration's own comment; reword the comment
  rather than narrowing the detector, because prose inside a SQL literal is
  inside the scanner's jurisdiction and narrowing is the unrecoverable
  direction.

### The writer

- `Core/Membership/MembershipWriter`, `ConfigWriter`'s shape: `MAX(version) + 1`
  computed inside the insert, `RETURNING`, both instants as parameters [D-W30],
  one transaction.
- The kind takes the domain type, rendered through `StoreMembershipKind` in
  `Core/Storage`, mirroring `StoreOptionRight` including the
  declared-not-derived rule. No raw string at any call site.
- The backdated-stamp refusal in C# names both instants, which `RAISE` cannot;
  the trigger is what holds against any writer.
- **Test**: versions n and n+1 for one symbol; a second symbol versioned
  independently; the kind lands in the declared form; the reason optional and
  stored when given.

### The read

- `AsOfMembership` in `Core/Membership`, its own type rather than a member of
  `AsOfMarketData`, for three reasons stated in the code: that type documents
  itself as the only read surface over the snapshot tables and membership is
  not a snapshot; the one-surface guarantee rests on no operational
  current-read consumer existing, never argued for membership and probably
  false for it at Phase 8; and a mirrored shape suite is a check, not a fact,
  so two copies do not drift.
- One member, `MembersOn(date, asOf)`, tickers in symbol order. A per-symbol
  read has no consumer until Phase 2 decides how the gate asks.
- **The governing axis is the greatest (`effective_on`, `version`)**, resolved
  by a window function inside a CTE with declared column names, ranking over
  every visible transition, which makes the no-latest-row-alone DoD
  structural. The `joined` filter is a parameter rendered through
  `StoreMembershipKind`, never a literal restating the declared form.
- **Test the axis by divergence**: joined 3/1, left 8/1, a correction fixing
  the join date to 2/15 as the highest version; latest-version answers member
  on 9/1 and latest-effective answers left. Flip the window ordering to
  version alone and confirm exactly this test fails, so the choice is
  exercised rather than asserted.
- **Test**: the three-interval re-entry case; the correction invisible as of
  an instant before its stamp and visible after, tying the date so version
  breaks the tie; before anything was observed the set is empty; symbol order
  pinned.
- The declared stored forms are pinned against migration 4's frozen `CHECK`
  vocabulary, which is where the coupling lives.
- Mirror the surface suite: every value-returning member takes `DateOnly asOf`
  by name, guard-the-guard, and a no-current tripwire whose message says a
  current surface arrives as a decision that amends it.

### The registered fixture

- FX-PitMembershipExcludesLaterJoiner holds on both axes: a name that joined
  after the queried date, and a join backfilled with an earlier effective date
  but recorded after the as-of instant, are both excluded; boundaries pinned
  inclusive.
- The registry marker is counted from disk, with 1.2's zero registrations
  noted where the range would otherwise read as a gap.

### Definitions of done carried from 0.2

- Every check registered against 1.3 exists in its kind.
- The forward-declared vocabulary entry is live and the created-tables sweep
  covers the new table with no edit.
- `DecimalColumns` gains nothing and 1.3's sections introduce no keys, both
  checked and empty.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Measure
with `EXPLAIN QUERY PLAN` before adding any index. A finding goes where
planning for the work will read it, then is read back off disk. Reconcile the
detail and the archive at sign-off, not during the build.

## 1.4 Chain ingest

Read `CLAUDE.md`, `BUILD_PLAN.md` §1.4 and the carried obligations rows raised
by PR #3, `DATA_AND_SCHEMA.md` §4.1 and the Time section, D-W8, D-W29, D-W30,
D-W31, `SyntheticChainReader` and its records, `AsOfMarketData`,
FX-WorkedExampleChainLoads, `Migrations`, and Current state above.

### Docs before any code

- Register FX-WorkedExampleChainPersists at 1.4: the worked example's chain
  persists and reads back identical to the document's tables. The marker moves
  when the fixture exists.
- **Reconfirm `contract_quotes` against `ContractQuote` by direct quote of the
  DDL before scoping the migration**, and say so in the report: the finding
  that raised the migration was itself a lesson in verifying one record and
  claiming both.
- Settle the detail's two open questions as live intent: same-instant
  re-ingest refused by the keys with a refusal that says so, a new instant
  appending alongside [D-W8]; and no Worker verb, tests being the only caller
  until Phase 8's vendor ingest.

### Migration 5, the bars rebuild

- **Enumerate the relaxed columns from `UnderlyingBar`, not from any
  sentence.** The record makes five optional where the finding's sentence
  named four; `volume` is the fifth. Then make the enumeration standing: a
  record-to-schema test comparing pragma nullability against the record's
  optional properties through a guarded map, so a record change names the
  migration owed.
- SQLite cannot alter nullability in place: create the replacement, copy rows
  across, drop, rename, and **recreate both triggers, which DROP TABLE takes
  with it**. Demonstrate the recreation on a seeded row; carry a
  hand-populated previous-schema store through the copy.
- State in the migration comment why rebuilding an append-only table is not a
  rewrite, and that DROP TABLE sits outside the banned statements
  deliberately: the rule governs observations, not schema.
- §4.1's markers change in the same commit, so the document and the DDL agree
  at every landed state.
- **Check the detectors against the rebuild's grammar before writing the
  SQL.** The clause anchor never reaches ALTER TABLE or DROP TABLE, so
  nothing widens, but that is measured, not assumed.

### The seam

- `AddStored` on the parameter collection, one overload per stored-form type,
  decimals through the refusing entry point [D-W31], nulls as DBNull. Each
  overload's rendering asserted equal to its Store* form's.
- The writer binds every rendered value through it; counts bind directly,
  having no stored form.
- Close the write-side obligation and state the teeth honestly: exclusivity
  is review's to hold [D-W33], and `ConfigWriter` is out of scope,
  `config_rows.value` being polymorphic by design.

### The writer

- `ChainWriter` beside `AsOfMarketData`, on the membership precedent: reader
  and writer of one subject in one folder.
- `Ingest(chain, observedAt)`: the chain carries no instant and the writer
  takes one [D-W30], stamped on every row. One transaction, all or nothing,
  and **observe the rollback rather than assuming it**: a collision after the
  header insert must leave no header row.
- One header row per distinct snapshot date; the format admits several per
  file. Contracts before quotes for the foreign key, with 1.1's note at the
  site. Find-or-create is ON CONFLICT DO NOTHING with a follow-up lookup —
  an upsert is impossible by construction, the append-only trigger refusing
  the update half — and a multi-match on the four-tuple refuses rather than
  guesses, naming §2's unsettled identity question.
- **Test both second-run behaviours**: same instant refused with the
  correction path named and counts unchanged; a new instant alongside, each
  observation visible to its own as-of, contracts found rather than
  recreated.

### The oracle

- **The parser is already shared**; check what is actually duplicated before
  extracting. The header vocabularies, structural constants and chain-file
  load are the duplicated half: move them to a shared oracle helper in a pure
  refactor, behaviour unchanged, before the new fixture consumes them.
- FX-WorkedExampleChainPersists: load, persist, read back at the recorded
  instant's date, compare against §2 and §5 to the cent, pairwise in identity
  order, delta included, non-empty asserted first. Absence survives the
  store: what the document does not state reads back null rather than zero.
- The marker counted from disk.

### Definitions of done carried from 0.2

- Every check registered against 1.4 exists in its kind.
- No new table, no new decimal column, no config key: each checked and
  reported empty. Migration 5 changes nullability, not names, so both
  vocabularies stand unchanged, said rather than skipped.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit
files with the file tools rather than a shell round trip, which mangles
UTF-8 outside ASCII. Reconcile the detail and the archive at sign-off, not
during the build.
