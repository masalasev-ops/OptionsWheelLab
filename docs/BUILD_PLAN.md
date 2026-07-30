# BUILD_PLAN

Build state: **Phase 0 complete; Phase 1 in progress**. 0.1 to 0.8 and 1.1 built and
signed off. 1.2 to 1.5 are live intent. Phase 2 detail not written.

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

| Owed at | Obligation | Raised |
|---|---|---|
| Phase 11 | Re-add `Microsoft.AspNetCore.OpenApi` against a version whose `Microsoft.OpenApi` dependency clears the audit. Removed at 0.1 rather than suppressing the advisory; the reason is in the Api project file. | PR #1 |
| Phase 1 | Give D-W29's write-side rule teeth. Every decimal reaching a `TEXT` column should pass through the canonical form, and nothing enforces that: `ConfigWriter.Append` takes a string. A decimal-typed parameter-binding seam is the likely mechanism, when the first real decimal column exists. | PR #3 |
| Phase 1 | Decide what an adjusted strike does when a corporate action makes it non-terminating. Identity canonicalises through the refusing path, so a 3-for-2 split forces a choice between rounding a value that is part of a contract's identity and carrying the ratio. | PR #3 |
| Phase 3 | Establish output-level determinism: a simulated run with a fixed clock produces byte-identical output across two invocations. 0.5 restated it as identical stored rows because no run existed to make. Compared as produced artefacts, never as a database file [D-W28]. | PR #4 |
| Phase 3 | Decide what bars nondeterminism in SQL that is not a clock. Enumerating the bundled SQLite showed `random()` and `randomblob()` alongside the seven clock functions; they are outside FX-ClockIsNotADateSource by name but would break a byte-identical run just as surely. | PR #4 |
| Phase 2 | Set the three `Risk:` fractions. 0.8 seeded nineteen rows-classed keys and left these because an equity-relative cap is the operator's risk appetite [D-W11], and the worked example illustrating one account is not the operator setting one. FX-GateRejectsAboveHeadroom needs them, so the phase that consumes them sets them. | PR #7 |
| Phase 3 | Set `Costs:AssignmentFee`. No document states it, and zero inferred from an absent ledger line is weaker than a stated number and invisible when wrong. Phase 3's assignment path is the first thing that computes with it. | PR #7 |
| Phase 2 | Decide whether the gate handles a crossed quote. 0.6's loader refuses bid above ask, which is the one domain rule it enforces, and that makes a crossed or locked market unwritable as a synthetic chain, so nothing can exercise the gate against one. D-W22's spread cap is a fraction of mid, so a crossed quote gives a negative numerator and passes a cap that exists to reject wide markets. If the gate handles it, the loader stops refusing it. | PR #5 |
| Phase 3 | Settle which quantity committed capital uses. D-W17's first paragraph says the contract multiplier and its third says the deliverable, and they differ for an adjusted contract. On a 3-for-2 split taking a $90 strike to $60 with a 150-share deliverable, strike times multiplier gives $6,000 and strike times deliverable gives $9,000, and only the second leaves the aggregate exercise where the adjustment found it. A reverse split may behave differently, in which case the aggregate exercise price is a stated fact per adjustment rather than a product of two columns, and the schema needs to carry it. Check against OCC's contract adjustment memos, not a secondary source. Raised at 1.1 while choosing a unique constraint, and twice reasoned wrongly from a sentence about premium quoting before the arithmetic was run. | v1.18.0 |
| Phase 4 | Store one feasible set per name and date rather than one per decision. `candidates` is keyed on `decision_id`, so three makers acting on one set write it three times, while [D-W4] requires the three to be byte-identical and `FX-ThreeMakersSameFeasibleSet` asserts it. Storing once and referencing thrice makes it true by construction and divides the largest uncertain table by three. Raised while estimating store size over a ten-year lifetime, before the table exists. | v1.17.0 |
| Phase 8 | Extract the market rules out of `SyntheticChainReader`, so one definition serves the synthetic reader and the vendor ingest. Refusing a negative bid, a negative ask and a crossed market are statements about what a market can be, not JSON concerns, and they sit as private statics on the reader, so a second producer of quotes can only duplicate them. Phase 8 is where that second producer arrives. **Coupled to the Phase 2 crossed-quote decision**: if the gate handles a crossed quote, the crossed rule moves to the gate rather than into the shared definition, so settle that first and extract what is left. Not extracted at 0.8 because there is one caller and the second does not exist. | PR #9 |
| Phase 2 | Reconcile `WORKED_EXAMPLE.md` with [D-W22] to [D-W25]. Its 45.00 strike fails the 12 percent spread cap at 18.18 percent of mid, so the three-candidate feasible set it teaches renders as two and the random maker's choice disappears along with the regret arithmetic built on it; the 47.50 strike passes at 11.97 percent, three hundredths of a point of margin, which is too fragile for the document that defines correctness. The fix is a deliberate rewrite of the chain, ideally so one candidate fails the spread cap by an obvious margin and the example teaches the gate as well. Downstream: seven registered fixtures read its conclusions; FX-WorkedExampleChainLoads parses §2 and §5 as its oracle and fails on a quote revision, which is a tripwire rather than an exposure; `Costs:CommissionPerContract` is seeded from §1 and `Trial:MaxTrialDays` is justified partly by the example's 109-day trial. Raised at v1.6.0, banner in §3, and never carried here because this table did not exist yet. | v1.6.0 |
| Phase 3 | Decide how dividends are recorded. Between assignment and call-away the account holds shares, and a dividend paid in that window is cash the trial received; omitting it understates every covered-call leg and misprices the buy-and-hold control [D-W13], which biases the exact comparison the lab exists to make. Needs a ledger `kind`, a source for ex-dates and amounts (`corporate_actions` already exists and lists `dividend` among its kinds), and a statement of whether the synthetic-chain format can express one. Raised from a review of what the wheel model omits, before Phase 3's detail is authored. | v1.22.0 |

---

## Phase 1 — Chain store and point-in-time invariants

Build state: **1.1 and 1.2 built and signed off; 1.3 to 1.5 not built**. On synthetic chains;
no vendor data until Phase 8. Delivers the market-data schema, the as-of read paths
over it, and membership as state.

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
- **DoD**: the worked example's chain loads into the store and reads back
  identical, against the same oracle 0.6's fixture uses.
- Discharges D-W29's write-side seam. This writes the first real decimal columns,
  which the obligation names as its trigger.

### 1.5 Corporate actions and the predecessor link
A split or special dividend mints a new contract identity with a recorded
predecessor rather than editing the existing row [§2].
- Settles what an adjusted strike does when the division is non-terminating. The
  obligation names the choice as rounding a value inside identity against carrying
  the ratio. This checkpoint owns it and the decision lands before the code.
- **DoD**: an adjusted contract is a new identity, its predecessor is recorded,
  and a historical join across the split resolves both.

Detail for Phase 2 is authored when Phase 1 signs off.

---

## Phase 2 and beyond

Not yet written. Detail for Phase 2 is authored when Phase 1 signs off, with
whatever Phase 1 taught already folded in.

The phase map in `SYSTEM_DESIGN.md` §7 states what each phase delivers and where
the data purchase boundary falls.
