# BUILD_PLAN

Build state: **Phase 0 in progress**. 0.1, 0.2, 0.3 and 0.4 built; 0.5 onward
not started.

## How this document works

Three kinds of content live here, and they have different shelf lives. Keeping
them straight is the point of the structure.

**The phase map** is design and is stable. It lives in `SYSTEM_DESIGN.md` §7 and
is not duplicated here.

**Checkpoint detail** is written one phase ahead, never further. Writing it eight
phases ahead is what made the equivalent AlphaLab document go stale, because a
checkpoint's acceptance criteria depend on decisions that have not landed yet.

A checkpoint's detail passes through three states, and the middle one is a single
event rather than a period.

**Not built.** The detail is live intent. It is corrected freely, and must be,
whenever something that has landed changes what the checkpoint should build. A
correction here is the propagation rule doing its job.

**Signed off.** The detail is frozen. It is not revisited, because the archive
now holds the prompt that reproduces the checkpoint and the Current state that
describes the result, and a third description that kept moving would be the least
authoritative of the three.

**Determined fully built**, which is the transition between them and happens
once. That moment is **after review has closed and before the merge**, not when
the last line is written. Review is part of determining a checkpoint fully built
because it changes what shipped: at 0.4 it changed the deliverable four times,
and the prompt had been archived before any of it, so replaying that prompt would
not have reproduced the tree. Reading "fully built" as "I have finished writing
it" reproduces exactly the staleness this rule exists to prevent.

At that point, and only then, the detail is reconciled against what shipped, and
the checkpoint's prompt is appended to `prompts/spent/phase-N.md` with Current
state overwritten. Both halves belong to the same moment: the reconciled detail
says what the checkpoint turned out to be, and the archive says how to reproduce
it. Doing this at sign-off rather than during the build is what keeps Current
state true, because it is then written after the last change rather than before
it.

A checkpoint's detail names everything the checkpoint ships, including
corrections it carries that nothing in the checkpoint caused. The detail is what
the build is measured against, so a checkpoint shipping more than its detail
predicts leaves the detail describing an idealised version of the work rather
than the work. Correcting the detail as the scope becomes clear is the
propagation rule doing its job. Leaving it, and recording the difference only in
the changelog, is how this document becomes ceremonial.

The build-state marker above says which sections are frozen and which are still
intent. Three things stay live regardless of it, because each is read before work
rather than after: the phase definition of done, the carried obligations, and the
detail for checkpoints not yet built.

A frozen section may still be corrected by a landed decision, on the authority of
the decision rather than of the code [`CLAUDE.md` §10]. That is the only thing
that reaches one.

**Prompts** are written immediately before they are spent. Once a checkpoint is
built, `prompts/spent/phase-N.md` carries one prompt for it, being the prompt
that produces the checkpoint as it now stands rather than the sequence of asks
that reached it. A correction found while building is folded back into that
checkpoint's prompt, so replaying the prompts in order reproduces the state
without replaying the mistakes. The file also carries one **Current state**
section holding the whole state of the repository, read in a single pass without
consulting another document. Only unspent prompts are subject to propagation.

A prompt names the corpus version it was written against. If `PROGRESS.md`
reports a different one, establish what changed before doing anything else.
Proceed only where the drift demonstrably does not reach what the prompt depends
on, and say in the report what changed and why it does not reach it. Where it
does reach, or where that cannot be established, stop.

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

SQLite store, snapshot-first migration runner, `migrate.ps1` as the operator
entry point invoking the runner so a hand-run cannot skip the snapshot. Worker is
the sole writer; Api opens read-only.

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

Reconciled at sign-off against what shipped. Three things were larger than the
scope above.

The decimal form needed **two entry points, not one** [D-W29]. The scale is a
fidelity requirement for a vendor-supplied value, which must refuse rather than
lose a digit quietly, and a rounding policy for a computed one. Decimal division
is non-terminating in general, so a single refusing function could not store the
worked example's own first return.

Two further stored forms came with it, for one reason: the obvious rendering is
culture-independent, plausible and wrong. A bare `ToString()` on a date gives
`MM/dd/yyyy` under `InvariantGlobalization`, and on the contract right gives
`Put` where the schema says `put`.

