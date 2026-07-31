# Phase 0 Foundations: spent prompts

Current state below is frozen at this phase's close and is not corrected
further. The description of the present lives in the open phase's file.

One prompt per checkpoint, being the prompt that produces the checkpoint as it
now stands. Corrections found while building are folded back into the
checkpoint's prompt rather than appended as further entries, so replaying the
prompts in order against the corpus reproduces the current state without
replaying the mistakes.

One file per phase. **This file is closed.** Phase 0 signed off at corpus v1.15.0,
and `phase-1.md` holds the description of the present from 1.1's sign-off.

---

# Current state, as it stood when Phase 0 closed

Corpus v1.16.0. **Frozen.** This described the present until `phase-1.md` opened at
1.1's sign-off, and records Phase 0's close from then on. Checkpoint 1.1 changed
several of the facts below, including the schema version, how many tables exist and
the test count. The current ones are in `phase-1.md`.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| CI | green, 268 tests, guards then restore then build then test, on push to `main` and every pull request |

Which branch the work sits on and which pull requests have merged are not
recorded here. Git holds both exactly, and a fact kept in two places drifts:
these two rows were the only thing in this section that could not be known at the
moment a checkpoint is determined fully built, because a merge commit does not
exist until after it.

## Build

.NET 10 solution, nullable enabled, warnings as errors, central package
management with transitive pinning: `OptionsWheelLab.Core` holding the
composition root, options types, the storage layer and the identity primitives,
`.Worker` and `.Api` as thin hosts both calling it, and `.Tests`, which
references both hosts and carries tests that use them, so a broken host fails
`dotnet test` and not only the separate build step.

One shared `src/appsettings.json` linked into both hosts and the test project,
loaded from `AppContext.BaseDirectory` because the generic host and the web host
default their content roots differently, then environment variables after it so
a per-machine value can override a committed one. No `Logging` section is
committed, so every top-level section must bind and the binding test needs no
framework allowlist. `appsettings.Secrets.json` is gitignored with a committed
empty `.example` and loads optionally, so a fresh clone builds without it.

`Microsoft.AspNetCore.OpenApi` is absent: version 10.0.9 pulls
`Microsoft.OpenApi` 2.0.0, carrying GHSA-v5pm-xwqc-g5wc. `Microsoft.Data.Sqlite`
10.0.9 is present with its four `SQLitePCLRaw` packages pinned to 2.1.12,
lifting them off 2.1.11 which carries GHSA-2m69-gcr7-jv3q. Neither advisory is
suppressed.

## Store

SQLite at the directory named by `Storage:Path`, supplied per machine through
the environment variable `Storage__Path` and committed empty. The path is
validated as rooted at the point of use rather than at binding, so a process
that merely binds configuration is unaffected while one that opens the store
fails fast. Nothing derives the location from `AppContext.BaseDirectory` or a
working directory, so the Worker and the Api cannot resolve it to two places.

WAL journal mode, set on the write connection and persisted with the database.
The Worker opens read-write as the sole writer; the Api opens
`SqliteOpenMode.ReadOnly`, set on the connection rather than by convention.

Snapshot-first migrations. A snapshot is one file written with `VACUUM INTO`
[D-W28]: atomic, no lock, no writer blocked, and a database in its own right
that can be opened directly rather than restored before it can be read. It
carries whatever has not yet checkpointed, and is named
`snapshot-<filename-form timestamp>.db` beside the store so a restore knows what
to look for. The first run has no store and
records that it skipped and why. Applied migrations are rows in
`schema_migrations`, not `PRAGMA user_version`. Two migrations exist:
`config_rows` with its append-only triggers, and a trigger holding `set_at`
monotonic per key.

`migrate.ps1` refuses when `Storage__Path` is unset and invokes the verb. It
supplies nothing else: the Worker carries the `migrate` verb, because the Worker
is the sole writer, and the verb reads the instant from the clock.

## Configuration

Two sections bound: `Eodhd` to `EodhdOptions` and `Storage` to
`StorageOptions`, both verified by reading the composition root.

Six sections deliberately unbound because `CONFIG_REFERENCE.md` classes them
`rows` and a registered options type is itself a current-value accessor: `Risk`,
`Gate`, `Costs`, `Policy`, `Trial`, `Scoring`.

`CONFIG_REFERENCE.md` carries 27 key rows, one key per row. Four Consumer cells
are verified; 23 carry **Unverified**, and they stay that way because every
consumer of a `rows` key is Phase 2 or later.

Nineteen of the 23 `rows`-classed keys hold a value at version 1, written by the
`seed` verb. Four carry an `Unset` marker and each names the phase that owes it:
the three `Risk:` fractions at Phase 2, `Costs:AssignmentFee` at Phase 3. Both
`app`-classed unset keys are supplied per machine.

The document is not the authority on what is in force; the store is. A value in
Notes is version 1 and the reason for it, and a revision inserts version + 1
without editing the document. Provenance is stated per key and is three kinds:
transcribed from a corpus statement, taken from a value a decision proposed, or
judged. Four values are judged, and `Trial:MaxTrialDays` at 120 is the least free
of them, having to clear `Gate:MaxDte` [D-W24] and to leave the worked example's
own 109-day trial representable. `Gate:MaxDelta` and `Policy:Random:DeltaMax` are
both 0.35 by choice, which settles D-W23; the 0.10 floor beneath them is inherited
and recorded as inherited.

Both directions of the key contract are standing checks for `app` keys:
FX-EveryBoundKeyIsDocumented walks the types and checks the document,
FX-EveryAppKeyBinds walks the document and checks the types. For `rows` keys only
the first holds, because most are deliberately unbound until their phase, and the
paragraph declining the reverse now says so.

Configuration is readable two ways, as separate types so neither can be reached
from the other. `AsOfConfiguration` takes a date on every member and resolves
`MAX(version)` among rows at or before that date's last instant.
`CurrentConfiguration` returns the newest and is for operational paths only.
`ConfigWriter` appends `MAX(version) + 1` computed inside the insert's own
transaction, with `set_at` supplied rather than read from a clock and refused if
it predates the newest version of that key. `AppendAll` writes N entries in one
transaction and `Append` delegates to it, so there is one definition of the insert
and one of the monotonic check. `AppendMissing` writes the first version of each
key that has none, skips the rest and names both sets. Each insert reports its own
version through `RETURNING`, inside the transaction that wrote it, rather than by
a following `MAX(version)` read that a second writer would make wrong.

Both read surfaces carry decimal and integer accessors alongside the string one,
as public instance methods rather than extensions, since the as-of guard reflects
over declared instance members and anything else would be invisible to it. They
exist so the canonical form is validated at the point of reading rather than
assumed, and so changing the scale is one edit; not to close an ambient-culture
trap, which `InvariantGlobalization` already closes. An integer is stored plainly
and not in the decimal form.

The two cross-key invariants are pure predicates over supplied values, with no
host, no config store, no startup wiring and no clock, and `ConfigWriter` calls
them on every write. They are in the writer rather than in the seeder because the
seeder is one caller and `Append` would have stayed unguarded, and because D-W23,
D-W24 and D-W27 all put enforcement at the moment a version is written, versions
being insertable while the process runs. A refused write leaves the table exactly
as it was, which the fixture asserts by row comparison rather than by the absence
of an exception.

A write touching a key one invariant needs, and leaving the store without the
rest of that invariant's keys, is refused [D-W34]. So `Gate:MaxDte` and
`Trial:MaxTrialDays` cannot be written apart, and neither can the delta ceiling
and the bands. A write touching no invariant key succeeds into an empty store. A
key already stored counts as an operand, so a half-seeded store can be completed.
`ConfigKeys` declares each invariant's key set, the same declared-vocabulary shape
as `DecimalColumns` and `AppendOnlyTables`, and carries each band's name beside
its key so a refusal can say which band it failed against.

**A declared vocabulary is checked standing in the direction in which absence
causes the bad outcome.** For `DecimalColumns` and `AppendOnlyTables` that is list
to document, a name with no table behind it being the error, and the reverse is a
definition of done on the checkpoint that adds each table. For
`PolicyBandCeilings` it is document to list, a band with no entry being the error,
because the ceiling is compared only against the bands the list names and an
omission would make a violating configuration pass rather than fail. The three
look inconsistent and are not.

