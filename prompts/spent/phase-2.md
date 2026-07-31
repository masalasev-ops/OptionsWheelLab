# Phase 2 Candidate generator and risk gate: spent prompts

Current state below is the whole state of the repository and the only description
of the present. Phase 0's and Phase 1's files hold the state as it stood at each
phase's close and are not corrected further.

One prompt per checkpoint, being the prompt that produces the checkpoint as it now
stands. Corrections found while building are folded back into the checkpoint's
prompt rather than appended as further entries, so replaying the prompts in order
against the corpus reproduces the current state without replaying the mistakes.

One file per phase. It closes when Phase 2 signs off; Phase 3 opens its own.

---

# Current state

Corpus v1.30.0.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| Phase 1 | complete, 1.1 to 1.5 built and signed off |
| Phase 2 | open, 2.1 built and signed off, 2.2 to 2.5 not started |
| CI | green, 378 tests, guards then restore then build then test, on push to `main` and every pull request |

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

**Identity is five components, settled at 1.5** [§2, D-W36]. A contract's
identity is underlying, expiry, right and strike together with the deliverable,
which is what separates an adjusted series from the standard contract listing
beside it at the same strike. `ContractIdentity` carries all five in equality,
hashing and the total order; the uniqueness constraint has since 1.1. Adjusted
terms are transcribed from what the adjusting authority states, never derived
from a ratio [D-W36], and the refusing decimal path is the tripwire: a
derivation producing a non-terminating value cannot be stored at all.

**A corporate action mints a stated successor, atomic with its event row**
[1.5]. `CorporateActionWriter` records the `corporate_actions` row and inserts
the successor with its predecessor link in one transaction; an adjustment whose
stated terms change nothing is refused, and the predecessor reads back
byte-identical. `ContractLineage` walks the link as the recursive CTE the alias
convention was proven to permit, timeless because contracts carry no
observation axis. The corporate-action kind vocabulary is `split` only, with
the fuller set and the CHECK question recorded at Phase 3's dividend
obligation.

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

## The worked example

`WORKED_EXAMPLE.md` is the oracle for two fixtures and, since 2.1, teaches the
gate as well as the wheel. Its chain is seven strikes on one expiry: three
feasible, one failing the spread cap alone at 37.84 percent of mid, one failing
the premium floor alone at half of it, and two failing the delta ceiling and the
per-name cap together, which is the case for recording every reason rather than
the first [D-W22]. The expiry window and earnings clearance are named in §3 as
constraints one snapshot cannot demonstrate, and belong to 2.3's fixtures.

The oracle pins the symbol, the snapshot date, the expiry and the right, and
parses everything else out of the document's tables, so the document and
`synthetic/worked-example.json` move together and no test code changes when they
do. §2's and §5's table headers are the parse keys, matched ordinally against the
first table carrying them, which makes a reworded header a silent miss caught
only by the fixtures' own emptiness assertions.

Every figure from §4 onward derives from a bid or a close. That is what made the
reconciliation small, and it is the property to check before revising the chain
again.

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

The gate's six bounds are among the nineteen, seeded at 0.8: a spread cap of 0.12
of mid and a premium floor of 0.30 [D-W22], a delta ceiling of 0.35 [D-W23], an
expiry window of 7 to 70 [D-W24], and an earnings buffer of 7 [D-W25]. The three
`Risk:` fractions are the `Unset` ones Phase 2 owes, and 2.4 sets them.

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

378: 253 across twenty-seven fixtures, and 125 across twenty-four unregistered
suites. The two guards are checks rather than tests and are counted in neither.
2.1 registered none and changed none, which is what a document-only checkpoint
pinned by existing fixtures looks like.

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
| FX-CorporateActionMintsSuccessor | 3 |
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

No candidate generator, no gate. The constraints of [D-W22] to [D-W25] exist as
decisions and as seeded configuration, and nothing reads them: 2.2 enumerates,
2.3 builds the contract constraints, 2.4 the portfolio ones, 2.5 the feasible
set. The worked example describes their verdicts; no code computes one.

`corporate_actions` has its first writer, the mint, and no as-of read;
`earnings_calendar` has neither. Their read consumers arrive at Phase 2.

No operator entry point ingests a chain. `ChainWriter`'s only callers are tests
until Phase 8's vendor ingest needs a verb, and a verb nothing calls is
speculation.

Nothing runs, so nothing produces output. Determinism is asserted over stored rows.

## Owed

Work deferred out of a checkpoint is registered in `BUILD_PLAN.md` carried
obligations, which is where planning for the phase that owns it will look. It is not
copied here: two registers of one list is how an obligation comes to exist in the one
nobody reads.

Entries stand against Phase 2, 3, 4, 8 and 11. The count is not restated here.
2.1 discharged the reconciliation row raised at v1.6.0, the table's oldest and
open for twenty-three corpus versions. Phase 2's two remaining rows are
preconditions rather than work items: the crossed-quote question is 2.3's,
because the spread cap is what a crossed quote defeats, and the three `Risk:`
fractions are 2.4's, because the operator sets a cap and a checkpoint that
tests one needs it to exist first.

## Working rules in force

- Commit subjects are prefixed with the phase and stage, as
  `Phase 2 / 2.1 - <type>: <subject>`.
- The pull request description describes the change as it stands rather than
  accumulating a section per review round.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.
- A checkpoint's pull request is merged as a merge commit, never squashed.

---

# Prompts

## 2.1 The worked example, reconciled