The configuration surfaces gained typed decimal and integer accessors, so the
canonical form is validated where it is read rather than assumed, and so
changing the scale is one edit.

Contract identity gained a total order, because three makers receiving
byte-identical candidate sets [D-W4] cannot depend on the order candidates
arrive in.

Implement the fixtures registered against 0.4 in `FIXTURES.md`.

- **DoD**: no `double` or `float` appears in any monetary path; a source guard
  enforces it. Shipped as `guards.ps1`, called by CI before the build so it
  fails even when the build does not, scanning the whole tree with no exemption
  mechanism. Its catch-list is wider than the two keywords, because
  `Random.NextDouble`, `Convert.ToDouble` and the `Math` functions carry
  neither.
- **DoD**: `50`, `50.0` and `50.00` produce one stored string. This is the
  identity property rather than formatting: strike participates in contract
  identity, so two spellings would give one contract two identities.

### 0.5 Deterministic clock

D-W30 is this checkpoint's design and lands with it: the clock returns the
instant the process is running at, a simulated date is never obtained from it,
and it is read at composition and entry points only. Nothing below them reads a
clock; they take instants as parameters. No call to `DateTime.Now` or
`DateTime.UtcNow` outside the clock implementation.

An `IClock` abstraction, being the one member the decision describes. The
alternative is .NET's `TimeProvider`, and the choice is reported with its
reasons rather than assumed.

The ambient-clock check extends the source guards 0.4 established rather than
introducing a second mechanism. Whether those guards stay a text scan is open
until 0.7, so this checkpoint states the rule and adds a check to whatever they
are, rather than committing to an implementation a later checkpoint may replace.
The permitted file is named in the script rather than implied, and has to earn
its place: scanning it must find an ambient call, or the carve-out is stale.

The two halves of D-W30 need two mechanisms. FX-NoAmbientClock is a `guard`,
because a source guard must fail even when the build does not.
FX-ClockIsNotADateSource is a `fixture`, because it asserts over shape: that the
clock cannot hand out a date, that nothing in `Core` holds one, and that no SQL
asks the store for the time. That last part is the one place a token scan cannot
reach, since it strips raw string literals by design and every statement here
lives in one.

Implement the fixtures registered against 0.5 in `FIXTURES.md`.

- **DoD**: with a fixed clock and the same inputs, two runs produce identical
  stored rows, compared as table contents. Not as file bytes: a SQLite file is
  not a deterministic rendering of its contents, so a byte comparison would fail
  for reasons that are not about the clock [D-W28]. The output-level property is
  owed at Phase 3, which is the first checkpoint with a run to make.
- **DoD**: introducing an ambient clock call outside the permitted file fails
  locally and in CI. Demonstrate, revert.
- **DoD**: every fixture registered against 0.5 exists and is named for it.
- **DoD**: every key the sections this checkpoint introduces carry is bound and
  verified. This checkpoint introduces none, so the obligation is discharged
  empty rather than skipped.

#### Corrections this checkpoint carries

None of these was caused by the clock. They are named here because the detail is
what the build is measured against, and a checkpoint that ships them without
saying so leaves this section describing a smaller piece of work than the one
that happened.

- **The Step 0 gate** moves into "How this document works". It tripped here, on a
  docs-only version bump, and said stop. Its wording forbade what was right.
- **`FIXTURES.md` gains a Kind column** and rule 2 is restated per kind. 0.5 is
  where a registered entry first had to be a script check rather than a file, and
  0.4's guard turned out to be the same shape and unregistered.
- **The empty-subject-set clause** in rule 2, because 0.5's own fixture
  obligations are where a definition of done passing on nothing became visible.
  0.6 and 0.7 were both discharging it that way.
- **`GLOSSARY.md` gains Clock and Determinism**, because D-W30 makes "clock" a
  fifth sense of a word this corpus already overloads four ways.
- **D-W26 gains the append-only clause**, and the `config_rows` triggers stop
  citing D-W8. The clock's placement rule turned on what D-W26 does and does not
  say, and reading it closely is what found the citation wrong.
- **0.7's detail and its carried obligations**, being the constraint that counted
  five and enumerated four, and the vocabulary question underneath it. This is
  0.7 work done at 0.5: the measurement that established 0.7's mechanism came out
  of counting what this tree already contains, which is a thing 0.5 could do and
  0.7 would have had to do anyway.

