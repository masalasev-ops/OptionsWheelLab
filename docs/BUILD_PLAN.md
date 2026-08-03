# BUILD_PLAN

Each phase section below carries its own build state, and this document
states it nowhere else. A summary here would be a second statement of a
fact stated below, which is how the header went stale at 1.2 and stayed
stale through three sign-offs.

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

Build state: **complete**. 0.1 to 0.8 built and signed off. No market data
and no domain logic, which is what it said it would deliver.

Delivers a repository that compiles, tests, migrates, and runs deterministically,
with no market data and no domain logic.

Definition of done for the phase: `dotnet test` green, `migrate.ps1` runs clean
from empty, CI green on a fresh clone, every `app`-classed key in
`CONFIG_REFERENCE.md` proven to bind, and no `rows`-classed key bound from
`appsettings` [D-W27].

**Met at 0.8**, item by item. The fourth item has been a standing check since
0.4 and is a registered fixture from 0.8: FX-EveryAppKeyBinds walks the document
and checks the types, where its mirror walks the types and checks the document.

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
alternative was .NET's `TimeProvider`, and `IClock` was chosen because of the
guard: with it the forbidden and sanctioned token sets are disjoint, whereas
`TimeProvider`'s ambient instance and an injected one are the same type, and
telling them apart in a text scan is the type inference such a scan cannot do.
`TimeProvider` also carries a machine-local timezone, which this decision scopes
out.

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

Reconciled at sign-off against what shipped. Two things were larger than the
scope above.

**The operator entry point stopped supplying an instant.** `--at` and the
argument parsing behind it are gone, and `migrate.ps1` no longer computes a
timestamp. The detail said where the clock is read; the consequence was that
nothing outside the process may name the instant a row is stamped with, since an
override would be a way to write a `set_at` that never happened into a store
whose rows can never be corrected. Two tests went with it, because an absent or
unparseable instant stopped being a failure mode.

**The SQL half had to be measured rather than listed.** SQLite defaults a date
function's time value to the current instant when it is omitted, so
`datetime()`, `date()`, `time()`, `julianday()`, `unixepoch()` and
`strftime('%Y')` read the clock while carrying no marker at all, and `'subsec'`
in the time-value position does the same. A first argument that is any other
modifier returns null rather than implying now, which is what bounds the residual
at those two words instead of at the whole modifier set. The function list was
enumerated from the bundled binary rather than from documentation: of 168
functions, seven read the wall clock, one of which had not been considered.

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
- **0.6's detail**, which conflated a check with a synthetic chain and stated a
  definition of done that was 0.2's check restated. The Kind column is what made
  the two kinds of check distinct enough for the third thing, which is data, to
  be visible as a separate thing at all.
- **`CLAUDE.md` §2 item 4** states the property rather than listing the forms of
  an ambient time read. It named two and this checkpoint's guard catches six.

### 0.6 Fixture harness

Two different things are called fixtures in this corpus and 0.6 conflated them.
A **check** is a registry entry in `FIXTURES.md`, either a `fixture` that is a C#
test file or a `guard` that is a named check in a script. A **synthetic chain**
is test data: option quotes and bars for a simulated date, written by hand so
that assignment, early exercise and roll-cap cases can be constructed
deliberately rather than waited for [`SYSTEM_DESIGN.md` §7].

0.6 builds the loader for synthetic chains. It does not read `FIXTURES.md`,
which registers checks and contains no data. Nothing about the registry is this
checkpoint's to build: FX-RegistryMatchesDisk shipped at 0.2, and the
entry-to-artefact direction is a definition of done on each checkpoint
[`FIXTURES.md` rule 2].

D-W31 lands with this checkpoint: a synthetic chain is authored by a person, so
the format optimises for being written and read by hand and pays for that in
loading cost. That decides the open question, which was whether a chain mirrors
the schema as rows per table or takes a domain shape as a chain per name per
date. The domain shape wins, and the reason is stronger than readability: the
fields a schema-mirroring row repeats are three of the four that make up contract
identity, so a hand-typo would produce a different contract rather than a parse
error.

**The loader produces objects, not rows.** No market-data table exists; 0.3
created `config_rows` and said the rest are Phase 1. Nothing this checkpoint
produces reaches the store, and Phase 1 wires it. Stated because "the quotes and
bars a simulated date offers" reads both ways, and the wrong reading builds a
store writer with nowhere to write.

**It defines the types it produces**, there being no quote type and no bar type
in the tree. A quote is a `ContractIdentity` plus its market data. The boundary
is what a synthetic chain can express, not the domain model Phase 2 will want.

**Decimals and dates parse through the stored forms**, never `decimal.Parse` or
`DateOnly.Parse` at a call site. The refusing path, because a hand-written value
is exact: a value beyond the scale is a malformed chain rather than one to round
[D-W29].

**Output is in `ContractIdentity`'s order**, never file order. This is where the
total order 0.4 built gets its first caller, and a hand-written file is reordered
by whoever edits it.

**`WORKED_EXAMPLE.md` §2 and §5 are the acceptance test**, so the format is
chosen against something rather than against nothing. The test parses those two
tables and compares, rather than restating their numbers, so a revision to the
example fails here and names the value that moved.

Chains live in `synthetic/` at the repository root, not in `docs/`, which holds
documents, and not in the test project, which the Worker cannot reach.

Implement the fixtures registered against 0.6 in `FIXTURES.md`.

- **DoD**: the loader reads a synthetic chain from disk and produces the quotes
  and bars a simulated date offers, and a malformed one fails rather than
  loading partially.
- **DoD**: every check registered against 0.6 exists in its kind.
- **DoD**: every key the sections this checkpoint introduces carry is bound and
  verified. It introduces none, the loader taking text and resolving no location,
  so the obligation is discharged empty rather than skipped.

Reconciled at sign-off against what shipped. Three things were larger than the
scope above.

**Every value in a chain is a quoted string, including the numbers.** The detail
asked only that values parse through the stored forms. The mechanism turned out
to matter more than the rule: `guards.ps1` names, as the thing it cannot catch,
a JSON number bound into an untyped tree, and an unquoted number is a `double`
waiting to happen that no scan here would see. Quoting closes that by
construction, and an unquoted value is refused, so the format carries exactly the
text the parser reads.

**Malformed reports every reason rather than the first.** The detail asked that a
malformed chain fail whole. A hand-written file carries three typos as often as
one, so reporting them a run at a time turns a minute into an afternoon. The same
reasoning as a gate recording every failing reason rather than the first [D-W22],
applied to a different subject.

**A third document became machine-checked.** The acceptance test parses the two
tables in `WORKED_EXAMPLE.md` rather than restating their numbers, joining the
Store column and the fixture registry. The line is drawn at tables: symbol,
snapshot date, expiry and right are constants in the test, because they are
structural and stated once in prose, and no prose is parsed at all.

### 0.7 Append-only guards

Assertions that no `DELETE FROM` or `UPDATE` reaches a snapshot table [D-W8],
`decisions` or `candidates` [D-W3], `config_rows` [D-W26], or the migration
ledger [D-W32].

**A fixture, not a source guard.** 0.7's detail said these extend the guards 0.4
established, which predates the split it now contradicts. 0.4's criterion decides
it: a `guard` is for a check that must fail when the build is broken, and a
`fixture` is for one that needs a vocabulary and must read structure rather than
text. The measurement settles it beyond the criterion — `guards.ps1` strips raw
string literals by design and every SQL statement here lives in one, so a pattern
added to the script would match nothing in the tree by construction.
FX-NoDecimalOrderingInSql is the precedent end to end, with a table vocabulary in
place of a column one.

**The vocabulary is checked against the schema, not against the database.** Every
name in it must appear in a §4 schema block, which is enforceable today; the
reverse is a definition of done on the checkpoint that adds each table. Most
entries name tables that will not exist for several phases, and that is the point
rather than a mistake: the constraint lands before the tables it guards.
`DecimalColumns` carries exactly this contract already.

Also in 0.7: decide whether the source guards stay a text scan or move to a
Roslyn analyser. 0.4 raised it and deferred it here deliberately, because this
is the first checkpoint where the mechanisms can be compared concretely rather
than argued in the abstract. Answered by D-W33: they stay, and the comparison
that settled it is recorded there.

Register this checkpoint's check in `FIXTURES.md`. Nothing is registered against
0.7 today, and doing it is due when this checkpoint's prompt is written
[`FIXTURES.md` rule 2].

- **Test**: the check fails when a violating statement is introduced, verified
  by a test that adds and removes one.