Read `CLAUDE.md`, `BUILD_PLAN.md` §2.1 and the carried obligations,
`WORKED_EXAMPLE.md` in full, D-W4, D-W5, D-W10, D-W12, D-W22 to D-W25,
`CONFIG_REFERENCE.md`'s seeded Gate and Risk rows, FX-WorkedExampleChainLoads,
FX-WorkedExampleChainPersists, `WorkedExampleOracle`,
`synthetic/worked-example.json`, and Current state above. No production code.

### The invariant, and why the rewrite is small

- **Establish this before changing anything, by reading rather than by trusting
  it**: every figure in §4 through §7 derives from a bid, because fills are at
  the bid [D-W12], and the only figure derived from an ask is the spread ratio
  the gate reads. Read each section in full; a search for a column name answers
  a different question.
- So the rewrite changes asks and adds rejected candidates, and changes nothing
  else. Not the symbol, the snapshot date or the expiry; not any bid on any
  strike; not the strikes 45.00, 47.50 and 50.00, which are the feasible set §4,
  §6 and §7 depend on, including FX-ThreeMakersSameFeasibleSet's expected set;
  not any delta on those three; nothing in §4, §5, §6 or §7.
- Report what the check found, not that it was run. An §4-onward figure reading
  an ask is a finding and changes the shape of this checkpoint.

### What the revised chain must demonstrate

The chain teaches the gate, which means each constraint this snapshot can
demonstrate has a candidate demonstrating it, with obvious margins rather than
hundredths.

- The spread cap [D-W22]: the three feasible strikes pass comfortably, and one
  added candidate fails it and nothing else.
- The premium floor [D-W22]: one added candidate fails it and nothing else.
- The delta ceiling [D-W23]: 52.50 and 55.00 already fail it at 0.44 and 0.62
  against 0.35, and already fail the per-name cap. Keep both, because a
  candidate failing two constraints is what FX-GateRecordsAllReasons exists for
  and the document should show one.
- Added candidates extend the strike ladder downward at its existing 2.50
  spacing rather than inserting off-ladder strikes.
- **Expect the floor to constrain the added candidates**, since 45.00's bid is
  the floor exactly and its bid cannot move. A spread-cap-only failure below it
  is possible on one bid alone, and the geometry that results is the stale
  untransactable quote D-W22 describes, so state it in §3 as deliberate rather
  than letting it read as an accident.
- Constraints this snapshot cannot demonstrate, stated in the document rather
  than left as a gap: the expiry window [D-W24] and earnings clearance [D-W25],
  because one snapshot date with one expiry cannot show a window and no report
  date exists in this example. Say so in §3 and say which checkpoint's fixtures
  cover them instead.
- Compute every ratio and report the arithmetic. Carry no figure from this
  prompt: none is supplied, deliberately, because the numbers are the thing to
  get right and this project's arithmetic errors have all come from
  transcription rather than from calculation.

### The three artefacts move together

§2's table, §3's gate table and `synthetic/worked-example.json` state one chain.
All three change in one commit or the oracle disagrees with itself.

- §3's gate table gains a column per constraint, or a reason column naming every
  constraint each candidate failed, so the table shows the gate's whole verdict
  rather than the capital cap alone. It shows one constraint today and the
  banner names three more.
- The JSON gains the new quotes and the revised asks.
- Both pinning fixtures pass unchanged in code: the oracle helper parses §2 and
  the fixtures compare, so a revision needing a fixture edit has changed
  something the tests were holding.

### The delta the gate compares, and the deferral it settles

- §3's table carries magnitudes where §2 carries the sign, and the document says
  why: the ceiling compares absolute delta [D-W23]. One sentence, and it removes
  the only place in the revision where one quantity appears two ways without
  explanation.
- `ContractQuote.cs` says whether the ceiling compares magnitude is Phase 2's to
  settle [D-W23], and D-W23 said absolute delta from the day it was written. The
  question was answered by the decision the comment cites, in the sentence it
  cites it for. Correct the comment: the ceiling compares absolute delta, and
  the loader still carries the sign the chain states, which is the part that was
  genuinely a loader question. Enumerate the reach from the work rather than
  from this clause; the same deferral is elsewhere.
- It is a comment in `src/`, so the commit touching it is code-adjacent rather
  than docs-only. Say so rather than letting a docs-only claim carry a `.cs`
  edit unremarked.

### The banner and the obligation

- §3's banner comes down, replaced with nothing: a resolved conflict needs no
  monument, and CHANGELOG carries the history.
- Remove the Phase 2 reconciliation row from carried obligations, raised at
  v1.6.0 and open since. Report the count before and after.
- CHANGELOG, under Fixed: the example was written before the contract-level gate
  existed and taught a three-candidate feasible set the gate would render as
  two. Reconciled by revising the quoted markets so the feasible set survives,
  and by adding candidates that fail the constraints the example never
  demonstrated. No bid changed, so nothing downstream of the fill moved.

### Definitions of done

- 2.1 registers no checks and says so in its detail, per rule 2's clause. It
  changes a document two existing fixtures already pin, which is the coverage.
- No table, no decimal column, no config key: each checked and reported empty.
- The seeded Gate values are read from `CONFIG_REFERENCE.md` rather than
  assumed: if any margin depends on a value that is proposed rather than seeded,
  say which.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit files
with the file tools rather than a shell round trip, which mangles UTF-8 outside
ASCII. Reconcile the detail and the archive at sign-off, not during the build.
