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

Corpus v1.9.8.

| | |
|---|---|
| Phase 0 | 0.1, 0.2 and 0.3 built; 0.4 onward not started |
| Branch | `phase-0/checkpoint-0.3`, off `main` |
| Merged | PR #1 into `main` as `53cc0b4`, 24 commits preserved, not squashed |
| CI | green, 92 tests, restore and build and test on push to `main` and every pull request |

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

## Time

Dates are `yyyy-MM-dd` and timestamps `yyyy-MM-ddTHH:mm:ss.fffZ`, both UTC and
fixed width. A date is widened to its last instant in one place before meeting a
timestamp column. Filenames use `yyyyMMddTHHmmssfffZ`, because a colon is
illegal in a Windows path.

## Tests

92 across eleven fixtures plus the 0.1 smoke test and eight unregistered suites.

| Fixture | Tests |
|---|---|
| FX-ConfigStoreClassHonoured | 12 |
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

All eleven fixtures registered against 0.2 and 0.3 are implemented and named for
their registry entry. The suite parses `CONFIG_REFERENCE.md` and `FIXTURES.md`,
so both are load-bearing rather than descriptive.

Every store test creates its own database in a temp directory, because the
append-only triggers make `config_rows` impossible to clean between cases. No
test touches the configured store directory, so the suite runs on CI where that
path does not exist.

## Layout

Repository root holds `README.md`, `CLAUDE.md` and `migrate.ps1`. Every document
is in `docs/`. Spent prompts are in `prompts/spent/`.

## Working rules in force

- Commit subjects are prefixed with the phase name and stage, as
  `Phase 0 Foundations / 0.3 - <type>: <subject>`.
- The pull request description is updated on every check-in.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

## Not built

Market data tables and every other table. The deterministic clock. Money and
ticker primitives. The fixture loader. The append-only CI greps. Every
checkpoint from 0.4 onward.

## Owed

- **Phase 11**: re-add `Microsoft.AspNetCore.OpenApi` against a version whose
  `Microsoft.OpenApi` dependency clears the audit. In `BUILD_PLAN.md` carried
  obligations.
- **0.8**: wire the two cross-key invariants to the config write path, and
  FX-ConfigWriteRefusesInvariantBreach.

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
