# Phase 0 Foundations: spent prompts

Current state below is the whole state of the repository and the only
description of the present.

One prompt per checkpoint, being the prompt that produces the checkpoint as it
now stands. Corrections found while building are folded back into the
checkpoint's prompt rather than appended as further entries, so replaying the
prompts in order against the corpus reproduces the current state without
replaying the mistakes.

One file per phase. It closes when Phase 0 signs off; Phase 1 opens its own.

---

# Current state

Corpus v1.9.9.

| | |
|---|---|
| Phase 0 | 0.1, 0.2, 0.3 and 0.4 built; 0.5 onward not started |
| Branch | `phase-0/checkpoint-0.4`, off `main` |
| Merged | PR #1 as `53cc0b4` and PR #2 as `a2b8c28`, both merge commits, neither squashed |
| CI | green, 152 tests, guards then restore then build then test, on push to `main` and every pull request |

## Build

.NET 10 solution, nullable enabled, warnings as errors, central package
management with transitive pinning: `OptionsWheelLab.Core` holding the
composition root, options types and the storage layer, `.Worker` and `.Api` as
thin hosts both calling it, and `.Tests`, which references both hosts and
carries tests that use them, so a broken host fails `dotnet test` and not only
the separate build step.

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

`migrate.ps1` supplies the instant and refuses when `Storage__Path` is unset.
The Worker carries the `migrate` verb, because the Worker is the sole writer.

## Configuration

Two sections bound: `Eodhd` to `EodhdOptions` and `Storage` to
`StorageOptions`, both verified by reading the composition root.

Six sections deliberately unbound because `CONFIG_REFERENCE.md` classes them
`rows` and a registered options type is itself a current-value accessor: `Risk`,
`Gate`, `Costs`, `Policy`, `Trial`, `Scoring`.

`CONFIG_REFERENCE.md` carries 27 key rows, one key per row. Four Consumer cells
are verified; 23 carry **Unverified**. No value is set that the document marks
unset.

Configuration is readable two ways, as separate types so neither can be reached
from the other. `AsOfConfiguration` takes a date on every member and resolves
`MAX(version)` among rows at or before that date's last instant.
`CurrentConfiguration` returns the newest and is for operational paths only.
`ConfigWriter` appends `MAX(version) + 1` computed inside the insert's own
transaction, with `set_at` supplied rather than read from a clock and refused if
it predates the newest version of that key.

The two cross-key invariants remain pure predicates over supplied values, with
no host, no config store, no startup wiring and no clock.

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
calls it before the build, because a source guard must fail even when the build
does not. Today it bans floating point across `src` and `tests` with no exemption
mechanism of any kind. The catch-list covers the two keywords plus
`Random.NextDouble`, `Convert.ToDouble`, `GetDouble`, the `Math` functions and
exponent literals, none of which carry a `double` token. It self-tests on three
samples before scanning, one of which exists because a literal stripper that
desyncs still scans every file and still reports success.

It catches declared intent, not inferred types, and says so.

## Tests

152 across fourteen fixtures plus the 0.1 smoke test and eleven unregistered
suites.

| Fixture | Tests |
|---|---|
| FX-MoneyRoundTrip | 16 |
| FX-TickerDashForm | 12 |
| FX-ConfigStoreClassHonoured | 12 |
| FX-NoDecimalOrderingInSql | 11 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-ConfigResolvesAsOf | 6 |
| FX-EveryConfigSectionBinds | 6 |
| FX-MigrateFromEmpty | 6 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-ApiCannotWrite | 3 |
| FX-NoCurrentConfigReadOnSimulatedPath | 3 |
| FX-SnapshotRestoresIdentically | 3 |
| FX-RegistryMatchesDisk | 1 |

All fourteen fixtures registered against 0.2, 0.3 and 0.4 are implemented and
named for their registry entry. The suite parses `CONFIG_REFERENCE.md`,
`FIXTURES.md` and `DATA_AND_SCHEMA.md`, so all three are load-bearing rather than
descriptive.

Every store test creates its own database in a temp directory, because the
append-only triggers make `config_rows` impossible to clean between cases. No
test touches the configured store directory, so the suite runs on CI where that
path does not exist.

## Layout

Repository root holds `README.md`, `CLAUDE.md`, `migrate.ps1` and `guards.ps1`.
Every document is in `docs/`. Spent prompts are in `prompts/spent/`.

`Core` has three folders: `Configuration`, `Storage` and `Identity`.

## Working rules in force

- Commit subjects are prefixed with the phase name and stage, as
  `Phase 0 Foundations / 0.4 - <type>: <subject>`.
- The pull request description is updated on every check-in.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

## Not built

Market data tables and every other table. The deterministic clock. The fixture
loader. The append-only CI greps. Every checkpoint from 0.5 onward.

Nothing writes a decimal through a typed path yet: `ConfigWriter.Append` takes a
string, so D-W29's write-side rule is a convention with no enforcement behind it.

## Owed

- **Phase 11**: re-add `Microsoft.AspNetCore.OpenApi` against a version whose
  `Microsoft.OpenApi` dependency clears the audit. In `BUILD_PLAN.md` carried
  obligations.
- **0.8**: wire the two cross-key invariants to the config write path, and
  FX-ConfigWriteRefusesInvariantBreach.
- **0.7**: decide whether the source guards move to a Roslyn analyser. A text
  scan cannot see an inferred type, and 0.7 is where three guards exist and one
  mechanism serving all of them can be compared concretely.
- **Phase 1**: give D-W29's write-side rule teeth, most likely a decimal-typed
  parameter-binding seam, when the first real decimal column exists. Also decide
  what an adjusted strike does when division makes it non-terminating, since
  identity takes the refusing path.

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

Read `CLAUDE.md`, `BUILD_PLAN.md` §0.4, `DATA_AND_SCHEMA.md` §2 and §4, the
`FIXTURES.md` rows at 0.4, and Current state above.

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
- **Parse lenient about padding, strict about precision.** A hand-written config
  row carries `0.35`. But `decimal.Parse` silently rounds beyond 29 significant
  digits, so count places on the string, not on the parsed value: a row that
  reads back as a different number than it states defeats the point of the store.
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

### Definitions of done carried from 0.2

- Every fixture registered against 0.4 exists and is named for it.
- Every key the sections this checkpoint introduces carry is bound and verified.
  This checkpoint introduces none, so the obligation is discharged empty rather
  than skipped.

### Constraints

No `DateTime.Now` or `DateTime.UtcNow`; the clock is 0.5. Nothing here reads or
writes market data.