`ConfigRowQuery.ResolveCurrent` takes an optional transaction, because the
invariants read rows that have not committed and Microsoft.Data.Sqlite refuses a
command with no transaction while one is pending.

## Stored forms

Every value with a stored representation has one place that renders and parses
it, because in each case the obvious call is culture-independent, plausible and
wrong.

Dates are `yyyy-MM-dd` and timestamps `yyyy-MM-ddTHH:mm:ss.fffZ`, both UTC and
fixed width. A date is widened to its last instant in one place before meeting a
timestamp column. Filenames use `yyyyMMddTHHmmssfffZ`, because a colon is
illegal in a Windows path. A bare `ToString()` on a date gives `MM/dd/yyyy` under
`InvariantGlobalization`, which cannot vary by machine and still sorts by month.

Decimals are fixed-scale at 8 places, so one number has one stored string
[D-W29]. Two entry points: one refuses a value it cannot hold exactly, for vendor
quotes and strikes and ledger amounts, and one rounds away from zero, for
computed values, since decimal division is non-terminating in general. Both
refuse on magnitude. The bound is computed from the scale rather than written
down. Parsing is lenient about padding, so a hand-written `0.35` reads, and
strict about precision, counted on the string, because `decimal.Parse` silently
rounds beyond 29 significant digits.

The form is not order-preserving, so no SQL orders, ranges over or aggregates a
decimal column. `config_rows.value` is the first entry in that vocabulary.

A contract right stores as `put` or `call`, declared rather than derived from the
enum's spelling.

## Time

`IClock` returns the instant the process is running at and offers nothing else
[D-W30]. A simulated date never comes from it: the lab's two kinds of time are
unrelated, and a component wanting the simulated one and reaching for the clock
gets an answer that is plausible, non-null and wrong.

It is read at composition and entry points only. The sole writer's host registers
it, the `migrate` verb resolves it, and nothing below them holds one. The config
writer and the migration runner still take instants as parameters, so a test
supplies a fixed value directly rather than through a fake. The read-only host
registers no clock, having nothing to stamp.

Nothing outside the process can name the instant a row is stamped with. The
option that supplied one existed for want of a clock and is gone, because an
override is a way to write a `set_at` that never happened into a store whose rows
can never be corrected.

`IClock` rather than `TimeProvider`, and the deciding reason is the guard: the
forbidden and sanctioned token sets are disjoint, where `TimeProvider`'s ambient
instance and an injected one are one type separated by which member is touched.
The return is `DateTimeOffset`, so it is UTC by construction rather than by a
`Kind` nothing checks.

Determinism is asserted as identical stored rows across two runs on one fixed
clock, compared as table contents. A SQLite file is not a deterministic rendering
of its contents, so bytes were never the comparison. The output-level property is
owed at Phase 3, the first checkpoint with a run to make.

## Synthetic chains

A synthetic chain is data, not a registry entry [D-W31]. It is authored by a
person, so the format optimises for being written and read by hand and pays for
that in loading cost. They live in `synthetic/` at the repository root, beside
`src/` and `docs/` rather than inside the test project, because phases 1 to 7
consume them and the Worker cannot reach test-project content.

The shape is a chain rather than a table: symbol, snapshot date, expiry and right
are stated once and the strike rows carry only what varies. That is decided on
identity rather than on readability, since three of those four make up contract
identity and a schema-mirroring row repeating them would turn a typo into a
different contract rather than into an error.

Every value is a quoted string, including the numbers, and an unquoted one is
refused. The source guard names a JSON number bound into an untyped tree as
something it cannot catch, so the format closes it by construction. Comments and
trailing commas are admitted, because these files carry commentary and get their
lists reordered.

Absent is absent rather than zero: the worked example gives bid, ask and delta
and the schema has seven more fields, and a zero gamma would be a false
observation. The underlying's close on a snapshot date is stated once, by the bar.

The loader takes text and resolves no path, so it needs no configured root and
introduces no key. It yields objects rather than rows, nothing reaching the store
until Phase 1 wires it, in contract identity order rather than file order.

Malformed fails whole and reports every reason in one pass, each carrying the
path to the offending value. Refused: any primitive refusal, a decimal beyond the
scale, an unrecognised property, an unquoted number, a duplicate bar or contract,
an expiry before its snapshot date, a negative bid or ask, and a bid above its
ask. The last is the one domain rule, and what it costs is owed at Phase 2: no
chain can now express a crossed market for the gate to be tested against.

`synthetic/worked-example.json` carries `WORKED_EXAMPLE.md` §2 and §5. The test
parses those two tables and compares rather than restating their numbers, which
makes that document the third to be machine-checked after `CONFIG_REFERENCE.md`
and `FIXTURES.md`.

## Identity

A ticker is the bare dash form, `BRK-B`, constructible only through
normalisation, so one carrying an exchange suffix cannot exist. The suffix is
stripped before dots become dashes, or `BRK-B.US` would become `BRK-B-US`. A
one-letter dot-suffix is a share class and a two-to-five-letter one is an
exchange code, stripped if known and refused if not: `GSPC.INDX` is refused
rather than silently becoming `GSPC-INDX`. Characters are checked before the case
fold, so a homoglyph cannot become a second key.

A contract's identity is underlying, expiry, right and strike, with the strike
canonicalised. That buys rendering stability and validation, not equality:
`decimal` equality and hashing already ignore scale. The vendor symbol lives on
`Contract` rather than on the identity, because record equality covers every
declared member. Identity carries a total order, since three makers receiving
byte-identical candidate sets requires one.

## Guards

`guards.ps1` at the root holds the checks that are not unit tests, and `ci.yml`
calls it before restore, so it reports on a tree where nothing else can run. Two
named checks, each a `guard`-kind row in `FIXTURES.md`, scanning every `.cs` under
`src` and `tests` with no exemption mechanism of any kind.

FX-NoFloatingPoint bans floating point. Its catch-list covers the two keywords
plus `Random.NextDouble`, `Convert.ToDouble`, `GetDouble`, the `Math` functions
and exponent literals, none of which carry a `double` token.

FX-NoAmbientClock bans the ambient time reads and `TimeProvider` as a type, since
a second time abstraction is the drift it exists to prevent. It is anchored on the
type name so the injected call cannot match, and does not catch converting a
supplied instant to a date, which three tests do. One file is permitted, named
once in the script: that states the rule rather than escaping it, since the rule
is "outside the clock implementation". The carve-out has to earn its place, so
scanning a permitted file with nothing to permit fails.

Elapsed-time counters are deliberately absent from both lists. They have no epoch
and cannot yield a date, so they cannot commit the error; a duration reaching a
stored row is the row comparison's business.

Each check self-tests on its own must-fire and must-not-fire samples before
scanning. A shared third sample exists because a literal stripper that desyncs
still scans every file and still reports success.

They catch declared intent, not inferred types, and say so. No `.ps1` is
scanned, including these.

The property is that they report when restore does not succeed, which is narrower
than the "fails even when the build does not" they used to claim. An analyser
reports alongside a compile error, measured; what it cannot survive is a failed
restore, where none runs at all. The guards stay a text scan and a fixture
[D-W33], one check of four being all an analyser would gain.

## Append-only

Ten tables are never rewritten, and no statement in `src/` may delete from or
update one. Six are the snapshot tables of §4.1 [D-W8], defined there because the
phrase was used in four documents and defined in none; `contracts` is one despite
carrying no `observed_at`, since a corporate action mints a new identity rather
than editing a row. Then `decisions` and `candidates` [D-W3], `config_rows`
[D-W26] and `schema_migrations` [D-W32]. Two exist and eight do not.

Every entry rests on a decision that states the property rather than on the list's
own existence. Three citations had to be corrected to make that true, which is
what `CLAUDE.md` §1's rule about verifying a citation by what rests on it came
from.

Three mechanisms exclude the statements already in the tree, and one would not
suffice. Statement form excludes the trigger DDL, since `DELETE FROM` needs the
`FROM` and `UPDATE` needs the `SET`. The vocabulary excludes a statement against
a table the rule does not cover. Scan scope excludes the tests that prove the
triggers work, which are real offences and are excluded by being tests rather
than by being anything else. None is an exemption list.

