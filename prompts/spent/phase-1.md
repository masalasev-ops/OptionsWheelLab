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

Corpus v1.19.0.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| Phase 1 | 1.1 built and signed off; 1.2 to 1.5 not built |
| CI | green, 304 tests, guards then restore then build then test, on push to `main` and every pull request |

Which branch the work sits on and which pull requests have merged are not recorded
here. Git holds both exactly, and a fact kept in two places drifts.

## Build

.NET 10. Four projects: `Core`, `Worker`, `Api`, `Tests`. Warnings are errors,
nullable is on, `InvariantGlobalization` is on, code style is enforced in the build.
Central package management with transitive pinning.

`Core` has five folders: `Configuration`, `Storage`, `Identity`, `Time` and
`Synthetic`.

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

**Schema 3.** Migration 1 is `config_rows`, 2 its monotonic `set_at` trigger, 3 the
six market-data tables of §4.1 [1.1].

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
absent rather than zero, matching `ContractQuote`.

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

Nothing writes a decimal through a typed path yet. `ConfigWriter` takes strings and
1.4 is where the first real decimal column is written, which is what the D-W29
obligation names as its trigger.

## Guards and detectors

`guards.ps1` runs before restore, so it reports on a tree where nothing else can. Two
named checks, no exemption mechanism, self-testing on their own samples.

Three SQL detectors, all reading `src/` only: no decimal ordering, no rewrite of an
append-only table, and no alias of a table or a column. The third is the convention
that discharges the alias obligation, and it is what makes the other two sound
without either resolving aliases.

**A declared vocabulary is checked standing in the direction in which absence causes
the bad outcome.** `DecimalColumns` and `AppendOnlyTables` run list to document, a
name with no table being the error. `PolicyBandCeilings` runs document to list, a
band with no entry being the error, because the ceiling is compared only against the
bands the list names.

## Tests

304: 240 across twenty-four fixtures, and 64 across thirteen unregistered suites.
The two guards are checks rather than tests and are counted in neither.

| Fixture | Tests |
|---|---|
| FX-ClockIsNotADateSource | 34 |
| FX-SnapshotNeverRewritten | 23 |
| FX-NoRewriteOfAppendOnlyTables | 20 |
| FX-MalformedChainFailsWhole | 17 |
| FX-MoneyRoundTrip | 17 |
| FX-ConfigWriteRefusesInvariantBreach | 16 |
| FX-NoDecimalOrderingInSql | 14 |
| FX-ConfigStoreClassHonoured | 12 |
| FX-TickerDashForm | 12 |
| FX-NoSqlAliases | 11 |
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
| FX-SnapshotRestoresIdentically | 3 |

The suite parses `CONFIG_REFERENCE.md`, `FIXTURES.md`, `DATA_AND_SCHEMA.md`,
`WORKED_EXAMPLE.md` and `guards.ps1`, so all five are load-bearing rather than
descriptive. **Nothing parses §4.1 as a schema**, which is why the schema document
and the migration diverged in six places at 1.1 and no check caught it.

Every store test creates its own database in a temp directory, because the
append-only triggers make the tables impossible to clean between cases.

## Not built

Every table beyond the eight that exist. `decisions` and `candidates` are Phase 4.

Nothing reads market data as-of yet: 1.2 builds the read paths, and the only as-of
read over a market-data table is inside FX-SnapshotNeverRewritten.

A loaded chain still reaches nothing. 0.6 built the loader and 1.4 persists what it
yields.

Nothing runs, so nothing produces output. Determinism is asserted over stored rows.

## Owed

Work deferred out of a checkpoint is registered in `BUILD_PLAN.md` carried
obligations, which is where planning for the phase that owns it will look. It is not
copied here: two registers of one list is how an obligation comes to exist in the one
nobody reads.

Entries stand against Phase 1, 2, 3, 4, 8 and 11. The count is not restated here.
1.1 discharged the SQL alias obligation, the first Phase 1 row to close.

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
