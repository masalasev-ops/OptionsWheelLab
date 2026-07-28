# BUILD_PLAN

Build state: **Phase 0 in progress**. 0.1 and 0.2 built; 0.3 onward not started.

## How this document works

Three kinds of content live here, and they have different shelf lives. Keeping
them straight is the point of the structure.

**The phase map** is design and is stable. It lives in `SYSTEM_DESIGN.md` §7 and
is not duplicated here.

**Checkpoint detail** is written one phase ahead, never further. Writing it eight
phases ahead is what made the equivalent AlphaLab document go stale, because a
checkpoint's acceptance criteria depend on decisions that have not landed yet.

**Prompts** are written immediately before they are spent. Once a checkpoint is
built, `prompts/spent/phase-N.md` carries one prompt for it, being the prompt
that produces the checkpoint as it now stands rather than the sequence of asks
that reached it. A correction found while building is folded back into that
checkpoint's prompt, so replaying the prompts in order reproduces the state
without replaying the mistakes. The file also carries one **Current state**
section holding the whole state of the repository, read in a single pass without
consulting another document. Only unspent prompts are subject to propagation.

### The propagation rule

At every reconciliation, ask: **does this decision change a prompt that has not
been spent yet?**

That question is the whole guard. It distinguishes a live gap, which must be
fixed, from historical drift in a spent prompt, which must be left alone.

### The enumeration rule

Prompts reference the fixtures registered against a checkpoint in `FIXTURES.md`.
They do not list fixture names inline. A prompt that enumerates fixtures goes
silently incomplete the moment a fixture is added elsewhere, which is a failure
that has already happened once in a sibling project.

---

## Phase 0 — Foundations

Delivers a repository that compiles, tests, migrates, and runs deterministically,
with no market data and no domain logic.

Definition of done for the phase: `dotnet test` green, `migrate.ps1` runs clean
from empty, CI green on a fresh clone, every `app`-classed key in
`CONFIG_REFERENCE.md` proven to bind, and no `rows`-classed key bound from
`appsettings` [D-W27].

### 0.1 Repository skeleton

Solution with `OptionsWheelLab.Worker`, `OptionsWheelLab.Api`, `OptionsWheelLab.Core`,
`OptionsWheelLab.Tests`. .NET 10. `CLAUDE.md` at root. `appsettings.Secrets.json` in
`.gitignore` with a committed `.example` alongside.

Configuration is one shared `src/appsettings.json`, linked into both hosts and
the test project rather than one file per host, because a Worker and an Api
disagreeing about the lab's configuration is a defect and two files is how that
happens. It loads from `AppContext.BaseDirectory` rather than the host default,
because the generic host and the web host default their content roots
differently.

No `Logging` section is committed, so every top-level section in
`appsettings.json` binds to one of the lab's own options types and the binding
test needs no framework allowlist. An allowlist is where a stray section would
hide. Adding logging configuration later fails that test until the section is
given a declared home, which is the intended prompt rather than a defect.

- **Test**: solution builds; a trivial test passes in `OptionsWheelLab.Tests`.
- **DoD**: CI green on a fresh clone with no local state.

### 0.2 Configuration binding

Bind only the sections `CONFIG_REFERENCE.md` classes as `app` [D-W27]. A section
classed `rows` is never given an appsettings-bound options type, not even as a
placeholder, because a registered options class is itself a current-value
accessor and 0.3 would then hold two paths to the same values.

Also in 0.2: the two cross-key invariants [D-W23, D-W24] as pure predicates in
`Core` over supplied values, with no host, no config store, and no startup
wiring. They are wired to the config write path when it lands at 0.8.

Implement the fixtures registered against 0.2 in `FIXTURES.md`, reading their
assertions from that file.

- **DoD**: the binding test fails when a stray section is added to any committed
  configuration file, being `appsettings.json` and
  `appsettings.Secrets.example.json`, and passes when it is removed. Demonstrate
  both. The uncommitted `appsettings.Secrets.json` is out of scope, being absent
  on a fresh clone.
- **DoD**: `CONFIG_REFERENCE.md`'s Consumer column names the component and the
  verified type, as `component via TypeName`, for every key bound in this
  checkpoint.
- **DoD**: every fixture registered against 0.2 in `FIXTURES.md` exists and
  is named for it. This is the entry-to-file direction of rule 2 and applies
  to every checkpoint from here on.
- **Why this checkpoint exists at all**: a sibling project shipped two
  configuration blocks that were never bound, so editing them silently did
  nothing. The test is cheap now and expensive to retrofit.