Quoted identifiers are covered, since four spellings are one statement. A table
alias is a known miss, pinned, and owed at Phase 1 with the decimal detector's
alias miss, which is the same problem one level down. So is a rewrite written in
`tests/` by mistake, which is what the scope mechanism costs.

Effective-dating is unsettled and three tables carry it: `watchlist_membership`,
`positions` and `trials` each have a nullable close column that makes a state
change an update while §4.2 says rows are never deleted. All three stay out of
the vocabulary and are owed at Phase 1 as one obligation.

## Tests

268: 205 across twenty-two fixtures, and 63 across thirteen unregistered suites,
one of which is the 0.1 smoke test. The two guards are checks rather than tests
and are counted in neither.

| Fixture | Tests |
|---|---|
| FX-ClockIsNotADateSource | 34 |
| FX-NoRewriteOfAppendOnlyTables | 21 |
| FX-MalformedChainFailsWhole | 17 |
| FX-MoneyRoundTrip | 17 |
| FX-ConfigWriteRefusesInvariantBreach | 16 |
| FX-TickerDashForm | 12 |
| FX-ConfigStoreClassHonoured | 12 |
| FX-NoDecimalOrderingInSql | 12 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-ConfigResolvesAsOf | 6 |
| FX-EveryConfigSectionBinds | 6 |
| FX-MigrateFromEmpty | 6 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-RegistryMatchesDisk | 5 |
| FX-EveryPolicyBandIsChecked | 5 |
| FX-ChainLoadsInIdentityOrder | 4 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-WorkedExampleChainLoads | 4 |
| FX-ApiCannotWrite | 3 |
| FX-EveryAppKeyBinds | 3 |
| FX-NoCurrentConfigReadOnSimulatedPath | 3 |
| FX-SnapshotRestoresIdentically | 3 |

FX-ClockIsNotADateSource is large because most of it pins measured SQLite
behaviour rather than its own detector, so an upgrade that changes the behaviour
fails on the behaviour. FX-MalformedChainFailsWhole is large because a refusal
and the case beside it are separate assertions.
FX-NoRewriteOfAppendOnlyTables is large because each of the three exclusion
mechanisms is asserted separately, and one of them takes both halves.
FX-ConfigWriteRefusesInvariantBreach is large because it covers two invariants,
both directions of D-W34, and the seed's own values through the same path.

All twenty-two entries registered against 0.2 to 0.8 are implemented and named for
their registry entry. The suite parses `CONFIG_REFERENCE.md`, `FIXTURES.md`,
`DATA_AND_SCHEMA.md`, `WORKED_EXAMPLE.md` and `guards.ps1`, so all five are
load-bearing rather than descriptive.

Every store test creates its own database in a temp directory, because the
append-only triggers make `config_rows` impossible to clean between cases. No
test touches the configured store directory, so the suite runs on CI where that
path does not exist.

## Layout

Repository root holds `README.md`, `CLAUDE.md`, `migrate.ps1`, `seed.ps1` and
`guards.ps1`. The corpus rule governs documents only: every document is in
`docs/`, spent prompts are in `prompts/spent/`, and hand-written synthetic chains
are in `synthetic/`. None of the last two are documents.

The Worker has two verbs, `migrate` and `seed`, each with an operator script that
checks `Storage__Path` and reports a failure without a stack trace. Setting a
store up is `.\migrate.ps1` then `.\seed.ps1`, and the second is a no-op on a
store that already has its values.

`Core` has five folders: `Configuration`, `Storage`, `Identity`, `Time` and
`Synthetic`.

## Working rules in force

- Commit subjects are prefixed with the phase name and stage, as
  `Phase 0 Foundations / 0.8 - <type>: <subject>`.
- The pull request description is updated on every check-in, and describes the
  change as it stands rather than accumulating a section per review round. An
  appended section cannot retract an earlier one, so a superseded decision ends
  up asserted alongside the one that replaced it. The commit log is the log.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.
- A checkpoint's pull request is merged as a merge commit, never squashed, so
  the phase-prefixed commits stay legible on `main`.

## Not built

Market data tables and every other table. Phase 0 is complete, so what is not
built is Phase 1 onward, whose checkpoint detail is not written.

Nothing writes a decimal through a typed path yet: `ConfigWriter.Append` and
`AppendAll` take strings, so D-W29's write-side rule is a convention with no
enforcement behind it. The nineteen seeded values are hand-written short forms
such as `0.35`, which `StoreDecimal.ParseStored` accepts by design, that leniency
existing for exactly this case. What is missing is not the padding but the seam:
a decimal reaching a `TEXT` column still does so as a string somebody typed.

Nothing runs, so nothing produces output. Determinism is asserted over stored
rows, and the output-level property waits for the first checkpoint with a run.

A loaded chain reaches nothing. There is no market-data table to put it in, so
Phase 1 wires the quotes and bars the loader yields and stamps `observed_at` as
it does so. One synthetic chain exists, being the worked example; the calls and
later expiries §6.3 names are expressible and belong to Phase 3's fixture.

## Owed

Work deferred out of a checkpoint is registered in `BUILD_PLAN.md` carried
obligations, which is where planning for the phase that owns it will look, and
which outlives this file. It is not copied here: two registers of one list is
how an obligation comes to exist in the one nobody reads.

Entries stand against Phase 1, Phase 2, Phase 3 and Phase 11. The count is not
restated here, because a count is the part of a pointer that rots while the
pointer stays true. Nothing is owed at a Phase 0 checkpoint, there being none
left: 0.7's row closed with an answer rather than by lapsing, and 0.8 added two
for the four keys it left unset.

One entry was raised in the corpus rather than out of a build, so the table admits
both origins and its Raised column carries a corpus version where there is no pull
request to name.

Nothing is scoped-but-not-deferred any more. That entry named 0.8's invariant
wiring, and 0.8 has shipped.

---

# Prompts

## 0.1 Repository skeleton

Read `CLAUDE.md`, `README.md`, `SYSTEM_DESIGN.md` §7, `BUILD_PLAN.md` §0.1.
`CLAUDE.md` is binding for how you work, not only for what you build.

- .NET 10 solution: `OptionsWheelLab.Core`, `OptionsWheelLab.Worker`,
  `OptionsWheelLab.Api`, `OptionsWheelLab.Tests`. Nullable enabled, warnings as
  errors, central package management. `Core` holds the composition root; the two
  hosts are thin and both call it, so there is one binding site.
- The repository root holds `README.md` and `CLAUDE.md` only. Every other
  document lives in `docs/`.
- One shared `src/appsettings.json`, linked as content into both hosts and the
  test project rather than one file per host, because a Worker and an Api
  disagreeing about the lab's configuration is a defect. Load it from
  `AppContext.BaseDirectory` rather than the host default: the generic host and
  the web host default their content roots differently and relying on that
  difference is a trap.
- Commit no `Logging` section, so every top-level section in `appsettings.json`
  must bind to one of the lab's own options types and the binding test needs no
  framework allowlist. An allowlist is where a stray section would hide.
- `appsettings.Secrets.json` in `.gitignore`, with
  `appsettings.Secrets.example.json` committed alongside carrying empty values
  and no real credentials. Load the secrets file optionally so a fresh clone
  builds without it. Inspect the `.gitignore` diff before committing it, and
  confirm the `.example` is not matched by the exclusion.
- Pin line endings to LF so a Windows checkout cannot produce a whitespace-only
  diff, which by rule would have to be its own commit.
- Do not reference `Microsoft.AspNetCore.OpenApi`. Version 10.0.9 pulls
  `Microsoft.OpenApi` 2.0.0, which carries a high severity advisory that the
  build's vulnerability audit fails on. The API surface is Phase 11 and has no
  endpoint to describe, so suppressing the audit to keep an unused package would
  cost detection everywhere [`CLAUDE.md` 4a]. Record the reason at the reference
  site and add the re-adding to carried obligations in `BUILD_PLAN.md`.
- CI workflow running restore, build and test on push to `main` and on every
  pull request, on actions targeting a supported Node runtime.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

- **Test**: solution builds; a trivial test passes in `OptionsWheelLab.Tests`.
- **DoD**: CI green on a fresh clone with no local state.

## 0.2 Configuration binding