### 0.6 Fixture harness

The loader that reads synthetic chain fixtures, plus `FIXTURES.md` as the single
registry. Fixtures are declared against a checkpoint, and the harness discovers
them from the registry rather than from a hardcoded list.

The registry checks are not this checkpoint's to build. FX-RegistryMatchesDisk
is registered at 0.2 and shipped there, because the file-to-entry direction is
safe from the first fixture onward. The entry-to-file direction does not become
a standing assertion here either: most entries belong to checkpoints not yet
built, so it stays a definition of done on each checkpoint [`FIXTURES.md` rule
2]. What 0.6 adds is the loader.

Implement the fixtures registered against 0.6 in `FIXTURES.md`. That set is
empty today and registering it is due when this checkpoint's prompt is written,
rather than left as a sentence resolving to nothing [`FIXTURES.md` rule 2].

- **DoD**: adding a fixture file without registering it fails the build.

### 0.7 Append-only guards

Assertions that no `DELETE FROM` or `UPDATE` reaches a snapshot table [D-W8],
and none reaches `decisions` or `candidates` [D-W3]. They extend the source
guards 0.4 established rather than introducing a second mechanism.

Also in 0.7: decide whether the source guards stay a text scan or move to a
Roslyn analyser. 0.4 raised it and deferred it here deliberately, because this
is the first checkpoint where three guards exist and one mechanism serving all
of them can be compared against three separate scans concretely rather than
argued in the abstract. The argument is that a text scan sees declared intent
and not inferred types, and it was recorded before the comparison rather than
after.

Register this checkpoint's guards in `FIXTURES.md` as `guard`-kind rows. Nothing
is registered against 0.7 today, and doing it is due when this checkpoint's
prompt is written [`FIXTURES.md` rule 2].

- **Test**: the guard fails when a violating statement is introduced, verified
  by a test that adds and removes one.
- **DoD**: guard runs in CI, not only locally.
- **Constraint**: statements already in the tree carry the banned text and must
  not be reported. They are the trigger DDL that enforces append-only, the tests
  asserting those triggers reject an `UPDATE` or a `DELETE`, and statements
  against tables the rule does not cover at all. **The check therefore
  distinguishes by table rather than by location**, which is a design problem for
  the check and not a case for an exemption list: 0.4's guard has none by
  decision, and adding one here would reopen that. A vocabulary is the mechanism
  that needs none, and `DecimalColumns` is the precedent for its shape. No count
  is given, because the count is not a property of the rule: most of those
  statements are tests, so it moves whenever one is written.

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
| 0.7 | Decide whether the source guards stay a text scan or move to a Roslyn analyser. A text scan sees declared intent and not inferred types, so `Math.Sqrt`, `Convert.ToDouble` and `Random.NextDouble` are caught only by naming each one. Scoped into 0.7 above. | PR #3 |
| Phase 1 | Give D-W29's write-side rule teeth. Every decimal reaching a `TEXT` column should pass through the canonical form, and nothing enforces that: `ConfigWriter.Append` takes a string. A decimal-typed parameter-binding seam is the likely mechanism, when the first real decimal column exists. | PR #3 |
| Phase 1 | Decide what an adjusted strike does when a corporate action makes it non-terminating. Identity canonicalises through the refusing path, so a 3-for-2 split forces a choice between rounding a value that is part of a contract's identity and carrying the ratio. | PR #3 |
| Phase 1 | Resolve aliases in the decimal-ordering detector, or adopt and check a convention that a decimal column is never aliased. `SELECT strike AS s FROM contracts ORDER BY s` orders a decimal and passes. Deleting FX-NoDecimalOrderingInSql's known-miss test is part of closing it. | PR #3 |
| Phase 3 | Establish output-level determinism: a simulated run with a fixed clock produces byte-identical output across two invocations. 0.5 restated it as identical stored rows because no run existed to make. Compared as produced artefacts, never as a database file [D-W28]. | PR #4 |

---

## Phase 1 and beyond

Not yet written. Phase 1 checkpoint detail is authored when Phase 0 signs off,
with whatever Phase 0 taught already folded in.

The phase map in `SYSTEM_DESIGN.md` §7 states what each phase delivers and where
the data purchase boundary falls.