- **DoD**: the check runs in CI, not only locally.
- **DoD**: every check registered against 0.7 exists in its kind.
- **DoD**: every key the sections this checkpoint introduces carry is bound and
  verified. It introduces none, so the obligation is discharged empty rather than
  skipped.
- **Constraint**: statements already in the tree carry the banned text and
  must not be reported. Three mechanisms exclude them, each a different
  class, which is why one does not suffice.

  **Statement form** excludes the trigger DDL. `BEFORE UPDATE ON
  config_rows` is not an `UPDATE ... SET`, so a check keyed on statement
  shape never sees it.

  **The table vocabulary** excludes statements against tables the rule does
  not cover, such as `SnapshotTests`' `UPDATE probe` on a scaffold table
  created inside the test.

  **Scan scope** excludes the tests that prove the triggers work.
  `ConfigWriteTests`' `UPDATE config_rows SET` and the two `DELETE FROM
  config_rows` are genuinely banned statements against a vocabulary table,
  and they exist to assert the triggers reject them. The rule governs what
  the lab does to its own store, and a test proving the guard works is not
  the lab doing it. So the check scans `src/`, which is the scope
  `FX-NoDecimalOrderingInSql` already has.

  None of the three is an exemption list. An exemption names a file to
  silence a failure; these name what the check is about, each fixed once
  with a reason rather than extended when something failed [0.4].

  Known limit: a banned statement written in `tests/` by mistake, rather
  than to prove a trigger, is not caught. Pinned as a test in the style of
  the alias miss rather than left in prose.

Reconciled at sign-off against what shipped. Three things were larger than the
scope above.

**Three citations were wrong, not one.** `decisions` and `candidates` were cited
to D-W3 for a property D-W3 did not state, exactly as `config_rows` was cited to
D-W8 until 0.5. Applying the method that found them to the vocabulary before any
code existed predicted a third: `schema_migrations` would have rested on §4.0's
prose. D-W3 gains the property, D-W32 states it for the ledger, and `CLAUDE.md`
§1 gains the method. That last is the part that outlives the checkpoint.

**The vocabulary needed a schema section that did not exist.** §4 documented
seventeen tables that do not exist and omitted one of the two that do, so §4.0
was written for `schema_migrations` and §4.1 gained the definition of "snapshot
tables", a phrase used in four documents and defined in none.

**The Roslyn measurement refuted the reason the guards were split.**
`guards.ps1` said a guard must fail even when the build does not; a probe showed
an analyser reports alongside a compile error, so the claim was false. The true
property is narrower — it reports when restore does not succeed — and the script
now says that instead [D-W33].

### 0.8 Configuration values for the open parameters

**Ten keys carry an `Unset` marker, not four.** `MaxRolls` and `MaxTrialDays`
[D-W14] and the divergence threshold and window [D-W20] are the four this section
used to name. `CONFIG_REFERENCE.md` also marks all six `Gate:` keys unset with
proposed values, and D-W22 to D-W25 each say "Phase 0.8 config". All ten are
0.8's.

**The seven `Policy:` keys are seeded too, and without them one invariant cannot
be exercised at all.** They carry no value and no `Unset` marker, so a strict
reading of "the unset keys" would leave them out — but D-W23 compares
`Gate:MaxDelta` against every policy band, and the predicate passes vacuously
against an empty band set. Their values come from `WORKED_EXAMPLE.md` §1 rather
than from invention. `Policy:Random:Seed` is chosen and reported as chosen.

**Provenance is judged per key, not per section.** `Policy:` was already split
that way and `Costs:` was not.
`Costs:CommissionPerContract` is seeded at the worked example's `0.65`, which
§4's fills, §6.3's ledger and FX-TrialCompleteIncludesAssignment's total all
depend on; its note says a real broker's rate replaces it by version + 1, which
is what the versioned store is for. `Costs:FillPoint` is seeded at `bid`, fixed
and not a tunable [D-W12], because a fixed value still has to be readable and a
`rows` key never written cannot be resolved as-of at all.

**What 0.8 deliberately does not set**, so the gap is not read as an omission:
`Costs:AssignmentFee`, which no document states and where a zero inferred from an
absent ledger line would be weaker than a stated number and invisible when wrong;
and the three `Risk:` fractions, which are the operator's risk appetite [D-W11],
an example's illustration of one account not being the operator setting one. Both
are carried obligations rather than gaps.

**Seeding is one transaction.** An invariant over two keys cannot be evaluated
while only one exists, so a loop over `Append` either fails on the first key or
passes vacuously until the last. Stated because that loop is the obvious
implementation and is wrong in a way that passes.

- **DoD**: values are recorded as config rows with a note explaining the choice,
  and appear in `CONFIG_REFERENCE.md` with their consumer **named**. Not
  verified: every consumer is Phase 2 or later, so the `Unverified` markers stay.
  A definition of done that reads as satisfiable and is not is 0.6's own failure.
- **DoD**: the cross-key invariants are enforced at config-write time, on every
  write rather than only on the seed, and an attempted insert violating either is
  refused with no row written [D-W23, D-W24]. A write leaving an invariant
  unevaluable is refused too [D-W34]. Implement the fixture registered against
  0.8 in `FIXTURES.md`.
- **DoD**: every check registered against 0.8 exists in its kind.
- **DoD**: every key the sections this checkpoint introduces carry is bound and
  verified. It introduces no new key, and the keys it sets are `rows`-classed and
  so never bind, so the obligation is discharged with that reason rather than
  empty.
- **DoD, the phase's**: this is the last checkpoint, so nothing after it will
  demonstrate the Phase 0 definition of done. Each item is run and its output
  recorded: `dotnet test` green, `migrate.ps1` clean from empty, CI green on a
  fresh clone, every `app`-classed key proven to bind, and no `rows`-classed key
  bound from `appsettings` [D-W27].
- **Note**: these values are expected to be revised. Because config rows are
  append-only and versioned, a revision inserts version + 1 and the old value
  stays readable, which is what lets a later behaviour change be explained.

Reconciled at sign-off against what shipped. Five things were larger than the
scope above, and the first two came out of the build rather than out of review.

**§3.5 was wrong about the delta bands.** It said the makers select inside the
same delta and expiry bands: true for expiry, false for delta, and
`WORKED_EXAMPLE.md` §1 has said 0.20 to 0.30 against 0.10 to 0.35 since v1.0.0.
Seeding the values a sentence describes is what found it, the same way building
the thing that enforces a rule found three wrong citations at 0.7. It now also
records the coupling the schema cannot: `Policy:Random:` carries no DTE keys
because the random maker reads the baseline's window, and 0.8 is the checkpoint
that makes a reader who would misread that absence exist.

**D-W23's open clause is settled and closed.** The detail did not name it. The
ceiling at 0.35 against 0.35 is argued from D-W4, a control drawing from a smaller
opportunity set than the gate admits making a difference partly permission rather
than judgement. The 0.10 floor is reported as inherited rather than argued,
because that argument does not reach it and no measurement 0.8 can make does
either.

**The `app`-classed reverse direction is a registered fixture now.** The
assertion is not new: it landed at 0.4 in a suite deliberately outside the
registry, because a phase definition of done was held not to be a fixture. It is
moved rather than copied into FX-EveryAppKeyBinds, so the phase's fourth item is
discoverable from `FIXTURES.md`. What was genuinely missing is the sentence in
`CONFIG_REFERENCE.md`: the paragraph declining the reverse check did not say
which class it declined it for, so it read as complete while covering only
`rows` keys, and has done since `Eodhd` bound at 0.2.

**`seed.ps1` ships beside `migrate.ps1`.** Seeding wraps nothing, so the case for
it was symmetry until it was measured: with `Storage__Path` unset the verb throws
from `StoreLocation` with the right words under a stack trace, where the script
refuses cleanly. Two steps of one setup sequence, and the second reporting the
identical mistake worse than the first is what the script fixes.

**The seed verb reports a refusal rather than raising one.** A refusal is a
designed outcome of the verb, being what happens when the store already holds a
value the entries contradict, and the messages name their decision and say no row
was written. A stack trace above them buries the sentence the operator needs.

---

## Carried obligations

Work deferred out of a checkpoint that a later phase must claim. An entry
leaves this list only when the phase that owns it has done it, never because it
has aged.

An obligation raised in the corpus rather than out of a build belongs here too,
and its Raised column carries the corpus version instead of a pull request. The
first such entry had been recorded in a document banner and a dated log entry for
nine minor versions without being carried here, which is what a register that
admits only one origin costs.

**A row points at another where one answer settles both, and they are never
merged.** Two rows raised for two reasons at two versions are the history, and
merging loses which question came from where. The first pair are the two Phase 4
rows: the grain a feasible set is stored at and how a set of gate reasons reaches
one column are one schema decision, and answering either alone constrains the
other silently. That is this corpus's pointer rule arriving in a register rather
than in prose, where it has until now applied to a fact stated twice; here it is
two questions with one answer, which fails the same way.

**The Owed at column names a checkpoint once the owning phase's detail exists,
and a phase otherwise.** Detail is written one phase ahead, so a row sits at
phase granularity for at most one phase, which is the rule `FIXTURES.md` already
applies to its own checkpoint column. A count taken over phase names alone
misses the rows that have moved on, which is how two readings of this table came
to differ. A count of this table is a count of rows. Rows sharing an Owed at
value are separate obligations, and a count of distinct values is not a count of
this table.

**Fifteen rows stand, five at checkpoint granularity and ten at phase**, read off
the table below at 3.3's sign-off. It stood at fourteen, seven and seven, at
3.2's; 3.3 closed its own four and raised five, so the total moved by one and the
split moved by two, which is why a count of rows and a count of granularities are
read separately rather than derived from each other.

| Owed at | Obligation | Raised |
|---|---|---|
| Phase 11 | Re-add `Microsoft.AspNetCore.OpenApi` against a version whose `Microsoft.OpenApi` dependency clears the audit. Removed at 0.1 rather than suppressing the advisory; the reason is in the Api project file. | PR #1 |
| 3.5 | Establish output-level determinism: a simulated run with a fixed clock produces byte-identical output across two invocations. 0.5 restated it as identical stored rows because no run existed to make. Compared as produced artefacts, never as a database file [D-W28]. | PR #4 |
| 3.5 | Decide what bars nondeterminism in SQL that is not a clock. Enumerating the bundled SQLite showed `random()` and `randomblob()` alongside the seven clock functions; they are outside FX-ClockIsNotADateSource by name but would break a byte-identical run just as surely. | PR #4 |
| 3.4 | Set `Costs:AssignmentFee`. No document states it, and zero inferred from an absent ledger line is weaker than a stated number and invisible when wrong. Phase 3's assignment path is the first thing that computes with it. | PR #7 |
| Phase 4 | Store one feasible set per name and date rather than one per decision. `candidates` is keyed on `decision_id`, so three makers acting on one set write it three times, while [D-W4] requires the three to be byte-identical and `FX-ThreeMakersSameFeasibleSet` asserts it. Storing once and referencing thrice makes it true by construction and divides the largest uncertain table by three. Raised while estimating store size over a ten-year lifetime, before the table exists. The grain is (symbol, date) and nothing carries it as a type; 2.5 declined to ship one because no maker exists to consume it, so Phase 4 models the set with `candidates` in front of it rather than inheriting a shape guessed three checkpoints early. `FX-ThreeMakersSameFeasibleSet` is also where the byte-level property is asserted, restated from 2.5's definition of done for want of a subject. **The reason-storage row raised at v1.32.0 is answered by whatever grain this settles.** | v1.17.0 |
| Phase 8 | Extract the market rules out of `SyntheticChainReader`, so one definition serves the synthetic reader and the vendor ingest. Refusing a negative bid, a negative ask and a crossed market are statements about what a market can be, not JSON concerns, and they sit as private statics on the reader, so a second producer of quotes can only duplicate them. Phase 8 is where that second producer arrives. **The crossed-quote coupling is discharged at 2.3**: the gate handles a crossed quote [D-W22, as amended], so that rule moved to the gate and left the loader, and what remains to extract is the two negative-price refusals. Not extracted at 0.8 because there is one caller and the second does not exist. | PR #9 |
| Phase 4 | Decide how a candidate's gate reasons are stored. `candidates.gate_reason` is a single nullable TEXT column and the domain type is a set in declared order [2.3], which FX-GateRecordsAllReasons at 2.5 asserts by requiring two reasons on one candidate. The options include a delimited list, which makes a reason unqueryable, and a row per reason, which changes the table's grain. Raised at 2.3 when the vocabulary was declared. **The grain this assumes is the one the v1.17.0 row decides.** | v1.32.0 |
| Phase 9 | Decide how configuration resolves for a simulated date that precedes the value being written. `SeedCommand` stamps `set_at` from the wall clock, so every gate bound resolves null for any simulated date before the seed ran, which is every date in a walk-forward over real history. [D-W26] requires resolution as of the simulated date and [D-W37] stops the evaluation rather than guessing, so the collision surfaces loudly at the first walk-forward rather than silently. The options include backdating the seed, which costs the audit trail its truthfulness, and resolving a registered run's configuration as of its pre-registration instant [D-W15], which keeps both rules intact. Raised at 2.3 while answering what an unresolvable bound does. | v1.32.0 |
| Phase 8 | Decide how a deliverable that is shares plus cash is recorded. The adjustment method in force gives a 4-for-3 split of an $80 option a deliverable "adjusted to 133 shares plus the cash value of the eliminated fractional share", strike unchanged; `contracts.deliverable_shares` is an integer and one of the five components of contract identity [1.5], and nothing in this corpus or its sources names cash in lieu. Owed at Phase 8 rather than 3.3 because no synthetic chain can express a corporate action at all, so the first deliverable of this shape arrives with vendor data, and the change is to a built structure with a migration cost that is no cheaper now. 3.3 must not assume a deliverable is wholly shares. Raised at 3.2. | v1.38.0 |
| Phase 5 | Decide whether cash earns, and at what rate. Nothing in this corpus names interest as a financial concept. The absence biases two of the three comparisons in opposite directions: the wheel holds cash securing its puts and the hold-cash floor holds cash outright, so both are understated by roughly the same amount and their comparison survives, while buy-and-hold holds no cash and is not understated at all, so the comparison the lab exists to make is biased against the wheel by whatever the rate is. A rate is an external source and choosing one is a modelling choice with its own argument. Owed at Phase 5, where the outcome metric and the controls' returns are computed. A control gap of the same kind as the dividend gap and in the same decision [D-W13]. Raised at 3.2. | v1.38.0 |
| Phase 4 | Resolve configuration in the projection rebuild as of the simulated date the run used, never as-now [D-W26]. Telling `closed_at_bound` from `closed_by_choice` means asking whether a bound had been reached, which reads `Trial:MaxRolls` and `Trial:MaxTrialDays`, and a rebuild reading current bounds would disagree with the run it is rebuilding while presenting the disagreement as a ledger defect. Not live at 3.3, where nothing writes `closed_by_choice` because no maker exists. Raised at 3.3 by building the rebuild. | v1.40.0 |
| 3.4 | Decide whether `Costs:AssignmentFee` can carry a non-zero figure without contradicting `WORKED_EXAMPLE.md`. §1 states the commission and states no assignment fee, and §6.3's assignment leg is exactly `-5,000.00` against a total of `498.05`, so that document's arithmetic assumes the fee is zero while `FX-TrialCompleteIncludesAssignment` asserts the total against the document. A stated figure therefore either changes §6.3 or breaks the fixture's source, which is the collision the PR #7 row could not see when it warned that a zero inferred from an absent ledger line is invisible when wrong. **The figure this constrains is the one that row sets.** Raised at 3.3, where the ledger the fee would appear in was built. | v1.40.0 |
| 3.4 | Decide whether a commission is its own ledger entry or is netted into the premium. [D-W12] requires per-contract commission and assignment fees explicit without saying where, and `WORKED_EXAMPLE.md` §6.3 nets them, writing "bid 0.95 less commission" as one leg of `+94.35`. A netted cost is not separately auditable, and a separate entry changes what the projection rebuilds from. Both `commission` and `assignment_fee` are already in the ledger's vocabulary [D-W48], so this settles what writes them rather than whether they can be written. Raised at 3.3. | v1.40.0 |
| Phase 4 | Verify the consumers of `Trial:MaxRolls` and `Trial:MaxTrialDays`, which 3.3 could not. `TrialBounds` reads both as of the simulated date and nothing in `src/` constructs it: the state machine is handed resolved bounds, and the component that would resolve them is the run loop. `CONFIG_REFERENCE.md` calls a consumer that cannot be verified once its checkpoint has landed a defect rather than a documentation gap, and 3.3 was that checkpoint, so both rows stay **Unverified** with the reason recorded rather than the column loosened to admit a type with no component behind it. Raised at 3.3. | v1.40.0 |
| Phase 4 | Decide which simulated date the trial bounds resolve as of. A trial spans many sessions and [D-W26] resolves configuration as of the simulated date, so nothing states whether an open trial is bound by the values in force at its open or by each session's, and `Trial:MaxRolls` changing mid-trial would move the bound under a position already taken. 3.3 built the machine taking bounds at construction, on `GateBounds`' resolve-once-per-evaluation shape, and invented no answer. **The rebuild row above asks the same as-of question from the other side.** Raised at 3.3. | v1.40.0 |

---

## Phase 1 — Chain store and point-in-time invariants

Build state: **complete**. 1.1 to 1.5 built and signed off. On synthetic chains;
no vendor data until Phase 8. Delivered the market-data schema, the as-of read
paths over it, membership as state, chain ingest, and the corporate-action mint
with its lineage walk.

### 1.1 The market-data schema
The six tables of §4.1 with the observation stamp in the key [D-W8].

`AppendOnlyTables` gains nothing: 0.7 declared all six forward. What 1.1 owes is the
other direction, the definition of done that every table this checkpoint adds is in
the vocabulary, which was unmeetable at 0.7 because none of them existed.
`DecimalColumns` is the vocabulary that gains entries, sixteen of them.
- **Test** FX-SnapshotNeverRewritten: a correction appends and both rows survive
  with their own stamps.
- **DoD**: migrating from empty produces the schema, and both guards report the
  new tables and columns rather than passing over them.
- Discharges the SQL alias obligation. Both detectors are touched here and the
  tables they will scan first appear here.

Reconciled at sign-off against what shipped. Five things were larger than the scope
above, and three of them were found by measuring something the detail assumed.

**The schema gained what the document did not specify.** Twelve triggers refusing
`UPDATE` and `DELETE`, three indexes, a `CHECK` on `right`, a uniqueness constraint,
and two foreign keys. Only the tables were in the detail. The triggers exist because
the source detector reads `src/` and cannot see a writer at a `sqlite3` prompt, which
is the same argument the `config_rows` triggers already rest on; the foreign keys
were not asked for by any document and are raised for that reason.

**§2's identity claim is false, and 1.1 is where it had to be settled or recorded.**
An adjusted series can share underlying, expiry, right and strike with a standard
contract and differ only in the deliverable. Checked against Fidelity, Schwab and
OCC's own symbology memo rather than reasoned about, then demonstrated against the
built schema: two contracts, one tuple. §2 carries a banner and the constraint went
on the deliverable, which is a floor under the decision rather than an answer to it.

**The multiplier and the deliverable were one column.** Splitting them was 1.1's;
deciding which one committed capital uses is Phase 3's and was deliberately not
settled here. The arithmetic favours the deliverable, and a reverse split may behave
differently, which is why it is owed against OCC's memos rather than closed on one
worked case.

**Measuring the decimal vocabulary's false-positive surface found a false negative.**
The detector filtered `LAST` as an order keyword before consulting the vocabulary, so
`ORDER BY last` would have been dropped the moment `last` became a column here. The
checkpoint asked for one measurement and the other defect was what the measurement
turned up.

**The alias convention's first run flagged a legitimate statement**, and would have
flagged thirteen once migration 3 landed: `BEFORE UPDATE ON config_rows BEGIN` reads
as a table followed by an alias. A second defect in the same detector meant the
second table of every join went unscanned. Both were found by running the convention
against real statements rather than synthetic ones.

### 1.2 As-of reads
Every read serving a simulated date takes the row for the date it asks about, at
the greatest `observed_at` at or before the instant it is asked as of. Two filters,
not one: which session the data describes, and when it was observed. A correction
is a second row on the same date with a later stamp, so a read as of before the
correction still returns what was believed then.

No tie is possible on the second axis, because `observed_at` is in the primary
key, so two observations of one row at one instant cannot both exist. `config_rows`
needs `version` to break that tie and these tables do not.

No check is registered against 1.2; its tests land as unregistered suites, and this
sentence is what rule 2 asks for in place of discharging on nothing.

Corrections this checkpoint carries: §4.2's membership schema, which D-W35 settled
and which blocked 1.3, fixed here because 1.2 is the checkpoint open when the fix
was written and 1.3 cannot be prompted without it.
- **DoD**: a correction recorded after a simulated date is invisible to a read at
  that date and visible after it.
- **DoD**: no read serving a simulated date returns current data, checked as the
  configuration surfaces already are.
- `ResolveAtOrBefore` gains the optional transaction its remarks predict, if 1.4
  needs it. Report which.

Reconciled at sign-off against what shipped. The read surface is one type,
`AsOfMarketData`, and no current-value market-data type exists at all, which is
stronger than the configuration split rather than half of it: configuration has an
operational consumer for current values [D-W26] and market data has none, so a
current-reading type would be a second path with no consumer to justify it. The
strongest form of "cannot read current" is that no current-reading type exists to
cast to. The shape check asserts the as-of parameter by name and type on every
value-returning member, because a two-axis read can take the session date and
still leak the latest observation, which a check asking only for a date would
pass.

**1.4 was checked rather than predicted, and `ResolveAtOrBefore` gains nothing.**
Its detail persists what the loader yields and verifies by reading back after
commit, so no as-of read happens inside a write transaction. The remark that
predicted otherwise had also named the wrong ender: membership resolution is not
a config read and would never pass through `ConfigRowQuery`. Corrected at the
site.

**The alias detector was blind to every parenthesised expression**, measured
before the chain read was written: the source arm was an identifier class and
cannot end at `)`, so `MAX(observed_at) AS latest` was invisible, and the
aggregate form is exactly what a naive chain read writes. Widened, swept over the
tree, zero flags. What keeps a CTE clean is the alias group rather than an
exemption: the token after `AS` in a CTE header is `(`, which no identifier can
match. The chain read is written as a CTE with declared column names, the
convention's own shape.

**The join 1.1 deferred is settled by measurement.** `contract_quotes` reaches
identity through `contracts` on `contract_id` filtered by symbol, and
`EXPLAIN QUERY PLAN` shows the uniqueness constraint's own index serving the
lookup, so there is no migration 4 for indexing. Identity order is imposed in C#
on the parsed identities, because the stored decimal form does not sort and the
convention refuses ordering a decimal column in SQL; the test pins the
9-versus-10 strike pair that text ordering gets backwards.

**One finding left for 1.4, recorded in its detail rather than here**: migration
3's `underlying_bars` cannot hold the bars the worked example supplies.

### 1.3 Watchlist membership as state
Append-only and versioned [D-W35]. A departure appends; a re-entry appends again.
Each version records one transition, `joined` or `left` effective on a date, not an
interval: §4.2 states the shape and why an interval per version cannot answer the
membership question.

Corrections this checkpoint carries: the dividend obligation, raised from a review
of what the wheel model omits, and 1.4's migration ordinal, which this checkpoint's
own migration would have made false. Neither was caused by 1.3; it is the
checkpoint open when they were found.
- **Test** FX-PitMembershipExcludesLaterJoiner.
- **Test**: a name that left and returned resolves correctly at a date in each of
  the three intervals.
- **DoD**: no query resolves membership from the latest row alone.

Reconciled at sign-off against what shipped. The read is its own type,
`AsOfMembership`, not a member of the market-data surface: that type documents
itself as the only read surface over the snapshot tables, membership corrects by
version rather than by re-observation, and the market-data one-surface guarantee
rests on a premise never argued for membership, which is probably false for it
once Phase 8's ingest wants to know what to fetch. Its shape suite and
no-current tripwire are mirrored copies, and the tripwire's message says a
current surface arrives as a decision that amends it.

**The governing axis is the greatest (`effective_on`, `version`), and the
choice was measured rather than asserted.** The two candidate axes disagree
only when a correction carries an earlier effective date than a later genuine
transition; flipping the window ordering to version alone fails exactly the
divergence test and passes the other five.

**Migration 4 carries three triggers where the detail implied two.** The third
is the monotonic stamp per symbol: version ordering constrains versions, not
visibility, so a backdated stamp would change what was believed at a past
instant after the fact. It landed inside the migration because an applied
migration's SQL is frozen, so deferring the decision would have cost migration
5.

**The upgrade test is the first from-previous-schema migration test in the
suite.** 1.1's prompt asked for one and the suite covered empty and
nothing-pending only, which nothing noticed because every store in the tree was
either empty or current until this checkpoint created a real gap to migrate
across.

### 1.4 Chain ingest
0.6 built a loader producing objects and nothing persists them. 1.4 does.

A migration first, before any ingest code: `underlying_bars` makes `open`, `high`, `low` and
`adj_close` NOT NULL while `UnderlyingBar` declares them optional and
WORKED_EXAMPLE §5 supplies only dates and closes, so the chain this
checkpoint's own DoD loads cannot be persisted into the table as it
stands. Relax the columns the record makes optional, enumerating from the
record rather than from this sentence, and correct §4.1 to match. Found
at 1.2 by reading the migration against the record; 1.1's claim that "a
chain the loader accepts is a chain this schema can hold" was verified
against ContractQuote only.

What a second ingest does is settled. Re-loading a chain with the same
observation instant is refused by the primary keys and the refusal says so;
recording the same chain with a new instant appends alongside the old, which
is the correction model arriving at the ingest level [D-W8]. Both are tested.

No Worker verb. Ingest is a Core writer; tests are its only caller until a
phase needs an operator entry point, and a verb nothing calls is speculation.
The first consumer with an operational need is Phase 8's vendor ingest.
- **DoD**: the worked example's chain loads into the store and reads back
  identical, against the same oracle 0.6's fixture uses.
- Discharges D-W29's write-side seam. This writes the first real decimal columns,
  which the obligation names as its trigger.

Reconciled at sign-off against what shipped. Migration 5 relaxed five columns
where the paragraph above names four: `UnderlyingBar` also makes `volume`
optional, which is exactly what "enumerating from the record rather than from
this sentence" was for. A standing record-to-schema test keeps the enumeration
a property rather than authoring-time care, comparing the table's pragma
nullability against the record's optional properties, so a record change names
the migration owed.

**The rebuild's triggers are demonstrated, not assumed.** DROP TABLE takes
them with it and a forgotten recreation passes every schema check, so the
refusals are asserted against the rebuilt table on a seeded row, and a
hand-populated schema-4 store is carried through the copy. The alias detector
was checked against the rebuild's grammar before the SQL was written; the
clause anchor never reaches ALTER TABLE or DROP TABLE, so nothing widened.

**The writer lives beside the reader**, `ChainWriter` in `Core/MarketData` on
the membership precedent, and the seam closed the write-side obligation with
its teeth stated honestly: the refusing decimal path, exclusivity held by
review rather than the type system [D-W33]. An upsert is impossible by
construction, the append-only trigger refusing the update half, so
find-or-create is DO NOTHING with a follow-up lookup that refuses rather than
guesses if the four-tuple ever stops being unique, which is 1.5's question and
§2's banner. The transaction's rollback is observed rather than assumed: a
collision after the header insert leaves no header row.

**The oracle's extraction premise was half true.** The parser has been shared
since 0.6; the header vocabularies, structural constants and chain-file load
were the duplicated half, and they moved to a shared oracle helper in a pure
refactor before the persistence fixture consumed them.

### 1.5 Corporate actions and the predecessor link
A split or special dividend mints a new contract identity with a recorded
predecessor rather than editing the existing row [§2].
- The adjusted-strike question is settled by D-W36: terms are transcribed from
  what the adjusting authority states and never computed, so the non-terminating
  case cannot arise. The obligation's dilemma, rounding a value inside identity
  against carrying the ratio, is dissolved rather than decided, and the refusing
  decimal path is the tripwire that keeps it dissolved.
- **DoD**: an adjusted contract is a new identity, its predecessor is recorded,
  and a historical join across the split resolves both.

Reconciled at sign-off against what shipped. D-W36 dissolved the dilemma the
detail was written around, and the refusing decimal path is exercised inside
the minting writer: the stated strike runs through it on the way to identity,
so a derivation that produced a non-terminating value could not be stored.

**The fifth identity component touched six sites where three were known.** The
comparer and equality; ChainWriter's find-or-create, made explicit and exact,
its multi-match refusal turning unreachable and coming out with the banner it
cited; the equality and ordering tests, where the old shared-identity test
inverted exactly as its own remark predicted; the as-of quote read, which now
reads the deliverable because defaulting it would mint every stored contract
as standard; the `Contract` record, which lost its copy of a fact identity now
carries; and `ToString`, which renders the fifth component so two identities
differing only in deliverable cannot stringify identically.

**The mint is atomic both ways, observed.** The unchanged-tuple refusal writes
nothing; a successor collision after the event insert rolls the event row back
with it; the predecessor reads back byte-identical by row comparison.
`corporate_actions` got its first writer three checkpoints after both
vocabularies learned its columns.

**The lineage read is timeless, its reasoning at the type.** Contracts carry
no observation axis, so a by-name `asOf` member would claim a filter the
schema cannot honour. The 1.1 recursive CTE became production in the shape the
pin proved, the alias convention's dated revisit closed in all three passages,
and resolution was never needed.

**Left for Phase 3, recorded at its dividend obligation**: the corporate-action
kind vocabulary is one entry with no CHECK on the table and no document
enumerating the values; the fuller vocabulary and the CHECK decision must not
ride a checkpoint silently as a rebuild migration.

Detail for Phase 2 is authored when Phase 1 signs off.

---

## Phase 2 — Candidate generator and risk gate

Build state: **complete**. 2.1 to 2.5 built and signed off. Delivers one
feasible set per name and date, produced by enumeration and filtered by a
gate that sits inside the generator [D-W10], with every rejection reason
recorded [D-W5]. On synthetic chains; no vendor data until Phase 8. Nothing
persists a candidate or its reasons, which is Phase 4's.

All three of this phase's carried obligations are preconditions rather than
work items, and that shapes the order. The worked example must be
reconciled before any fixture reads it, the crossed-quote question decides
what test data can express before the liquidity constraints are built, and
the risk fractions must exist before a capital cap can be tested against
anything.

All three are discharged: the worked example at 2.1, the crossed quote at
2.3, and the risk keys at 2.4, which turned out to be four keys rather than
three because every cap divides by an equity figure nothing held.

### 2.1 The worked example, reconciled
Not code. Seven registered fixtures read this document's conclusions, and
its own chain contradicts the constraints this phase builds: the 45.00
strike fails the twelve percent spread cap at 18.18 percent of mid, so the
three-candidate feasible set it teaches renders as two, and the 47.50
passes by three hundredths of a point.
- Rewrite the chain deliberately, so the example teaches the gate as well as
  the wheel: at least one enumerated contract fails a named constraint by an
  obvious margin, and the surviving feasible set is the three the example's
  later sections depend on.
- Registers no fixtures: the two that pin this document are registered at
  0.6 and 1.4, which is the coverage.
- **DoD**: FX-WorkedExampleChainLoads and FX-WorkedExampleChainPersists pass
  against the revised tables, which is what makes the document the oracle
  rather than a description. A revision that breaks them has changed
  something the store's own tests were pinning.
- Discharges the reconciliation obligation raised at v1.6.0.

Reconciled at sign-off against what shipped. Three things the detail did not
anticipate, and one record that had run ahead of the disk.

**The rewrite was smaller than "rewrite the chain" implies, and structurally
so.** Every figure from §4 onward derives from a bid or a close, established
by reading each section in full rather than by searching for a column name,
so the revision is confined to asks and added candidates. No bid moved,
which is why nothing downstream of the fill moved and the fixtures pinning
§5 never came into it. The three surviving strikes' spreads went to 6.12,
7.02 and 6.45 percent of mid against a twelve percent cap, from 18.18 for
the strike that failed and three hundredths of margin for the one that
passed.

**The premium floor equals a frozen bid, which bounds what this example can
demonstrate.** 45.00's bid is 0.30 and the floor is 0.30, and the bid could
not move. A candidate below 45.00 that fails the spread cap and nothing else
must therefore post exactly 0.30 and, once its spread is wide enough to fail
by an obvious margin, carries a mid above 45.00's. The 42.50 is D-W22's
untransactable quote by construction rather than by choice, so §3 states it
as the case the cap exists for. Two rows now pass the floor with no margin
at all, and no revision that leaves bids alone can widen that.

**A comment cited a decision as leaving open what it settled.**
`ContractQuote.cs` deferred the magnitude question to Phase 2 citing D-W23,
which had said absolute delta from the day it was written; the synthetic
chain's delta comment carried the same deferral uncited. Both corrected,
comment only. This is the fourth instance of the citation pattern in this
corpus and the first of its inverse, the earlier three having cited a
decision for a property it did not state, and the same method found it:
reading the decision because something rested on it.

**The zero-registration sentence rule 2 requires was recorded before it was
written.** v1.28.0's entry said 2.1's detail states that it registers no
fixtures; the detail did not say it. The sentence is in the detail above,
and the record is true a version after it was made.

### 2.2 Enumeration and membership
What a candidate is: a contract sellable on a name that was a watchlist
member at the simulated date [D-W9], read through 1.3's as-of membership,
given the position's state.

Three things that sentence leaves open, each a judgement rather than a
transcription.

**A candidate carries the quote and no economics.** §4.3's `candidates` row
carries `contracts_qty`, `committed_capital`, `credit` and `feature_json`,
and 2.2 declines all four. None of 2.3's constraint families needs them: the
spread cap, the premium floor and the delta ceiling read the quote and the
expiry window reads a date. Only 2.4's caps need committed capital, and the
quantity that computes it is the open Phase 3 obligation, so building the
economics here means choosing between the multiplier and the deliverable at
the checkpoint with no reason to, three checkpoints before the obligation
that settles it. The type is not the `candidates` row, which is Phase 4's;
nothing persists at 2.2.

**The simulated date is used on both axes, and that is a choice.** A read
takes a snapshot or effective date and an as-of instant, and 1.2 made them
independent deliberately. On a simulated date the generator wants the chain
for that date as known at that date, so one date reaches four parameters
across two reads. It is stated at the call sites rather than left to one
variable landing in two parameters unremarked, because the two axes exist
because they can differ: collapsing them is correct for a simulated run and
would be wrong for a backfill, and the next reader should not have to work
out which.

**Enumeration filters on nothing but position state and membership.** A deep
in-the-money put is sellable and will be rejected by every constraint.
Enumerating it anyway is what makes the gate's effect auditable [D-W5,
D-W10], and §3 of the worked example demonstrates it: seven strikes
enumerated, three feasible, and the four rejections are the lesson. A
generator that pre-filters produces a smaller enumerated set and a smaller
record of what the gate did, which is the property Phase 4's decision record
exists to hold. Nothing about basis or moneyness happens at 2.2, which is
why the gross-basis constraint on a call strike is registered against 2.4.

Cash sells puts and shares sell calls, which is the wheel [D-W16, D-W19]. A
short leg enumerates nothing: rolling is permitted and bounded [D-W14], but
no document states which contracts a roll enumerates, the bounds are Phase
3's, and enumerating a guess would put an unrecorded rule into the decision
path.
- **Test**: a non-member symbol enumerates nothing, and the worked example's
  chain enumerates exactly the strikes its own tables state.
- **DoD**: enumeration is a pure function of the chain, the membership
  answer and the position state, so the same inputs enumerate the same
  candidates in the same order [D-W4].
- **DoD**: the position state reaches the filter, shown on a chain carrying
  both rights. Every chain in the repository is puts, so a generator ignoring
  its state argument would pass every other check this checkpoint has.

Reconciled at sign-off against what shipped. Three things were larger than the
scope above. Two came out of review and one out of the build, and the
difference matters: review found what the detail had not thought to ask for,
and the build found a rule this corpus had been unable to follow.

**The per-symbol read became one ranking, and measuring it chose the form.**
The detail asked only that the gate be able to ask about one name. Review
required the two members be unable to disagree structurally rather than by
agreeing across chosen cases, which is the distinction between a property and
evidence for one. The build then found that the obvious way to hold one text
constant, an `OR` on the parameter's nullness, costs the seek: `EXPLAIN QUERY
PLAN` reports a scan where the direct predicate reports a search on the
symbol, and a scan per call is most of what a per-symbol read exists to avoid.
So the ranking is stated once and only the predicate varies. The measurement
changed the answer, which is why it was run rather than reasoned about.

**The two-right chain closed a gap every other check would have missed.**
Every chain in this repository is puts, so the four position states had one
tested and three merely reported. An enumerator ignoring its state argument
would have passed the registered fixtures, the worked-example oracle and the
determinism suite alike. Review named the gap; the build answered it by
reintroducing the defect, and five of the ten cases fail against a generator
returning puts unconditionally, four of four against a membership read that
always answers yes. The rolling states are asserted empty deliberately, so the
day Phase 3 gives rolling a rule the suite says that 2.2 assumed otherwise.

**The fixture-naming rule was half wrong, and this detail is the second
incident.** The build reported that `CLAUDE.md` §5 forbade naming fixtures in
a prompt as well as in checkpoint detail, and that every prompt in the project
named them anyway, so the rule was followed in neither half. The prompt half
was the wrong half: a spent prompt is frozen and records what was asked, where
a detail is read for years and gains fixtures after it is written. The rule is
now narrowed to detail, and this checkpoint's own Test line is the evidence
for it, having named one of its two fixtures and gone incomplete when the
second was registered. The remaining live Test lines describe what they test;
the built ones keep their names as records of what was built.

### 2.3 The contract constraints
The four families of [D-W22] to [D-W25]: liquidity as a spread cap and a
premium floor, the delta ceiling, the expiry window, and earnings
clearance.

**Earnings clearance is three builds and not one constraint.**
`earnings_calendar` has existed since migration 3 with nothing reading it and
nothing writing it, and a synthetic chain is a symbol, its bars and its
quotes, so no fixture can put a report date in a store. The constraint needs
a format that can express a report date, a writer, and an as-of read before
it is testable at all. Kept here rather than split out, because the registry
puts its check at this checkpoint and the constraint is the reason the table
exists.

**The reason vocabulary is declared here**, because every check at this
checkpoint records a reason and 2.5 asserts that a candidate failing two
constraints carries both. A candidate therefore carries a set of reasons
rather than one, and that shape is decided here even though it is asserted
at 2.5.
- **Test**: each contract constraint rejects a candidate that breaches it and
  admits one that does not.
- Settles the crossed-quote obligation. The gate handles a crossed quote and
  records its own reason for it [D-W22], so 0.6's loader stops refusing one
  and the format can express the case. The loader was the wrong venue: Phase
  8's ingest reaches the store without passing it, so a refusal there would
  be absent exactly when vendor data arrives.
- **DoD**: every constraint reads its bound from configuration as-of the
  simulated date [D-W26], never from a constant. An unresolvable bound stops
  the evaluation naming the key and the date [D-W37], and bounds resolve once
  per evaluation rather than once per candidate.

Reconciled at sign-off against what shipped. Three things were larger than the
scope above, and two of them are decisions this checkpoint could not have been
built without.

**The gate could not reject a crossed quote on any ground D-W22 stated.** That
decision gave two, a spread above the cap and a bid below the floor, and a
crossed quote is neither: its spread is negative and its bid is high. Rejecting
on it would have been this corpus's citation pattern for the sixth time and the
first instance created rather than inherited, the earlier five having been found
by building the thing that rested on the citation. D-W22 gains the ground before
the code cites it, and the reason is its own rather than the spread cap's,
because a negative spread is not a spread above a cap and the audit trail should
not say it was. `FX-CrossedQuoteRejected` is registered here, which the registry
did not carry when this detail was written.

**Two boundaries turned out to be unstated, and one rested on a convention the
gate does not use.** D-W25 said the buffer was on both sides and not whether its
edge was inside; D-W24 said "outside `Gate:MinDte` to `Gate:MaxDte`", which
states an edge only through the convention that a range includes its endpoints.
Both are amended, and D-W25 carries the note that the gate's comparisons are
deliberately not uniform because the quantities differ, so each decision states
its own boundary rather than one convention governing all of them. Four
decisions were touched in total where the detail anticipated none.

**The mutation method needed a correction of its own.** 2.2 established that a
suite observed to pass is not a suite shown able to fail. 2.3 found that a
mutation confined to one site is not a mutation of the behaviour: defaulting
either half of the bound resolution passed everything, because each left the
other still raising, and only defeating both showed the two tests that assert
D-W37. The same shape appeared four times this checkpoint in searches rather
than mutations, and both halves are carried in the archive.

Seven fixtures landed where the registry carried five, and
`FX-MalformedChainFailsWhole` lost an assertion and gained its inverse, since
what the loader enforces is smaller than it was.

### 2.4 The portfolio constraints
The three caps of [D-W11], and the gross-basis rule of [D-W19] binding an
admissible call strike.

**Equity does not exist and every cap divides by it.** No key, no column and
no table holds an account value: `WORKED_EXAMPLE.md` §1 states 100,000 in
prose and nothing reads it. It is a configuration key rather than a derived
figure, and D-W11's own rationale is the argument, since a denominator
computed from the run's own state moves with the run and a drawdown would
loosen every cap at the moment it should bind. So the obligation is four
keys rather than three.
- Sets the four `Risk:` keys, discharging that obligation. They are the
  operator's [D-W11], so record what each value means rather than choosing
  one and moving on, and say per key whether it is transcribed or chosen.
- **Test**: a candidate whose committed capital exceeds the per-name headroom
  is rejected, and a covered-call strike below gross basis is not admissible.
- **Test**: each portfolio cap rejects at non-zero exposure. A cap tested only
  against an empty portfolio passes whether or not it works, which is 1.1's
  empty-table shape.
- **DoD**: committed capital is computed in one place, which is what makes the
  Phase 3 metric question a one-site change. That question is still open, so
  state which quantity this checkpoint reads and why.
- **DoD**: every cap reads its bound as of the simulated date [D-W26], and an
  unresolvable one stops the evaluation naming the key and the date [D-W37].

Reconciled at sign-off against what shipped. Five things were larger than the
scope above, the first of them changed what the obligation was, and the last
arrived in review.

**The fractions are transcribed, not chosen, and saying so corrects the
framing this detail was written under.** §1 states equity of 100,000, a
per-name cap of 25 percent and a total cap of 60 percent, and derives §3's
5,100.00 headroom from them. Only the simultaneous-assignment fraction is a
choice, and its reason is arithmetic rather than appetite: a cash-secured
put's committed capital is its assignment exposure, so a lower value makes
the total cap unreachable and a higher one never binds. 0.8's argument for
leaving these keys, that an example illustrating one account is not the
operator setting one, is about who decides and does not stop the decided
values coinciding with the example's. Presenting transcription as choice is
0.8's distinction inverted.

**The DoD asking for committed capital "as the store records it" could not be
met and was replaced rather than waived.** Nothing persists at 2.4:
`candidates.committed_capital` is §4.3's column and Phase 4's to write. What
the clause was protecting is that a cap and a stored figure cannot disagree,
and one computing site delivers that in the only form available now, since
Phase 4 will persist this figure rather than a second one.

**A cap whose bound is never reached is the second kind of vacuity, and §3
alone cannot see it.** §1 derives two headrooms and only the per-name one
reaches §3, so a total cap wired to the wrong exposure, or not wired at all,
reproduces §3's verdicts exactly. Both headrooms are asserted through the
functions the constraint compares against, which is why those are public.
The first kind, a cap tested at zero exposure, is the Test line above; this
is its mirror and neither implies the other.

**The two capital caps cannot be told apart by a rejection.** Both fractions
are 0.60 and assignment exposure never exceeds committed capital on a book
this lab can hold, so a candidate breaching one breaches the other, and a
constraint reading the wrong fraction passed every test until two assertions
were added at a configuration the store does not hold and could. That is
2.3's mutation lesson one level up: there a mutation confined to one site
left another site raising, here a value equal to its neighbour left a field
unreadable from any verdict, which is the third and last cause a passing
mutation has.

**Review moved that condition to where it is read, and the assertion to where
it is found.** The indistinguishability was recorded in the code and the
changelog, which reaches a reader of the code; an operator revising
`Risk:SimultaneousAssignmentLimitFraction` reads `CONFIG_REFERENCE.md`, whose
Notes now state it as a condition of the equal fractions rather than a fact
about the caps. The assertion telling the two constraints apart moved out of
an unregistered suite into `FX-AssignmentStressRejects`, since it is the only
one that does and belongs where the registry points.

D-W19 gains its boundary, a strike exactly at gross basis being admissible,
and `WORKED_EXAMPLE.md` §3's per-name headroom citation moves from D-W10 to
D-W11. D-W22, D-W24 and D-W25 gain the amendment stamps 2.3 did not write,
so the register shows four decisions moving where it showed one; that this
checkpoint's own plan cited D-W25 as the precedent for stamping, D-W25 being
one of the three unstamped, is the citation pattern created rather than
inherited and caught inside the checkpoint that made it.

One obligation is raised beside the one discharged: what a covered call
commits, owed at Phase 3. Twelve becomes thirteen.

### 2.5 The feasible set
Assembly and ordering, and the record of what the gate refused.
- **Test**: a candidate failing two constraints carries both, which is why
  the gate evaluates every constraint rather than short-circuiting.
- **DoD**: the same inputs produce the same sequence, compared as an ordered
  sequence of identities and reasons, and the ordering is the identity total
  order [D-W4]. The byte-level property belongs to the checkpoint that
  persists the set, and there is nothing here to compare bytes of.
- The order is inherited rather than imposed here. The chain read has sorted
  on the identity total order since 1.2 and gained its fifth component at 1.5
  without changing, so the gate depends on that sort and asserts the result;
  a second sort would be a second statement of one guarantee.
- Carries `SYSTEM_DESIGN.md` §3.3 and §3.4's build-state markers, which
  `CLAUDE.md` §5 requires of every section describing a component and which
  v1.28.0's sweep left because neither was built then. §3.3 is the candidate
  generator, built across 2.2 and 2.3; §3.4 is the risk gate, whose contract
  family landed at 2.3 and whose portfolio family landed at 2.4. They land
  here rather than at 2.4 because §3.4 is complete only once the feasible set
  assembles, and a marker written a checkpoint early would need amending a
  checkpoint later. Raised at 2.4.

Reconciled at sign-off against what shipped. Three things were larger than the
scope above, and the first is a defect the definition of done found.

**`GatedCandidate`'s equality compared its reasons by reference.** A record
compares each member with the default comparer, which for a collection is
reference equality, so two candidates with the same contract and the same
reasons in the same order were unequal whenever the lists were separate
instances, which is every time the gate runs twice. That is the question D-W4
asks and the type answered it wrong. Equality now compares by sequence, order
included, because two arrangements of one set are not one verdict. It was
found by asserting that an evaluation repeats, and Phase 4's
FX-ThreeMakersSameFeasibleSet is where it would otherwise have surfaced, as a
difference between three makers that did not exist.

**Ordering needed a chain no fixture in this repository had.** Identity orders
on expiry before strike and every chain here is one expiry, so nothing could
tell an identity comparison from a strike comparison. The ordering suite uses
two expiries and four strikes supplied scrambled. Removing the chain read's
sort then failed exactly one test, the read's own, because the store returned
identity order anyway on that chain; which access path produced it was not
measured. That is the right division rather than a gap: the read owns the sort
and owns the test that holds it, and the gate asserts the property it inherits.

**Three build-state markers had gone stale and the sweep for two found them.**
§3.2 said the earnings calendar had its table and no writer, which 2.3
falsified; §8 said four keys were unset, which has been one since 2.4; and
`ORIENTATION.md` described what had shipped as Phase 1's store. Each
correction states what is not built beside what is, since a marker that only
accumulates reads as further along than the work.

The definition of done was restated before the code, and Phase 4's two
obligation rows now point at each other.

---

## Phase 3 — Thin slice: one full wheel turn

Build state: **partly built**. 3.1, 3.2 and 3.3 built and signed off: the
mechanics settled as decisions, the completeness pass run, and the state machine,
the ledger and its projections built against four tables. 3.4 and 3.5 not
started, so nothing prices a fill and no run is byte-identical yet. Delivers
one trial from cash to cash: a put sold, assignment or expiry, shares held, calls
written, called away or closed at the roll bound, with every cash movement in the
ledger and the trial and position rebuildable from it. On synthetic chains; no
vendor data until Phase 8.

Several of the obligations owed here determine what the state machine does
rather than sitting inside it, and those are settled at 3.1 before any
transition is written, on the ordering Phase 1 and Phase 2 both used: a
precondition answered late is a schema or a transition built twice. One
settled into a code correction instead, which is what it looks like when a
precondition turns out to have been answered already and only the
implementation lags.

### 3.1 The mechanics, settled before the machine
Not code. Each answer becomes a decision citing its source, and each source
is OCC's own rules rather than a secondary description. This project
reasoned twice from a secondary source about contract adjustment and was
wrong both times, which is why the sourcing is a requirement rather than a
preference.
- Exercise by exception and its in-the-money threshold at expiry.
- When assignment is known to the account against when it occurred, which is
  the point-in-time discipline applied to the account itself.
- When cash from an assignment or a call-away is usable again under T+1.
- The early-assignment model around ex-dividend, which `VALIDITY.md` already
  names as modelled by rule.
- Dividend entitlement timing, and whether a dividend enters
  `ledger_entries` and the buy-and-hold control [D-W13].
- Which quantity committed capital uses, the contract multiplier or the
  deliverable, from OCC's adjustment memos.
- What a covered call commits, given [D-W17] fixes committed capital at open.
- **DoD**: every transition 3.3 will write cites a decision settled here, and
  no mechanic is encoded from recollection.
- Registers no fixtures and says so, per rule 2.

Reconciled at sign-off against what shipped. Seven questions were listed and
seven were answered, by six new decisions and one amendment: D-W38 to D-W43, and
[D-W17] corrected. Four obligations closed, three were raised, six fixtures were
registered against later checkpoints, and no `.cs` file was touched.

**The sourcing requirement changed what the decisions could claim, which is what
it was for.** A market rule governs the clearing layer and the account layer is
convention, so a decision spanning both cites two authorities or states that it
has one. Four of the seven needed that disclosure. One had no authority at any
layer, because the act it models is a holder's choice. And one rests on a rule
that deliberately omits its own method: SR-OCC-95-16 removed random selection
from Rule 803 in 1995 and put the assignment procedures outside the rule, so the
gap in what could be cited was designed rather than missed.

**One mechanic was answered by amending a decision rather than by adding one.**
[D-W17]'s third paragraph had said committed capital reads the deliverable and
located it in `contracts.multiplier`, which is neither the right quantity nor the
right column. It entered at 0.4 and stood for fifteen checkpoints, produced the
obligation that reasoned from it, and shaped `CommittedCapital.For` at 2.4. The
filing that settled it is the one that retired the method the obligation's
arithmetic describes, and the footnote which appeared to settle it the other way
sits in that filing's Background. A quotation is not evidence until its position
in the document is established.

**Two schema consequences fell out of decisions rather than out of rules.**
`ledger_entries` gained `known_on` here, deduced from [D-W35] because a
projection cannot carry what its source lacks, and the table is 3.3's so the
schema is right before it is built rather than changed after. The second did not
resolve: [D-W40] settles to "the first business day after" and no business-day,
trading-day or session-calendar concept exists in this corpus or in `src/`. That
one has more than one defensible home and is raised at 3.3 rather than deduced
here.

### 3.2 The completeness pass
Not code. Every check in this repository compares one part of the corpus
against another, so an omission from the domain model is invisible to all of
them: dividends were absent for eight checkpoints and surfaced from a
conversation rather than a process.
- Walk one wheel turn end to end against the corpus and ask what the strategy
  involves that no document mentions. Cash movements between assignment and
  call-away, what a trial's economics include, what an account holds that the
  ledger does not name.
- Findings become obligations or decisions, and the pass runs before 3.3
  rather than after, because a missing concept is cheapest before the
  transitions exist.
- **DoD**: the pass is recorded with what it examined, not only with what it
  found. A pass that found nothing is evidence only if its scope is stated.

Reconciled at sign-off against what shipped. The clause above said "obligations
or 3.1 decisions" and was written while 3.1 was ahead; it signed off first, so a
settled finding takes its own D-W number here and the clause is corrected to say
so. Six findings, three settled as decisions and three raised as obligations.

**The pass had to compare the corpus against something outside it, which its own
argument forces.** If every check this repository has is corpus against corpus,
so is a survey that reads the corpus and asks whether it looks complete. Four of
the five axes were walked against OCC's own enumeration of the events that adjust
a contract, retrieved on 3.1's routes; the fifth had no external authority, since
nothing governs what a laboratory chooses to measure, and that is recorded as the
reason rather than left as a gap in the method.

**The scope was committed before the walk, and each axis is recorded whether or
not it found anything.** Both are the same discipline: a record written
afterwards cannot show that it preceded what it describes, and five axes reporting
two findings would leave a record silent on three. An axis that found nothing is
the only evidence it was walked at all.

**The dividend was one word in this corpus and two events in the market.** An
ordinary dividend pays cash; a non-ordinary one adjusts the contract by calling
for delivery of the dividend [D-W44]. That narrowed [D-W42], authored the day
before, to unadjusted dividends, because a holder who receives the dividend
through the deliverable has no reason to exercise early. A pass run after 3.3
would have found it against transitions already written.

**The passage that looks like the answer was the retired rule again.** The 10%
Rule defines an ordinary dividend by size and sits in the Background of the
filing that replaced it with a test of regularity. Second occurrence in two
checkpoints, and the reason the lesson from 3.1 is stated as a rule about
position in a document rather than as a fact about one filing.

### 3.3 The state machine and the ledger
Four states as a discriminated union, daily events driving transitions
[SYSTEM_DESIGN §3.8]. Four tables land here: `trials`, `positions` and
`ledger_entries` from §4.3, and `market_sessions`, because a calendar that is
transcribed rather than derived is a table like any other stated fact [D-W46] and
settlement cannot resolve a next session without one.
- Rolling bounded by whichever of the roll count and the trial days binds
  first, closing at market at the bound [D-W14]. Both legs of a roll reach the
  ledger, without which the projection cannot rebuild [D-W35]. The roll's
  decision row is Phase 4's, alongside every other decision, because
  `decisions` lands there [§4.3].
- Cost basis recorded both gross and net, with the covered-call constraint
  reading gross [D-W19], which 2.4 built against a parameter and this
  supplies.
- `trials` and `positions` are projections and may be rewritten only where a
  test rebuilds them from the ledger [D-W35].
- **Test**: a trial reaching the roll bound closes at market and resolves.
- **Test**: `trials` and `positions` discarded and rebuilt from
  `ledger_entries` give the same rows, which is what makes them projections
  rather than records.

Reconciled at sign-off against what shipped. Three decisions, three migrations,
four tables, the state machine, the ledger and its projections, and the fifteen
registry rows, which is every entry standing against this checkpoint. Four
obligations closed and five raised. The suite went from 503 to 629, and the guard
script from two named checks to three.

**The branch paused for review after the decisions and before any DDL existed.**
One checkpoint and two review points: what a split would have bought, without
moving fifteen registry rows and four obligations to buy it. A checkpoint is a
unit of work rather than a unit of review, which 3.1 already showed by being
pushed eleven times.

**The ordering paid twice, and both times on something a migration would have
frozen.** The event set turned out not to be a list of five more names: §3.8's
six lie on three axes, earnings drives no transition and is a gate input, and
exercise is assignment seen from the side this lab is never on. And the ledger
needed an eleventh kind, because a short bought back to roll and one bought back
to end a trial are two events under one cash direction and the sequence cannot
tell them apart afterwards.

**Migration 8 was edited in place rather than superseded, and the ground was
measured rather than assumed.** 0.3 took the other course and stated the rule
that decides between them: an amended migration never re-runs, so amending is
available only while nothing has run it. That is a condition, and four
measurements found it absent here. The clause raising this cited 1.3; it is 0.3,
and a wrong pointer sends the next reader to a phase that never had the question.

**Four defects were found by building the check rather than by reading.**
A foreign key from the record into a projection would have made the rebuild
impossible. Both SQL detectors were reading `--` comments as SQL, which two
sentences of ordinary English in new migrations were the first to collide with.
`MembershipKind` was the one enum in the store's vocabulary set starting at zero,
so `default` read as `Joined` and an uninitialised transition would have put a
name on the watchlist. And the bound sold shares on the session they were
assigned, which is a decision depending on an assignment that occurred that day.

**A review after sign-off found four more, and one of them was this checkpoint's
own correction made again.** Two were settled by [D-W49], which values a stopped
trial at the close rather than zeroing it and buys a forced close back at the ask
rather than at intrinsic. All four were priced in the same direction: each made a
trial look better or a loss look cleaner than it was, which is why nothing
prompted anyone to look for them. Committed capital was fixed at strike times the
multiplier on the argument that the quantity sat in one place; the state machine
then priced an assignment, a call-away and a forced close from the deliverable.
An adjusted put charged a trial 7,500 against the 5,000 it had committed. The
second was the cost bases, which are divisions bound through the refusing decimal
path, throwing on any premium the ledger's own scale admits and the share count
does not divide. Neither was visible to a test, because every contract in the
suite carried one hundred as both quantities and every premium divided cleanly,
which is exactly the condition the obligation described.

**Two consumers could not be verified, and that is recorded as a defect rather
than as a gap.** `TrialBounds` reads both trial bounds as of the simulated date
and nothing in `src/` constructs it, because the component that would is the run
loop. Loosening the Consumer column to name a type with no component behind it
would make that column mean one thing on verified rows and another on unverified
ones.

### 3.4 The fill model and the costs
Sells at the bid with explicit per-contract commission and assignment fees
[D-W12], never the mid, because end-of-day granularity means the lab never
observes the price it would have received.
- Sets `Costs:AssignmentFee`, discharging that obligation. It is a broker
  figure and no document states it, so record it as chosen with what it is
  chosen from.
- **Test**: the worked example's assigned trial totals what §6.3 states,
  which is the seventh fixture reading that document and the first to read
  its ledger, where the six before it read its chain, its verdicts and its
  bases.

### 3.5 Determinism, end to end
0.5 restated its byte-identical definition of done as identical stored rows
because no run existed; this is the checkpoint that has one.
- **Test**: a simulated run with a fixed clock produces byte-identical output
  across two invocations.
- Settles what bars nondeterminism in SQL that is not a clock, which the
  enumeration of the bundled SQLite raised.
- **DoD**: the guard covers every source of nondeterminism it names, and
  names every source it does not cover.

Detail for Phase 4 is authored when Phase 3 signs off.