Read `CONFIG_REFERENCE.md` and `FIXTURES.md` before starting. Confirm the
working copy is current before reporting any corpus entry as absent
[`CLAUDE.md` 10].

- Bind only the sections `CONFIG_REFERENCE.md` classes as `app` [D-W27]. A
  section classed `rows` is never given an appsettings-bound options type, not
  even as a placeholder, because a registered options class is itself a
  current-value accessor and 0.3 would then hold two paths to the same values
  [D-W26].
- Leave values unset wherever that document marks them unset. Do not invent
  values.
- Route every binding through one helper that records the section path and
  options type in the same call, so the record is a by-product of binding rather
  than a parallel list. A test reading a hand-maintained list passes while the
  list is stale, which is the failure this checkpoint exists to prevent.
- Implement the fixtures registered against 0.2 in `FIXTURES.md`, reading their
  assertions from that file. Name each test file exactly for its fixture.
- The binding test must ENUMERATE the options types registered in composition
  and fail on any section that binds to nothing. A test that only asserts known
  sections populate does not satisfy this. Check both directions, and also that
  every options type configured at composition was registered through the
  helper, or the bypass leaves the enumeration comparing an incomplete set.
- The binding check covers every committed configuration file, being
  `appsettings.json` and `appsettings.Secrets.example.json`. The uncommitted
  `appsettings.Secrets.json` is out of scope, being absent on a fresh clone.
- Deliver the two cross-key invariants [D-W23, D-W24] as pure predicates in
  `Core` over supplied values, with no host, no config store and no startup
  wiring. Enforcement is at config-write time and lands at 0.8. Cover the
  holding case, the violating case, and equality at the boundary.
- Any test that parses a corpus document or enumerates a set asserts its input
  is non-empty before comparing anything. A parse that silently matches nothing
  passes without testing anything, which has already happened once in this
  corpus.

- **DoD**: the binding test fails when a stray section is added to a committed
  configuration file and passes when it is removed. Demonstrate both, and commit
  neither stray section.
- **DoD**: `CONFIG_REFERENCE.md`'s Consumer column names the component and the
  verified type, as `component via TypeName`, for every key bound in this
  checkpoint. Verify by reading the composition code, not by a grep for the key
  name.
- **DoD**: every fixture registered against 0.2 in `FIXTURES.md` exists and is
  named for it.

Constraints
- No `DateTime.Now` or `DateTime.UtcNow`. The clock abstraction lands in 0.5.
- No `double` or `float` in any type that will carry money.

Out of scope
- The store, migrations, and any table. That is 0.3.
- The `config_rows`-backed configuration path and the as-of resolver. That is
  0.3. Do not introduce a current-value config accessor that 0.3 would have to
  remove.
- Setting values for any key `CONFIG_REFERENCE.md` marks unset. Those are 0.8.

## 0.3 Store bootstrap, migrations, and the config read service

Read `CLAUDE.md`, `BUILD_PLAN.md` 0.3 and its prompts rule,
`DATA_AND_SCHEMA.md` section 3, Time and section 4, D-W1, D-W8, D-W26, D-W27,
the `FIXTURES.md` rows at 0.3, and Current state above. Confirm the working copy
is current before reporting any corpus entry as absent.

### Store bootstrap

- SQLite. The Worker is the sole writer; the Api opens read-only [D-W1]. Set
  read-only on the connection with `SqliteOpenMode.ReadOnly`, not by convention.
- The store directory is `Storage:Path`, bound to `StorageOptions` through the
  existing `BindSection` helper. It is `app`-classed by necessity rather than by
  the read-path criterion: a value needed to open the store cannot be stored in
  the store [D-W27].
- Name the section `Storage`, not `Store`. A key rooted at `Store` collides with
  the **Store** column that every table in `CONFIG_REFERENCE.md` carries, in a
  document the suite parses.
- Commit `Storage:Path` empty and supply it per machine through the environment
  variable `Storage__Path`. A committed absolute path starts on one machine only
  and publishes a filesystem layout to a public repository.
- Add `AddEnvironmentVariables()` **after** the JSON files. Both host builders
  add environment variables during construction, so otherwise the committed
  empty value wins and the override appears not to work. Note in a comment that
  this also puts environment ahead of the command-line provider, which is the
  reverse of the conventional order.
- Validate the path at the point of use, not at binding. 0.2 established no
  `[Required]` and no `ValidateOnStart` on unset keys, and that rule governs the
  binding layer. Refuse an empty or relative path with a message naming
  `Storage__Path` and saying a relative path is refused because the two hosts
  have different working directories.
- WAL journal mode, set on the write connection and persisted with the database.
  Without it the snapshot definition of done demonstrates copying a write-ahead
  log that does not exist.

- **Test** FX-ApiCannotWrite: a write through the read-only connection throws.
- **Test**: the Worker's write path and the Api's read path resolve to the same
  absolute path from different working directories.
- **Test**: an empty value and a relative value each fail with that message; an
  absolute value binds.
- Reference both hosts from the test project, and give the references real
  tests. Otherwise nothing in the suite touches a host builder, the
  provider-ordering fix is only ever demonstrated, and `dotnet test` passes
  while a host does not compile.
- **Test**: a host builder configured through the extension resolves
  `Storage:Path` from the environment and not from the committed empty value,
  for the generic host and the web host alike.
- **Test**: with the variable absent, binding succeeds and only opening the
  store fails.
- **Test**: an opened store reports WAL.
- **Report, do not decide**: a read-only connection needs the file to exist, so
  the Api cannot start against a store that has never migrated. Report the
  options and do not create the store from the Api. Include that a read-only
  connection to a WAL database still needs write access to the `-shm` file, so
  read-only is a guarantee about the data, not about the filesystem.

### Migrations, snapshot-first

- The runner snapshots before applying, so a hand-run migration cannot skip it,
  and `migrate.ps1` is the operator entry point.
- Take the snapshot with `VACUUM INTO` a timestamped file [D-W28]. It runs in a
  read transaction: atomic, blocks no writer, needs no lock, and produces one
  file rather than a set whose members can disagree. Do not copy the database
  and its write-ahead log; that form needs an exclusive lock held across the
  copy, and that lock byte-range locks `-shm` and makes it unreadable, so the
  lock and the three-file copy are not jointly satisfiable.
- The result is a defragmented rebuild rather than a byte-identical copy, so
  assert what a restored store resolves rather than comparing bytes.
- The first run has no store yet. Skip the snapshot and record that it was
  skipped and why: a base case rather than an exception.
- A snapshot is one file named `snapshot-<filename-form timestamp>.db` beside
  the store, so a restore knows what to look for and what to ignore.
- Record applied migrations in a table, not `PRAGMA user_version`. Schema
  version is the highest applied id.
- Migration 1: `config_rows` per `DATA_AND_SCHEMA.md` section 4.5, with triggers
  raising on UPDATE and DELETE so append-only holds against any writer.
- Migration 2: a BEFORE INSERT trigger refusing a row whose `set_at` predates the
  newest for that key. `version` always increases but `set_at` is supplied and
  otherwise unconstrained, and resolution filters on `set_at` then orders by
  `version`, so an out-of-order timestamp makes the value in force on a date
  depend on insertion order rather than on time. Equal is allowed: two versions
  can share an instant and `version` breaks the tie. Per key.
- A new migration rather than amending migration 1, because the
  definition-of-done demonstration already ran migration 1 against the real
  store, and an amended migration never re-runs.
- One instant, two renderings. `applied_at` and `set_at` take the stored form;
  the snapshot directory takes the colon-free filename form, because a colon is
  illegal in a Windows path. Render at the point of use, never convert between.
- Timestamps are parameters. There is no clock until 0.5, and a
  `DateTime.UtcNow` here is a call 0.5 has to remove.

- **Test** FX-MigrateFromEmpty, covering both meanings of empty because they
  behave differently: a first run against **no file** applies every migration
  and takes no snapshot; a second run against **a file with nothing pending**
  applies nothing and does take one.
- **Test** FX-SnapshotRestoresIdentically: a store snapshotted, mutated and
  restored from the snapshot resolves what it did before the mutation, and keeps
  its append-only triggers.
- **Test**: a snapshot succeeds while a reader holds the store, and while a
  writer holds an uncommitted transaction, capturing the committed state and not
  the uncommitted.