- **Note**: from this checkpoint the test suite parses `CONFIG_REFERENCE.md` and
  `FIXTURES.md`, so both are load-bearing rather than descriptive. An edit that
  breaks their table shape fails the build, which is the intended cost of making
  them checked contracts.

### 0.3 Store bootstrap and migrations

SQLite store, snapshot-first migration runner, `migrate.ps1` calling the snapshot
tool internally first. Worker is the sole writer; Api opens read-only.

- **Test** FX-MigrateFromEmpty: migrating an empty database produces the expected
  schema version and is idempotent on a second run.
- **Test** FX-ApiCannotWrite: an attempted write through the Api connection
  throws.
- **DoD**: `migrate.ps1` leaves a timestamped snapshot beside the store.

Also in 0.3: the config store and its read service. The as-of surface and the
current-value surface are separate types, and the type a simulated-date path
depends on exposes no way to read current [D-W26]. Enforcing this by API shape
rather than by scanning callers means a misuse cannot be written rather than
being detected after it is. Implement the fixtures registered against 0.3 in
`FIXTURES.md`.

The invariant enforcement on config writes is owed by 0.8, not here [D-W23,
D-W24]. Nothing is seeded until 0.8, so there is no window in which an unguarded
write path could admit a violating version.

### 0.4 Money and identity primitives

Decimal-as-TEXT storage helpers with round-trip tests. Ticker normalisation to
the EODHD dash form. Contract identity as the underlying, expiry, right, strike
tuple.

- **Test** FX-MoneyRoundTrip: a set of adversarial decimals round-trips through
  storage without loss, including values that lose precision as doubles.
- **Test** FX-TickerDashForm: `BRK.B` and `BRK-B` normalise to the same key.
- **DoD**: no `double` or `float` appears in any monetary path; a CI grep
  enforces it.

### 0.5 Deterministic clock

An `IClock` abstraction injected everywhere. No call to `DateTime.Now` or
`DateTime.UtcNow` outside the clock implementation.

- **Test** FX-NoAmbientClock: a CI grep fails the build on `DateTime.Now` or
  `DateTime.UtcNow` outside the permitted file.
- **DoD**: a simulated run with a fixed clock produces byte-identical output
  across two invocations.

### 0.6 Fixture harness

The loader that reads synthetic chain fixtures, plus `FIXTURES.md` as the single
registry. Fixtures are declared against a checkpoint, and the harness discovers
them from the registry rather than from a hardcoded list.

- **Test** FX-RegistryMatchesDisk: every fixture in `FIXTURES.md` exists on disk
  and every fixture on disk is registered, failing on either mismatch.
- **DoD**: adding a fixture file without registering it fails the build.

### 0.7 Append-only guards

CI greps asserting no `DELETE FROM` or `UPDATE` against snapshot tables
[D-W8], and none against `decisions` or `candidates` [D-W3].

- **Test**: the grep fails the build when a violating statement is introduced in
  a scratch file, verified by a test that adds and removes one.
- **DoD**: guard runs in CI, not only locally.

### 0.8 Configuration values for the open parameters

Set `MaxRolls` and `MaxTrialDays` [D-W14], and the divergence threshold and
window [D-W20]. These are policy choices, deliberately not fixed in the design.

- **DoD**: values are recorded as config rows with a note explaining the choice,
  and appear in `CONFIG_REFERENCE.md` with their consumer named.
- **DoD**: the cross-key invariants are enforced at config-write time, and an
  attempted insert violating either is refused with no row written [D-W23,
  D-W24]. Implement the fixture registered against 0.8 in `FIXTURES.md`.
- **Note**: these values are expected to be revised. Because config rows are
  append-only and versioned, a revision inserts version + 1 and the old value
  stays readable, which is what lets a later behaviour change be explained.

---

## Carried obligations

Work deferred out of a checkpoint that a later phase must claim. An entry
leaves this list only when the phase that owns it has done it, never because it
has aged.

| Owed at | Obligation | Raised |
|---|---|---|
| Phase 11 | Re-add `Microsoft.AspNetCore.OpenApi` against a version whose `Microsoft.OpenApi` dependency clears the audit. Removed at 0.1 rather than suppressing the advisory; the reason is in the Api project file. | PR #1 |

---

## Phase 1 and beyond

Not yet written. Phase 1 checkpoint detail is authored when Phase 0 signs off,
with whatever Phase 0 taught already folded in.

The phase map in `SYSTEM_DESIGN.md` §7 states what each phase delivers and where
the data purchase boundary falls.