- **Test**: appending with a `set_at` earlier than the newest for that key is
  refused naming both instants; an equal `set_at` is accepted and resolves by
  version; a different key is unaffected.
- **Test**: the WAL copy, with automatic checkpointing turned off and the
  connection held open. SQLite checkpoints and deletes the write-ahead log when
  the last connection closes cleanly, so without both there is no `-wal` at
  snapshot time and a one-file copy would pass while losing data.
- **Test**: a snapshot filename round-trips to the same instant as the stored
  form.
- **Test**: the widened as-of boundary is the greatest value the stored format
  can render for that date, derived from the format's fractional digits rather
  than restated, so adding precision to the format fails here instead of quietly
  excluding the end of every day.
- **DoD**: `migrate.ps1` leaves a timestamped snapshot beside the store.

### Config read service, two surfaces

- Two separate public types, not one type with two interfaces. A shared
  implementation could be cast back to the current-value surface and the
  guarantee would be a convention again.
- The as-of surface takes a date on every member. No dateless member, no
  overload without a date.
- The current-value surface is separate and for operational paths only.
- `set_at` is a timestamp and the as-of parameter is a date, and the two never
  meet directly. A timestamp for any instant on a day sorts after that day's
  bare date, so widen the date to its last instant first, in exactly one place.
  Resolution is inclusive of the as-of date [D-W26].
- Both surfaces delegate to one internal query, so there is a single definition
  of resolving a key.

- **Test** FX-ConfigResolvesAsOf: three versions resolve to the one in force,
  not the newest; a row written at any time on the as-of date is in force; a row
  written at any time on the following day is not. Name each test for which way
  it resolves.
- **Test** FX-NoCurrentConfigReadOnSimulatedPath: reflect over the as-of type
  and assert no member returns a value without a date parameter. Assert the
  reflection found members, or it passes by finding nothing. This replaces the
  caller-scanning form, which would assert over an empty set today.
- **DoD**: adding a dateless read method to the as-of type fails the suite.

### Config writes

- Append-only. Version is `MAX(version) + 1` for the key, computed inside the
  same statement and transaction as the insert, so two writers cannot produce
  one version. The primary key on (key, version) makes a collision fail loudly.
- `set_at` is a parameter, never a clock read.
- Invariant enforcement on writes is owed by 0.8, not here [D-W23, D-W24].
  Nothing is seeded until then, so there is no unguarded window.

- **Test**: an update or a delete against `config_rows` fails.
- **Test**: two inserts for one key produce versions n and n+1.
- Every store test gets a fresh database. The triggers make `config_rows`
  impossible to clean between cases, so state that in the test helper rather
  than leaving it to be rediscovered.

### Definitions of done carried from 0.2

- Every fixture registered against 0.3 exists and is named for it.
- Every key the sections this checkpoint introduces carry is bound and verified
  in `CONFIG_REFERENCE.md`. This checkpoint introduces `Storage:Path`.

### Constraints

No `DateTime.Now` or `DateTime.UtcNow`. No `double` or `float`. Money as decimal
in TEXT. Nothing here reads or writes market data. Never suppress a
vulnerability advisory to keep a dependency: drop it or lift it to a patched
version, and record the reason [`CLAUDE.md` 4a].

## 0.4 Money and identity primitives

Read `CLAUDE.md`, `BUILD_PLAN.md` §0.4 and its phase definition of done,
`DATA_AND_SCHEMA.md` §2 and §4, D-W29, the `FIXTURES.md` rows at 0.4, and
Current state above.

Two things every later phase indexes on: what a number means when it is written
down, and what makes two option contracts the same contract. They are joined.
Strike participates in contract identity and strike is a decimal, so `50` and
`50.00` being the same number and different `TEXT` would give one contract two
identities and split its history without ever failing.

### The canonical decimal form

In `Core/Storage`, beside `StoreTimestamp` and the same shape: a declared format,
`ToStored` and `ParseStored`. Not a wrapper type; money is decimal.

- **One declared scale.** Choose it from the widest precision any column in
  `DATA_AND_SCHEMA.md` needs, report which column drove it, and say whether that
  figure is measured or assumed.
- **Two entry points, and this is the part a single function cannot do.** For a
  vendor-supplied value the scale is a fidelity requirement, so `ToStored`
  refuses rather than rounds: losing a digit quietly is the failure. For a
  computed value it is a rounding policy, so a second entry point rounds and is
  the only one that does. Decimal division is non-terminating in general, and
  `29.35m / 4500.00m` carries 28 fractional digits, so a single refusing function
  could not store the worked example's own first candidate. Name the midpoint
  rule; the default disagrees with away-from-zero on exactly the values a
  ranking sits on.
- **Both refuse on magnitude, differing only on precision.** Rounding bounds
  precision and not magnitude, so otherwise a large computed value rounds
  cleanly and then renders a string the parser cannot read back.
- **Derive the magnitude bound from the scale, never write it down.** The
  mantissa is fixed and the shift is the scale.
- **Pin the scale inside the range a decimal admits**, and let that assertion
  touch the scale constant and nothing else. Above 28 the bound's initialiser
  throws, which surfaces as a type-initialiser error naming whichever caller got
  there first rather than the constant that is wrong. A `const` is inlined and
  does not run the initialiser, so the assertion still reports cleanly while
  every other test in the file is failing for the wrong reason.
- **Parse lenient about padding, strict about precision.** A hand-written config
  row carries `0.35`. But `decimal.Parse` silently rounds beyond 29 significant
  digits, so count places on the string, not on the parsed value: a row that
  reads back as a different number than it states defeats the point of the store.
  Say in the remarks that "lenient on padding" means shorter than the stored
  form and not longer: `0.350000000` is refused though its zeros carry nothing,
  so the same value is admitted from a decimal and refused from a string. At most
  scale PLACES is a simpler contract than at most scale SIGNIFICANT places.
- **Test** FX-MoneyRoundTrip: values that lose precision as doubles, the worked
  example's figures, both scale boundaries, a midpoint, the magnitude bounds,
  negatives, and a non-terminating ratio through both entry points.
- **Negative zero is an equality between two stored strings**, not a case in a
  list. A listed case is satisfied by whatever the runtime happens to do.
- **DoD**: `50`, `50.0` and `50.00` produce one stored string.

**The fixture is the first test of the guard's own no-exemption policy.** The
build plan asks for values that lose precision as doubles and the guard bans the
type from the tree. Both hold at once: those values are exactly the ones binary
cannot represent, and the property under test is that they round-trip exactly, so
no floating-point value is constructed. If one genuinely turns out to be needed,
take that as a recorded decision rather than reaching for an exemption.

### The other two stored forms

The same shape, for the same reason: in each case the obvious call is
culture-independent, plausible and wrong.

- A date is `yyyy-MM-dd`. A bare `ToString()` gives `MM/dd/yyyy` under
  `InvariantGlobalization`, which cannot vary by machine and still sorts by
  month, so no culture test would catch it. **Test**: the two differ.
- A contract right is `put` or `call`, lower case. `Enum.ToString()` gives the
  wrong case. Declare the permitted values rather than deriving them from the
  enum's spelling, so renaming a member cannot change the stored form of every
  existing row. **Test**: an unrecognised value is refused rather than defaulted.

### Typed accessors on both configuration surfaces

Decimal and integer, built on the parser above, neither reimplementing it.

Record the real reasoning where they are defined. `InvariantGlobalization` is on
repository-wide, so the ambient-culture trap is already closed; the decimal
accessor exists because it is where the canonical form is validated rather than
assumed and where changing the scale stays one edit. The int case is weaker and
is about completeness of the surface: a surface that types one and returns
strings for the other teaches callers to parse at the call site. An integer is
not stored in the canonical decimal form, and the tests should say so.

Public instance methods, never extensions or statics. The as-of guard reflects
over declared instance members, so anything else is invisible to it and the guard
reads green while covering nothing. Confirm by running it.

### Identity

A new `Core/Identity` folder.

- **Ticker**, the bare dash form, constructible only through normalisation so one
  carrying an exchange suffix cannot exist. Strip the suffix **before** dots
  become dashes, or `BRK-B.US` becomes `BRK-B-US`. `BRK.B` is genuinely ambiguous
  between a share class and an exchange, so decide by suffix length: one letter
  is a class, two to five is an exchange code, stripped if known and **refused if
  not**. That refusal is the load-bearing choice: `GSPC.INDX` silently becoming
  `GSPC-INDX` mints a ticker that matches nothing and never fails. Check
  characters before the case fold, since `ToUpperInvariant` folds some non-ASCII
  letters into ASCII and a homoglyph would become a valid-looking second key.
  Report the edge cases you find.
- **Test** FX-TickerDashForm, with the injectivity and refusal sides too. The
  registered assertion alone is satisfied by a constant function.
- **ContractIdentity**, the tuple of underlying, expiry, right and strike. A
  reference type with a private constructor and get-only members: a struct admits
  `default` and `init` lets `with` reach the copy constructor, and both bypass the
  factory. The vendor symbol lives on a separate type, because record equality
  covers every declared member and the schema forbids it being part of the key.
- Canonicalise the strike, and be accurate about why. It buys nothing for
  equality, since `decimal` already ignores scale; it buys rendering stability,
  which byte-identical output needs, and validation at construction.
- **A total order**, since three makers receiving byte-identical candidate sets
  requires one. Name `StringComparer.Ordinal` with its reason.
- Out of scope: corporate-action adjustment and the predecessor link.

### The guards, as a script

The first check that is not a unit test, so write it as a place rather than as
one check: 0.5 and 0.7 both owe greps.

- One script at the root, called by `ci.yml` before the build, because a source
  guard must fail even when the build does not.
- Whole tree, no exemption mechanism of any kind. "Monetary path" is not a set
  anything can enumerate, and a guard that can be argued with is not a guard. The
  first legitimate floating-point value should cost a recorded decision about
  where statistics end and money begins.
- **Widen the catch-list past the two keywords.** `Random.NextDouble`,
  `Convert.ToDouble`, `GetDouble`, the `Math` functions and exponent literals
  carry no `double` token, and the random-within-band maker is the likeliest
  first violator. Record that this is a catch-list and not an exemption list:
  an incomplete catch-list still catches what is on it.
- **Say what it cannot catch**, so a green run is not read as proof. Tokens are
  declared intent, not inferred types.
- **Self-test before scanning.** A scan matching nothing reports success while
  testing nothing. If you strip literals, add a leg for the desync failure mode:
  a stripper that loses its place still scans every file and still passes.
- **DoD**: demonstrate a `double` in a decimal path failing, and a
  `Random.NextDouble` failing. Revert both.
- **Expect it to find something real on its first run, and fix rather than
  exempt.** `AsOfBoundaryTests` computes an exact power of ten with `Math.Pow`
  and casts the result back. That is arithmetic that should never have left the
  integers, not a case for the exemption mechanism this guard deliberately does
  not have. It will also fire on a token inside a string literal, which is a
  defect in the guard rather than a violation: strip literals, and add the
  desync leg to the self-test when you do.

### FX-NoDecimalOrderingInSql

A test, not a grep, and say why you chose that. The stored form is not
order-preserving, so no SQL may order, range over or aggregate a decimal column.

- A pure detector over `(sql, columns)`, exercised on synthetic SQL.
- Declare the column vocabulary beside the migrations. `config_rows.value` is its
  first entry: it carries decimals for four key families and is only sometimes a
  decimal, and classing it decimal is conservative on purpose. Write that down,
  or the first false positive reads as a defect and the column gets removed.
- Scan string literals holding a statement keyword, not whole source text. A C#
  parameter named `value` compared with `>=` reads exactly like a range
  comparison over the `value` column.
- Assert the two negative controls already in the tree: `ORDER BY version` and
  `MAX(set_at)`, whose string order is its time order by construction.
- **Record that an alias defeats it**, beside the over-reach note rather than
  apart from it. `SELECT strike AS s FROM contracts ORDER BY s` orders a decimal
  and the ordered token is not in the vocabulary. The over-reach note defends the
  false-positive direction; this is the false-negative one, and it is the
  direction that fails quietly. Pin it as a test so the gap is in the suite
  rather than only in prose, named so its eventual failure is the signal to
  delete it. Put the obligation in `BUILD_PLAN.md` carried obligations, not in a
  comment: a comment is committed and permanent and still not where the planning
  for Phase 1 will look.

### The phase definition of done, made checkable

Phase 0 requires every `app`-classed key in `CONFIG_REFERENCE.md` proven to bind,
and nothing enforces it, so it passes by coincidence. The reverse direction is
deliberately not standing for `rows`-classed keys, because most are unbound until
their own phase; that reasoning does not reach `app`, where a key is bound from
`appsettings` by definition.

- Make it a standing check, in a suite that is not a registered fixture.
- **Test**: an `app`-classed key with no bound property is reported by name.
  Demonstrate, revert.

### Definitions of done carried from 0.2

- Every fixture registered against 0.4 exists and is named for it.
- Every key the sections this checkpoint introduces carry is bound and verified.
  This checkpoint introduces none, so the obligation is discharged empty rather
  than skipped.

### Constraints

No `DateTime.Now` or `DateTime.UtcNow`; the clock is 0.5. Nothing here reads or
writes market data.

## 0.5 Deterministic clock

Read `CLAUDE.md`, `BUILD_PLAN.md` §0.5 and "How this document works", the carried
obligations, D-W26 and D-W27, the `FIXTURES.md` rows at 0.5, and Current state
above.

The lab has two kinds of time and they are unrelated: when this run is happening,
and which day is being simulated. A component that wants the second and reaches
for the clock gets the first, and the answer is plausible, non-null and wrong.
That is D-W26's leakage arriving through a different door, so the checkpoint is
about where the clock may be read rather than about the clock existing.

### D-W30, the decision this checkpoint lands

The injected clock returns the instant at which the process is running. A
simulated date is never obtained from it. It is read at composition and entry
points only; nothing below them reads a clock, which is the shape 0.3
deliberately gave `set_at` and the migration instant, and which keeps this
checkpoint a wiring change. Converting an instant to a trading date needs a
market calendar and a session timezone and is Phase 1.

Register both fixtures it names. `FIXTURES.md` is the single registry, so a
decision naming a check obliges a row.

### The abstraction

- **Choose between an `IClock` and .NET's `TimeProvider`, and report why.** The
  deciding argument is the guard: with a bespoke interface the forbidden and
  sanctioned token sets are disjoint, whereas `TimeProvider`'s ambient instance
  and an injected one are the same type and telling them apart is the type
  inference a text scan cannot do. `TimeProvider` also carries a machine-local
  timezone, which D-W30 scopes out, and its test double is a package in a
  repository that drops rather than suppresses.
- **The return is UTC by construction rather than by convention.** A `DateTime`
  with an unchecked `Kind` is the trap, and it is also not what the stored
  timestamp form takes.
- **Test**: the system clock's offset is zero.

### Wiring, at the edges only

- Register it in the sole writer's host and nowhere else. The read-only host has
  nothing to stamp, and an unused registration is noise.
- The migrate verb reads it. **Remove the option that supplied the instant, and
  the computation behind it in the operator script.** It existed for want of a
  clock, four comments say so, and an override left in place is a way to write a
  `set_at` that never happened into a store whose rows can never be corrected.
  Two tests go with it: an absent or unparseable instant stops being a failure
  mode.
- The config writer and the migration runner keep taking instants as parameters.
  Injecting the clock there would replace a fixed value in tests with a fake and
  buy nothing. Correct their comments, which give the reason as the clock not
  existing yet.
- **Test**: with one fixed clock and the same inputs, two runs produce identical
  stored rows, compared as table contents. Not as file bytes: a SQLite file is
  not a deterministic rendering of its contents. Seed a config row at the same
  instant so both tables carry one, assert the compared set is non-empty, and
  carry the negative direction so the comparison is known to be able to fail. Do
  not order by a column the decimal vocabulary claims.

### The guard, extending the one 0.4 built

- **Name each check**, because a registry row has to point at one.
- Catch-list at least: the ambient `DateTime` and `DateTimeOffset` reads, the
  day-granularity one, and whatever the abstraction choice makes ambient. Anchor
  on the type name, or the sanctioned call matches. Do not catch converting a
  supplied instant to a date; three existing tests do that.
- **The permitted file is named once, in the script.** This does not reopen
  0.4's no-exemption decision: the rule is "outside the clock implementation", so
  naming the implementation states the rule rather than escapes it. Make it earn
  its place, so a carve-out over a file with nothing to permit fails.
- **Decide whether elapsed-time counters are in scope and say why either way.**
- The script reads `*.cs` only, so no operator script is scanned. State it rather
  than leaving it incidental.
- **DoD**: an ambient call outside the permitted file fails locally and in CI.
  Demonstrate, revert. CI should need no change.

### The check a script cannot make

- The clock cannot hand out a date: one member, returning an instant.
- Nothing below an entry point holds a clock.
- The types serving a simulated date are enumerated rather than assumed, so the
  assertion is anchored to what would do the damage.
- **The store is not a date source either, and this is the leg to take
  seriously.** It is the one place a token scan structurally cannot reach,
  because the guard strips raw string literals by design and every statement here
  lives in one. **Measure the bundled SQLite rather than reading its
  documentation.** A date function whose time value is omitted reads the clock
  while carrying no marker; one modifier in that position does the same and the
  rest return null, which is what bounds the residual. Enumerate the functions
  from the binary, not from memory. Match positionally, since the same word
  applied to a supplied time value is legitimate.
- Absence assertions carry probes proving each predicate fires.
- **Test**: whichever way the measurement goes, pin it, so an upgrade that
  changes the behaviour fails on the behaviour rather than silently.

### Definitions of done carried from 0.2

- Every fixture registered against 0.5 exists and is named for it.
- Every key the sections this checkpoint introduces carry is bound and verified.
  This checkpoint introduces none, so the obligation is discharged empty rather
  than skipped.

### Constraints

No `double` or `float`. Nothing here reads or writes market data. Reconcile the
detail and the archive at sign-off, not during the build.

## 0.6 Synthetic chain loader

Read `CLAUDE.md`, `BUILD_PLAN.md` §0.6 and "How this document works",
`DATA_AND_SCHEMA.md` §2, §4.1 and the Time section, D-W29 and D-W30,
`WORKED_EXAMPLE.md` §2 and §5, the `FIXTURES.md` rules, and Current state above.

Phases 1 to 7 all run on synthetic chains and none exists. The difficulty is not
parsing. **A synthetic chain is written by a person**, which makes the format a
usability decision with a correctness consequence: a case nobody can write is a
case nobody constructs, and a number that reads back differently from how it was
written defeats the reason for writing it by hand.

### D-W31, the decision this checkpoint lands

A synthetic chain is authored rather than generated. The format optimises for
being written and read by hand and pays for that in loading cost. It states what
the format optimises for rather than what the format is, so a later checkpoint
changing the format supersedes nothing while one changing the property has
something to supersede.

Register the checkpoint's checks. The registry set is empty and the detail
already says registering it is due when this prompt is written.

### Settle the format, and report the choice

The open question is a schema-mirroring shape, rows per table, against a domain
shape, a chain per name per date.

- **Decide it on identity, not on readability.** The fields a schema-mirroring
  row repeats are three of the four that make up contract identity, so a hand-typo
  produces a different contract rather than a parse error. That is the failure
  D-W29 exists to prevent, arriving through the fixture instead of the store.
- **Every value is quoted, including the numbers, and an unquoted one is
  refused.** The source guard names a JSON number bound into an untyped tree as
  something it cannot catch, so close it by construction rather than by
  discipline. Quoted, the file carries exactly the text the parser reads.
- **Absent is absent, never zero.** The document supplies bid, ask and delta and
  the schema has seven more fields. A zero gamma is a false observation rather
  than a missing one.
- **State each fact once.** The underlying's close on the snapshot date is in the
  bar; do not repeat it on the chain. Derived columns are not observations and are
  not carried.
- Report where files live and how one is found, since the detail says neither.
  Use no configured root: take text, and the checkpoint introduces no key.

### The loader

Objects, not rows. No market-data table exists, so nothing here reaches the
store and Phase 1 wires it. Define the quote and bar types it produces, being
what a chain can express rather than the domain model Phase 2 will want.

- Values go through the existing stored forms and the identity factory. Add no
  parsing. The refusing path, since a hand-written value is exact and one beyond
  the scale is a malformed chain rather than one to round.
- **Output is in contract identity order**, never file order. Write the test
  fixture out of order deliberately: a file already sorted would pass on a loader
  that sorted nothing.
- **Test**: loading twice gives one sequence, and expiry orders before right and
  right before strike, since a strike-only case would pass on a loader that sorted
  only by strike.

### Malformed fails whole

Parse then yield. A partially loaded chain looks like a chain and whatever it is
missing is missing silently.

- **Report every reason in one pass, not the first.** A hand-written file carries
  three typos as often as one. Same reasoning as a gate recording every failing
  reason [D-W22], as an analogy rather than as authority.
- Carry the path to the offending value in each message, or the reader cannot fix
  the file.
- **Refuse an unrecognised property rather than ignoring it.** A misspelled field
  ignored silently leaves the value absent and the chain loading with nothing to
  show for it. This is the worst failure a hand-written file has.
- Report what else was treated as malformed. A duplicate identity and a quote
  whose bid exceeds its ask are the two worth a judgement, and the second is a
  domain rule rather than a format one.
- **Test the boundary next to each refusal**, so a refusal is known not to have
  swallowed the case beside it: a locked market where bid equals ask loads, and an
  expiry on its own snapshot date loads.
- Cases are inline strings, not files. A malformed file in the data directory
  reads as data rather than as a test case.

### The acceptance test

`WORKED_EXAMPLE.md` §2 and §5 are the acceptance case, so the format is chosen
against something rather than against nothing.

- **Parse the two tables and compare, rather than restating their numbers.** A
  second copy of a number is a second thing to keep true. It also closes a live
  coupling: §3 carries an unresolved banner whose recorded fix may revise the
  quotes, and a parsed oracle fails on that revision and names the value.
- **Draw the line at tables.** Symbol, snapshot date, expiry and right are
  constants in the test: they are structural, stated once in prose, and if one
  changed the example would be a different example. Build no prose parser; a regex
  over a sentence breaks on a rewording that changes no fact.
- **Vacuity**: assert both tables parsed to a non-empty set before comparing.
- Demonstrate the oracle failing on a divergence, and revert.
- Report where the line was drawn, whether it held, and whether parsing was
  harder than restating.

### Definitions of done carried from 0.2

- Every check registered against 0.6 exists in its kind.
- Every key the sections this checkpoint introduces carry is bound and verified.
  This checkpoint introduces none, so the obligation is discharged empty rather
  than skipped.

### Constraints

No `double` or `float`. No ambient clock. Nothing here writes to the store.
Reconcile the detail and the archive at sign-off, not during the build.

## 0.7 Append-only guards

Read `CLAUDE.md` §2, `BUILD_PLAN.md` §0.7 and the carried obligations,
`DATA_AND_SCHEMA.md` §3 and §4, D-W3, D-W8 and D-W26, the `FIXTURES.md` rules,
FX-NoDecimalOrderingInSql, `guards.ps1`, and Current state above.

The rule has existed since D-W8 and nothing has ever enforced it. Building the
check settles three things the corpus left open: which tables it covers, what
mechanism it is, and whether the source guards move to Roslyn.

### Settle the mechanism first, because it decides the shape

- **Measure before choosing.** `guards.ps1` strips raw string literals and every
  SQL statement here lives in one, so a pattern added to the script matches
  nothing in the tree by construction. The script's own stripper self-test
  already demonstrates it.
- So this is a `fixture`, which is also what 0.4's criterion says: a guard is for
  a check that reports when nothing else can, a fixture for one needing a
  vocabulary and structure. 0.7's detail says the opposite and is corrected.

### The vocabulary, and check every entry's authority

- Flat set beside `DecimalColumns`, same two-directions contract: every name must
  appear in a §4 schema block, and the reverse is a definition of done on the
  checkpoint that adds each table. Most entries name tables that do not exist,
  which is the point.
- **Check what each entry rests on before it goes in, and expect to find a
  citation naming a decision for a property that decision does not state.** This
  corpus has had two. Reading the decision does not find them; building the check
  that rests on them does.
- Report which tables are in, which are forward-declared and which are live.
- A table whose schema shape and stated rule disagree stays out and is raised.
  Putting it in would settle the disagreement by implication.

### The check

- A pure detector over SQL and the vocabulary, exercised on synthetic statements
  with the tree as its negative controls. Reuse the existing SQL extraction
  rather than writing a second scanner, and say whether it needed changing.
- **Statement form is what excludes the trigger DDL.** Require the `FROM` and the
  `SET`, so `BEFORE UPDATE ON` matches neither. That is a property of what those
  statements are rather than an exemption granted to them.
- Cover the quoted identifier forms. One statement spelled four ways is lexical,
  and a check missing the quoted form is evaded by a paste from a database
  browser. Pin a table alias as a known miss instead, since widening the pattern
  invites ambiguity about which token is the table.
- **Test**: a violating statement is reported, naming the table.
- **Test**: each statement already in the tree, individually, with the mechanism
  that excludes it asserted rather than left incidental. Where the mechanism is
  scan scope, assert both halves: that the statement IS an offence, and that it
  does not appear in the scanned scope.
- **Test**: the vocabulary and the scan are each asserted non-empty first.
- **Test**: every vocabulary name appears in a §4 schema block.
- **No exemption mechanism.** A statement not reported is one whose table, form
  or scope puts it outside the rule, each fixed once with a reason.
- **DoD**: introducing a violation fails. Demonstrate, revert.

### The Roslyn question, which this checkpoint owns

- **Measure two facts rather than arguing them.** Whether an analyser still
  reports when the compilation has errors elsewhere, which is the script's stated
  justification; and what a package costs in a repository that drops rather than
  suppresses.
- Compare all four checks against the mechanisms: what a text scan sees, what a
  fixture sees, what an analyser would add.
- **Decide, and record it as a decision either way.** A no-change outcome is
  still a decision, because what it records is why the mechanism stays, and a
  later phase reopening it should supersede something rather than redo the
  comparison. Say what would reopen it.
- Close the carried obligation with the answer rather than letting it lapse.

### Definitions of done carried from 0.2

- Every check registered against 0.7 exists in its kind.
- Every key the sections this checkpoint introduces carry is bound and verified.
  This checkpoint introduces none, so the obligation is discharged empty rather
  than skipped.

### Constraints

No `double` or `float`. No ambient clock. Nothing here writes to the store.
Reconcile the detail and the archive at sign-off, not during the build.

## 0.8 Configuration values and write-time invariants

Read `CLAUDE.md` §1 and §3, `BUILD_PLAN.md` §0.8 and the Phase 0 definition of
done, `CONFIG_REFERENCE.md` whole, `SYSTEM_DESIGN.md` §3.5 and §8,
`WORKED_EXAMPLE.md` §1, D-W4, D-W11, D-W12, D-W14, D-W20, D-W22 to D-W27 and
D-W34, `ConfigurationInvariants`, and Current state above.

The last checkpoint of Phase 0. Every value the lab will run under is absent, and
the two cross-key invariants have been pure predicates with no caller since 0.2.
This sets the values and gives the predicates teeth.

### Count the unset keys before believing the detail

- **Measure, do not read.** The detail names four unset keys. Count the `Unset`
  markers in `CONFIG_REFERENCE.md` and check them against D-W22 to D-W25. Where
  the two documents disagree, say which is wrong rather than reconciling silently.
- The `Policy:` keys carry no marker and are seeded anyway, or D-W23's invariant
  cannot be exercised: the predicate passes vacuously against an empty band set,
  which its own fixture has asserted since 0.2.
- **Provenance is judged per key, not per section.** A section is not a unit of
  provenance, and `Costs:` is the case that proves it: the commission is a
  property of the modelled market that D-W12 fixes in advance, the assignment fee
  is stated nowhere, and they are in the same section.

### The values, and say which kind each is

- Three kinds: transcribed from a corpus statement, taken from a value a decision
  proposed, or judged. Say which for every key, and mark the judged ones.
- **A judged value is argued from what constrains it, and where nothing does, say
  so.** A free choice presented with a rationale reads as derived, which is worse
  than saying it was chosen.
- **Look for a second constraint on every bound before setting it.** One of them
  has a constraint no document names: check the worked example's own trial against
  the day bound, and against what that example's stated total depends on.
- What is deliberately not seeded gets a stated reason and **a carried obligation
  naming the phase that owes it**. Unseeded and unscheduled are different, and
  only the first is a decision.

### D-W23's open clause, which this checkpoint owns

- Settle it, and settle only what the argument reaches. The ceiling and the floor
  of one band are two questions and D-W4 reaches one of them.
- Where the argument does not reach, report the value as inherited and name the
  measurement that would settle it, without making it. A measurement 0.8 cannot
  perform is not an argument 0.8 can use.
- Closing the clause is authored, so raise the wording rather than writing it.

### Enforcement, in the write path

- **In `ConfigWriter`, not in the seeder.** The seeder is one caller. D-W23,
  D-W24 and D-W27 all put enforcement at the moment a version is written,
  precisely because versions are insertable while the process runs.
- One transaction for the whole seed. An invariant over two keys cannot be
  evaluated while only one exists, so a loop over `Append` either fails on the
  first key or passes vacuously until the last. That loop is the obvious
  implementation and is wrong in a way that passes.
- Each invariant's key set is a declared vocabulary beside the predicates, the
  same shape as `DecimalColumns` and `AppendOnlyTables`.
- A write leaving a touched invariant unevaluable is refused [D-W34]. A write
  touching no invariant key is permitted whatever else is absent.
- **Expect existing tests to fail, and read the failure before fixing it.** A
  fixture using an invariant's key as a convenient arbitrary key was relying on
  the write path being unguarded. Move it to a key in no invariant and say why in
  a comment, because moving it back is the obvious edit.

### The seed, as a verb

- A verb beside `migrate`, not a migration. A migration is applied once and
  recorded by id and can never be corrected except by another migration; a config
  value is expected to be revised, and version 1 arriving by a different route
  from every later version gives "how did this value get here" two answers.
- Idempotent by **skipping**, not by overwriting. Write the first version of each
  key that has none, skip the rest, report both. An identical version + 1 is legal
  and would fill the history with revisions that revised nothing and overwrite an
  operator's later value on every run.
- A refusal is an outcome of the verb, not a crash. Report it; the messages name
  their decision and say no row was written, and a stack trace buries them.
- **Test**: both invariants, both directions of D-W34, and a refused write leaving
  the store exactly as it was, asserted by comparing rows rather than by the
  absence of an exception.
- **DoD**: the refusal demonstrated end to end against a real store, and a second
  seed run shown to be a no-op.

### Documentation

- Replace the `Unset` markers with the value in force and the reason for it.
  Consumer cells stay `Unverified`: every consumer is Phase 2 or later, so the
  definition of done means named, not verified.
- **Say that the store is the authority, not the document.** A value recorded here
  is version 1; a revision inserts version + 1 and does not edit the document.
  Without that sentence the first revision makes the document read as wrong.
- Record any coupling the schema cannot express. A key absent because another key
  covers it looks like an omission to a reader of the reference alone, and this is
  the checkpoint that makes that reader exist.
- **Check every sentence you are about to make a reader trust.** Seeding the
  values a design paragraph describes is what finds the paragraph that was wrong
  about them, the same way building a check found three wrong citations at 0.7.

### Definitions of done carried from 0.2

- Every check registered against 0.8 exists in its kind.
- Every key the sections this checkpoint introduces carry is bound and verified.
  It introduces none, and the keys it sets are `rows`-classed and so never bind, so
  the obligation is discharged with that reason rather than empty.

### The Phase 0 definition of done, which nothing after this will demonstrate

- Run each of the five items and record its output rather than asserting it.
- **Anything in it that can be a standing check should be one, and check whether
  it already is before building it.** One item was already covered by an
  unregistered suite, and a report claiming otherwise was written from the
  fixture files without reading the rest of the tests.
- Where a standing check exists outside the registry, register it and move it.
  Two tests asserting one thing with two failure messages is a fact kept in two
  places.

### Constraints

No `double` or `float`. No ambient clock; the instant is read at the verb and
threaded down. Money is decimal in `TEXT`. Reconcile the detail and the archive at
sign-off, not during the build.
