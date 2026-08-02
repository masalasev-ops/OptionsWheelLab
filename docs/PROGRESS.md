# PROGRESS

Appended to, never rewritten. The repository is the authority on build state.

## Current state

**Phase 0 complete and reviewed. Phases 1 and 2 complete.** Checkpoints 0.1 to
0.8, 1.1 to 1.5 and 2.1 to 2.5 built and signed off. **Checkpoint 3.1 is in
progress on `phase-3/checkpoint-3.1` and deliberately unmerged**: two of its
seven mechanics are settled and five are not. The documentation corpus is at
v1.36.0.

## Log

### 2026-07-26 — corpus v1.0.0
Documentation corpus authored from scratch, superseding the unrecoverable v0.1
of 2026-07-12. Decisions D-W1 to D-W21 registered. Phase map fixed at 12 phases
with the data purchase boundary at Phase 8. Phase 0 checkpoint detail written;
Phase 1 onward deliberately not written yet.

Two configuration values deliberately left unset for Phase 0.8: the roll bounds
[D-W14] and the fast-slow divergence threshold and window [D-W20].

### 2026-07-26 — corpus v1.1.0
`UI_MOCKUPS.md` added. Six screens specified. Mockups not yet produced; §10 of
that document is empty pending a Design pass.

### 2026-07-26 — corpus v1.2.0
First UI mockup pass produced and reviewed. Four defects logged (UI-1 to UI-4),
all traced to underspecification in the brief rather than to the mockup, and all
closed by amending `UI_MOCKUPS.md`. UI-1 was an inversion of [D-W19], which is
the drift that decision exists to prevent, arriving through the documentation.

Schema required no changes to support any of the six screens.

### 2026-07-26 — corpus v1.3.0
Mockup pass 1 found unreadable by its owner. Diagnosis: two vocabularies stack in
this product, standard options terminology and the lab's own invented terms, and
the brief defined only the second. `GLOSSARY.md` added covering both. The brief
now requires in-place definitions and a plain opening sentence per screen.

Open and unresolved: the corpus generally assumes options fluency. `GLOSSARY.md`
and the `ORIENTATION.md` pointer are a first pass at that, not a complete fix.

### 2026-07-26 — corpus v1.4.0
`PRIMER_THE_WHEEL.md` added, covering the strategy itself rather than the lab.
This closes the larger half of the comprehension gap found in mockup pass 1: the
lab's own terms were undefined, but so was the domain underneath them.

### 2026-07-27 — corpus v1.5.0
Renamed to `OptionsWheelLab`. Namespace roots updated in [D-W1] and
`BUILD_PLAN.md` §0.1. Decision prefix `D-W` deliberately unchanged.

Mockup pass 2 reviewed. All four defects from pass 1 closed. The four
contract-level gate constraints the mockup depicted are now adopted as D-W22 to
D-W25, with config keys, fixtures, and two startup invariants.

### 2026-07-27 — corpus v1.6.0
Contract-level gate constraints adopted as D-W22 (liquidity), D-W23 (delta
ceiling), D-W24 (expiry window), D-W25 (earnings clearance). Eight fixtures
registered, six against Phase 2 and two against Phase 0.2 as startup invariants.
`SYSTEM_DESIGN.md` §3.4 restructured into portfolio and contract constraint
families.

**Open and blocking.** `WORKED_EXAMPLE.md` predates these constraints and
conflicts with them: its 45.00 strike fails the proposed spread cap, so the
three-candidate feasible set it teaches would be two. Seven fixtures derive from
that example. It needs either revised quotes or revised arithmetic before Phase 2.

### 2026-07-27 — corpus v1.7.0
`CLAUDE.md` rewritten against AlphaLab's working conventions. D-W26 added for
as-of configuration resolution, with the config read service folded into
checkpoint 0.3.

The Phase 0 prompt issued earlier this session predates both and should be
reissued rather than run as written: it does not carry the verification rules,
and checkpoint 0.3 has grown.

### 2026-07-27 — corpus v1.8.0
Three questions raised against checkpoint 0.2 before any code was written, all
answered in the corpus rather than in the prompt.

Config storage class settled as D-W27. The two cross-key invariants moved from
startup to config-write time, correcting an error in 1.6.0.

The third question reported D-W26 as absent from the register. It is present.
The working copy was mixed, carrying v1.7.0's `CLAUDE.md` against v1.6.0 of
everything else. No corpus change followed; the operational rule is to confirm
the working copy is current before reporting a corpus entry as missing.

### 2026-07-27 — corpus v1.8.1
FX-ConfigStoreClassHonoured's description now names the mechanism, making the
`CONFIG_REFERENCE.md` Store column machine-checked.

Second stale-working-copy question in as many turns, cause identified: the
corpus was distributed as a zip alongside two individual files, and copying the
individual files produces a mixed tree. Distribute the zip whole.

### 2026-07-27 — corpus v1.8.2
A propagation review raised four mismatches. Three were artefacts of a stale
working copy and were already correct in the corpus. The fourth was real:
`BUILD_PLAN.md` §0.2 still carried its pre-D-W27 wording because the 1.8.0 edit
silently matched nothing while the changelog recorded it as done.

`CLAUDE.md` gains §10 separating authored content from verified content, which
is the standing answer to who closes a propagation gap.

### 2026-07-27 — corpus v1.8.3
Checkpoint 0.1 + 0.2 planning raised three authored-content findings, all real
and all corrected here: the Phase 0 definition of done contradicted D-W27, the
`Eodhd:BaseUrl` row lacked an Unset marker, and the `CONFIG_REFERENCE.md`
build-state line asserted nothing was verified.

Checkpoint 0.1 + 0.2 approved to build against v1.8.3.

### 2026-07-27 — checkpoints 0.1 and 0.2 built
Repository created at `masalasev-ops/OptionsWheelLab`, public. .NET 10 solution
with `OptionsWheelLab.Core`, `.Worker`, `.Api` and `.Tests`. CI runs restore,
build and test on push to `main` and on every pull request. 23 tests green,
build clean under warnings as errors.

One section binds: `Eodhd`, to `EodhdOptions`, verified by reading the
composition root. It is the only section `CONFIG_REFERENCE.md` classes as `app`.
No `rows`-classed section is bound, and FX-ConfigStoreClassHonoured fails the
build if one ever is. The three `Eodhd` Consumer entries are now verified; the
other nineteen rows carry **Unverified** until their checkpoints land.

Configuration lives in one shared `src/appsettings.json` linked into both hosts
and the tests, rather than one file per host, so the Worker and the Api cannot
disagree about it. `appsettings.Secrets.json` is gitignored with a committed
`.example` and loads optionally, so a fresh clone builds without it.

The two cross-key invariants are pure predicates in `Core` over supplied values,
with no host, store, startup wiring or clock. Enforcement at config-write time
is owed by 0.8.

`Microsoft.AspNetCore.OpenApi` was left out. Version 10.0.9 pulls
`Microsoft.OpenApi` 2.0.0, which carries a high severity advisory that the
build's vulnerability audit failed on. The API surface is Phase 11 and has no
endpoint to describe, so the package is added back there against a patched
version rather than the audit being suppressed now.

### 2026-07-28 — corpus v1.9.1
Three findings from the PR #1 build answered in the corpus. The OpenApi
removal was a decision rather than a defect and is now a Phase 11 carried
obligation. FIXTURES.md rule 2 was unenforceable as written and is split by
direction, moving FX-RegistryMatchesDisk to 0.2. A third configuration
direction is registered as FX-EveryBoundKeyIsDocumented.

### 2026-07-28 — corpus v1.9.2
CONFIG_REFERENCE.md now carries one key per row. Checkpoints 0.1 and 0.2
verified complete against corpus v1.9.1.

### 2026-07-28 — corpus v1.9.3
BUILD_PLAN.md 0.1 and 0.2 reviewed against what shipped. The checkpoint text
was substantially accurate; the build-state marker was false and four passages
described less than what was built. Both checkpoints now match the repository.

### 2026-07-28 — corpus v1.9.4
`prompts/spent/phase-0.md` created, closing the gap where BUILD_PLAN described
an archive the repository did not have. One file per phase rather than one per
prompt, and each entry carries an absolute state snapshot so the current state
is read in one take rather than reconstructed from deltas. Five prompts spent
against Phase 0 so far, all recorded.

### 2026-07-28 — corpus v1.9.5
The spent-prompt archive now carries one overwritten Current state section
rather than a snapshot per entry. Per-entry snapshots meant four of five
described a state no longer true. What was asked and what was delivered cannot
age, so they stay in the entries; state is current or it is wrong, so it lives
in one place.

### 2026-07-28 — corpus v1.9.6
The spent-prompt archive carries one prompt per checkpoint rather than one per
ask, with corrections folded back into the checkpoint's prompt. Replaying the
prompts reproduces the state without replaying the mistakes. What was asked at
the time is no longer recoverable from the archive; that history stays in the
commit log and the pull request thread.

### 2026-07-28 — corpus v1.9.7
Checkpoint 0.3 scoped. The as-of and current-value config surfaces are separated
so D-W26 holds by construction. The reverse binding direction is recorded as a
per-checkpoint definition of done. Time formats are pinned, WAL journal mode is
stated, and D-W27 gains the bootstrap clause that puts `Storage:Path` in
`appsettings` by necessity rather than by the read-path criterion.

### 2026-07-28 — corpus v1.9.8
Snapshots move to `VACUUM INTO` and the mechanism becomes D-W28 rather than
prose in one document contradicted by the code and justified in a doc comment.
The build had rejected `VACUUM INTO` because the three-file copy was what the
corpus specified; holding the lock that copy needs turned out to make `-shm`
unreadable, so the specification was never satisfiable. D-W28 also states the
shape a snapshot has on disk.

The test project now references both hosts and carries tests that use them, so
a broken host fails `dotnet test` rather than only the separate build step, and
the provider-ordering fix is asserted rather than demonstrated.

### 2026-07-28 — corpus v1.9.9
Checkpoint 0.4 scoped. D-W29 makes the stored decimal form canonical, because
strike participates in contract identity and two spellings of one strike would
give one contract two identities and split its history without failing. The same
decision separates the scale's two meanings: a fidelity requirement for
vendor-supplied values, which refuse rather than round, and a rounding policy for
computed ones, which round explicitly.

The `Pct` keys are renamed to `Fraction`. The suffix named a percentage while
every description said fraction, and `Gate:MaxSpreadPctOfMid` was proposed as 12,
which is only meaningful as a percent. Every affected key is unset and
unconsumed, so the rename was free and could not have been free again.

Three documents carried prose that D-W28 had already superseded or that stated a
version long past. `CLAUDE.md` §10 gains the rule that distinguishes correcting
such prose from a build overruling a document: the authority is the landed
decision, never the code.

Checkpoint 0.4 built and signed off. The stored decimal form, the stored date and
contract-right forms, typed configuration accessors, ticker normalisation,
contract identity with a total order, the decimal-ordering check, and the first
source guard that is not a unit test. 156 tests.

Two rules came out of building it rather than out of planning it. `CLAUDE.md` §1
gains that a code comment is not a corpus record, written after a report claimed
an obligation was recorded when it sat in a fixture comment. `BUILD_PLAN.md`
gains the three states a checkpoint's detail passes through, with reconciliation
and archiving bound to the single moment a checkpoint is determined fully built.
That second rule replaced a weaker one written the same day, and it also closed a
defect the checkpoint had hit four times: Current state kept going stale because
it was written mid-build rather than after the last change.

### 2026-07-28 — corpus v1.9.10
The three-state rule refined twice by trying to apply it. It now says when a
checkpoint is determined fully built, which is after review closes rather than
when the code is written, because at 0.4 review changed the deliverable four
times after the prompt had been archived.

Current state stops recording the branch and the merged pull requests. Git holds
both, a fact kept in two places drifts, and they were the only fields that could
not be known at the moment the rule says to write them. Removing them closed the
timing gap instead of adding a step to work around it, which is the fifth
instance of one pattern at this corpus version: the fix for a duplicated fact is
to delete the copy, not to synchronise it.

### 2026-07-28 — corpus v1.10.0
Checkpoint 0.5 scoped. D-W30 places the clock: it returns the instant the process
is running at, a simulated date is never obtained from it, and it is read at
composition and entry points only. That is D-W26's rule arriving through a
different door, because a component wanting the simulated date and reaching for
the clock gets an answer that is plausible, non-null and wrong.

0.5's byte-identical definition of done had no subject, since there is no
simulated run at 0.5. It is restated as identical stored rows compared as table
contents, and the output-level property is carried to Phase 3, which is the first
checkpoint with a run to make. A SQLite file is not a deterministic rendering of
its contents, so the comparison was never going to be of bytes.

Three corrections came out of applying the corpus rather than out of the
checkpoint. The Step 0 gate said stop on any version mismatch, which would make
every docs-only bump either halt a checkpoint or teach the gate to be ignored, so
`BUILD_PLAN.md` now says establish first and proceed only where the drift
demonstrably does not reach. D-W26 gains the clause that a written version is
never altered, which is what makes `config_rows` append-only; the triggers
enforcing that had cited D-W8, which governs snapshots and does not reach a
versioned configuration table. And 0.7's constraint counted five statements while
enumerating four; measuring found six, one of which is an `UPDATE` against a
table the rule does not cover, and that is what established that the check
distinguishes by table rather than by location. The count left the constraint
with it, because four of the six are tests and a count that moves whenever a test
is written is not a property of the rule.

Two enumerations were replaced by the properties they were trying to state, and
the second is the same defect as the first. `CLAUDE.md` §2 item 4 listed two
forms of an ambient time read where the guard catches six, so it now states the
rule and points at the script for the list.

0.6's detail turned out to conflate two things this corpus calls fixtures. A
check is a registry entry; a synthetic chain is test data. 0.6 builds the loader
for the second and does not read `FIXTURES.md`, which registers checks and holds
no data. Its one definition of done was 0.2's registry check restated, so as
written 0.6 would have discharged on work another checkpoint did. The Kind column
0.5 added is what made the two kinds of check distinct enough for the third thing
to be visible as a separate thing at all.

Checkpoint 0.5 built and signed off. `IClock` and the system clock, the migrate
verb reading it, `--at` and the instant `migrate.ps1` computed both removed, a
determinism test comparing stored rows across two runs, the ambient-clock guard
beside the floating-point one, and the two checks D-W30 names. 199 tests.

The SQL half of FX-ClockIsNotADateSource had to be measured rather than
specified. SQLite reads the clock through six forms that carry no marker at all,
because a date function's time value defaults to the current instant when it is
omitted, and `'subsec'` in that position does the same. Every other modifier
returns null rather than implying now, which is what bounds the residual at one
word instead of at the modifier set. The function list was enumerated from the
bundled binary rather than from documentation, and turned up a seventh function
nobody had considered.

0.5's detail asked for four things and the checkpoint shipped sixteen. The
largest single cause was one conflict: a registered check that had to be a script
rather than a file. That is why `BUILD_PLAN.md` now says a checkpoint's detail
names everything the checkpoint ships, including corrections nothing in it
caused, and why 0.5's detail carries a section listing them.

### 2026-07-29 — corpus v1.11.0
Checkpoint 0.6 scoped. D-W31 states that a synthetic chain is authored by a
person and that the format optimises for being written and read by hand, paying
for it in loading cost. That settles the open question at 0.6 in favour of a
domain shape, and the deciding argument is not readability: the fields a
schema-mirroring row repeats are three of the four that make up contract
identity, so a hand-typo would mint a different contract rather than fail.

The decision states what the format optimises for rather than what the format is,
so a later phase changing the format supersedes nothing while a later phase
changing the property has something to supersede.

Hand-written chains live in `synthetic/` at the repository root. `README.md` now
says the corpus rule governs documents only, since code, spent prompts and
synthetic chains are none of them. The directory is not called `fixtures/`,
because 0.5 established that a fixture is a registry entry and a `fixtures/`
directory holding no `FX-*.cs` would undo that.

One obligation is carried to Phase 2. The loader refuses a quote whose bid
exceeds its ask, which is the single domain rule it enforces, and that makes a
crossed or locked market unwritable as a synthetic chain. Nothing can then
exercise the gate against one, which is a hole in D-W31's own premise that
deliberate cases are writable.

Checkpoint 0.6 built and signed off. The synthetic chain types, the reader, the
worked example transcribed into `synthetic/worked-example.json`, and the three
checks registered against it. 224 tests.

The format is a chain rather than a table, and the deciding argument was not
readability. The fields a schema-mirroring row repeats are three of the four that
make up contract identity, so a hand-typo would mint a different contract rather
than fail, which is the failure D-W29 exists to prevent arriving through the
fixture instead of the store.

Every value in a chain is a quoted string, including the numbers. The source
guard names a JSON number bound into an untyped tree as something it cannot
catch, so the format closes that by construction rather than by discipline, and
an unquoted value is refused.

The acceptance test parses `WORKED_EXAMPLE.md`'s two tables rather than restating
their numbers, which makes it the third document to be machine-checked after the
Store column and the fixture registry. That also closes a coupling that could
only be flagged before: §3's unresolved banner may be settled by revising the
quotes, and the test now fails on that revision and names the value rather than
letting the transcription drift until Phase 2.

The loader refuses a quote whose bid exceeds its ask, which is the single domain
rule it enforces, and what that costs is carried at Phase 2.

### 2026-07-29 — corpus v1.12.0
Checkpoint 0.7 scoped. The append-only rule has existed since D-W8 and nothing
has ever enforced it, so building the check had to settle three things the corpus
left open: which tables the rule covers, what mechanism the check is, and whether
the source guards move to Roslyn.

"Snapshot tables" was used in four documents and defined in none. §4.1 now
defines the six, and `contracts` is one of them despite carrying no `observed_at`
because a corporate action mints a new identity rather than editing a row. §4.0
documents `schema_migrations`, which 0.3 created and which §4 had never carried
while documenting seventeen tables that do not exist.

The check is a fixture rather than a source guard. 0.4's criterion decides it,
and a measurement settles it beyond the criterion: `guards.ps1` strips raw string
literals by design and every SQL statement here lives in one, so a pattern added
to the script would match nothing in the tree by construction.

Two citation errors of one shape are now three. `decisions` and `candidates` were
cited to D-W3 for a property D-W3 did not state, exactly as `config_rows` was
cited to D-W8 until 0.5. Both were right about the rule and wrong about its
authority, which is why reading either document alone found neither, and building
the check that rests on the citation is what found both. `CLAUDE.md` §1 gains
that method, and applying it to the vocabulary before writing any code predicted
the third: `schema_migrations` would have rested on §4.0's prose, so D-W32 states
the property instead. That is the first instance found deliberately.

### 2026-07-29 — corpus v1.13.0
Checkpoint 0.7 built and signed off. The append-only vocabulary of ten tables,
FX-NoRewriteOfAppendOnlyTables and its detector, §4.0 for the migration ledger,
§4.1's definition of the snapshot tables, and three corrected citations. 245
tests.

D-W33 closes the obligation 0.4 raised and deferred here: the source guards stay
a text scan and a fixture. The measurement refuted the reason the split was
given. `guards.ps1` claimed a guard must fail even when the build does not, and a
probe with a violation in one file and a type error in another reported both, so
the claim was false. What an analyser cannot survive is a failed restore, where
none runs at all. The script runs before restore, so its property is narrower
than claimed and true, and it now says so.

One check of four would have gained anything. The two SQL checks are SQL-parsing
problems where an analyser returns the same string literal a fixture already has,
so "one mechanism serving four" did not survive contact with what the four are.

The obligation is closed with a no-change answer rather than left to lapse, and
the topical index gains a Verification mechanisms line, the six existing ones
being about the experiment rather than about how the repository checks itself.

### 2026-07-29 — corpus v1.14.0
Checkpoint 0.8 scoped, the last of Phase 0. Its detail named four unset keys and
measuring found ten: `CONFIG_REFERENCE.md` marks all six `Gate:` keys unset with
proposed values and D-W22 to D-W25 each assign them to Phase 0.8, so the two
documents disagreed and the checkpoint detail was the one that was wrong.

The seven `Policy:` keys are seeded too, though they carry no unset marker,
because without them D-W23's invariant cannot be exercised at all: the predicate
passes vacuously against an empty band set, which the fixture that has asserted
it since 0.2 already documents.

D-W34 makes that vacuity unreachable. A write touching a key one of the
invariants needs, and leaving the store without the rest of them, is refused. The
consequence is the mechanism: `Gate:MaxDte` and `Trial:MaxTrialDays` cannot be
written apart, so the pair is atomic by the write path rather than by the
seeder's discipline, and every later phase that writes configuration inherits
that without knowing to reproduce it.

Provenance is judged per key rather than per section, which `Policy:` already was
and `Costs:` was not. Four keys are left unset and both are owed rather than
open, since leaving a key unseeded and leaving it unscheduled are different
things and only the first was deliberate.

§3.5 said the makers select inside the same delta and expiry bands. True for
expiry, false for delta, and the worked example has said so since v1.0.0. Found
by seeding the values the sentence describes.

### 2026-07-29 — corpus v1.15.0
Checkpoint 0.8 built and signed off, and with it **Phase 0**. Nineteen of the
twenty-three `rows`-classed keys carry a value at version 1, the two cross-key
invariants are enforced in `ConfigWriter` on every write, a `seed` verb and
`seed.ps1` write them, and FX-ConfigWriteRefusesInvariantBreach and
FX-EveryAppKeyBinds are registered against 0.8. 264 tests.

The invariants had been pure predicates with no caller since 0.2. They are wired
in the writer rather than in the seeder, because the seeder is one caller and
`Append` would have stayed an unguarded path, and because D-W23, D-W24 and D-W27
all put enforcement at the moment a version is written precisely so that later
versions are guarded too.

D-W23's open clause is closed. The ceiling at 0.35 is chosen, argued from D-W4:
a control drawing from a smaller opportunity set than the gate admits would make
a difference between it and the learner partly permission rather than judgement.
The 0.10 floor is a separate question that argument does not reach, and is
recorded as inherited rather than argued. Two provenances for two bounds of one
band, which is what judging provenance per key means when it is applied.

The `app`-classed reverse direction is a registered fixture now, and the
assertion behind it is not new: it landed at 0.4 outside the registry, because a
phase definition of done was held not to be a fixture. It moves into
FX-EveryAppKeyBinds rather than being copied there. What was genuinely missing
was in `CONFIG_REFERENCE.md`, whose paragraph declining the reverse check did not
say which class it declined it for, so it read as complete while covering `rows`
keys only, and had done since `Eodhd` bound at 0.2. A finding reported at 0.8
said the check itself was absent; that was asserted from the fixture files
without reading the unregistered suites, and it was wrong.

`seed.ps1` ships for a reason that had to be measured rather than assumed. With
`Storage__Path` unset, `migrate.ps1` refuses cleanly and the bare verb throws the
same words under a stack trace. Two steps of one setup sequence, the second
reporting the identical mistake worse than the first.

Phase 0 delivers a repository that compiles, tests, migrates and runs
deterministically, with no market data and no domain logic, which is what it said
it would deliver.

### 2026-07-29 — corpus v1.15.1
The worked-example reconciliation is a carried obligation, owed at Phase 2. It has
been recorded since v1.6.0 in a banner at `WORKED_EXAMPLE.md` §3 and in this log,
and was never carried into `BUILD_PLAN.md` Carried obligations because that table
did not exist until v1.9.1. Nine minor versions and three checkpoints that took
dependencies on the example passed in between. The obligation is unchanged; only
its location is, and the location is the whole point, since planning for Phase 2
will read the table and not the banner.

Carried obligations said it holds work deferred out of a checkpoint, which is why
a corpus-raised obligation had nowhere to go. It admits both origins now, and the
Raised column carries a corpus version where there is no pull request to name.

The changelog now says why there is no 1.9.0, above the 1.9.1 heading where a
reader meeting the gap is looking. That note was proposed at 0.5 and not applied,
and four days later the clause raising this obligation cited v1.9.0 as the version
that created the table. An unexplained gap is not inert: it reads as a missing
entry, and a missing entry gets guessed at.

### 2026-07-29 — corpus v1.16.0
Phase 0 reviewed, the first pass over the code by a reader who went to the files
rather than to a report. Five findings, one of them a defect and four of them
things the code was right about and silent on. 268 tests.

**The defect.** `ConfigWriter.Append` returned `MAX(version)` read after its own
transaction committed, while claiming to return the version written. Sole-writer
made it true [D-W1] and Phase 4 makes it false, and it would have failed by
returning a plausible number. `RETURNING` inside the insert now.

**The one that would have let a bad configuration through.**
`PolicyBandCeilings` had no completeness check, and its own remarks already named
the failure. An incomplete catch-list still catches what is on it; an incomplete
band list makes D-W23's ceiling pass against fewer bands than exist. The learner's
band arrives at Phase 4 and nothing would have failed had it been omitted.

That produced a rule worth more than the fixture: **a declared vocabulary is
checked standing in the direction in which absence causes the bad outcome.** For
`DecimalColumns` and `AppendOnlyTables` that is list to document, a name with no
table being the error. For `PolicyBandCeilings` it is document to list. The three
looked inconsistent and are not, and nothing had said why.

The other three are silences rather than errors: pooling disabled with no note of
what it protects, an as-of read that cannot join a transaction where the current
read can, and market rules living inside a JSON reader with no schedule for
getting them out. Each is correct today and each has a phase at which it stops
being, which is what the records now say.

### 2026-07-29 — corpus v1.17.0
Phase 1 checkpoint detail authored, five checkpoints: the market-data schema,
as-of reads, membership as state, chain ingest, and corporate actions with the
predecessor link. Documentation only; no code.

Phase 1 is the first phase whose detail was written after its preconditions were
settled rather than alongside them. The effective-dating question forced that
order, because it decides a column shape, and a detail written before it would
have described a schema that might be wrong.

D-W35 settles that question. A record is the only place a fact is held, so
rewriting it destroys the fact and it is append-only; a projection is derived from
an append-only source and may be rewritten, because it can be rebuilt.
`watchlist_membership` is a record; `trials` and `positions` are projections of
`ledger_entries`. The condition is not free: a projection may be rewritten only
where a test discards it, rebuilds it and gets the same rows, and that test also
proves the ledger's `kind` vocabulary carries enough to rebuild from, which
nothing else checks.

**Three snapshot tables could not have accepted a correction.**
`underlying_bars`, `chain_snapshots` and `contract_quotes` were keyed without
`observed_at`, so a second row for the same bar violated the key and the only way
to record a vendor correction was an update, which D-W8 forbids and the
append-only guard now refuses. §3 has said corrections arrive as new rows since
v1.0.0, so the prose was right and the keys contradicted it for eleven versions.
Nothing noticed because none of the three tables exists yet, and
FX-SnapshotNeverRewritten is registered against Phase 1, so the first thing that
would have caught it is the checkpoint this detail describes.

Found by reading the schema against what rests on it, which is the method that
found three wrong citations at 0.7 and is now the third time it has paid.

### 2026-07-29 — corpus v1.18.0
Checkpoint 1.1's corpus changes, ahead of the code.

**§2's identity claim is false, and it is recorded rather than corrected.** An
adjusted series can share underlying, expiry, right and strike with a standard
contract and differ only in the deliverable: a three-for-two split takes a 90 strike
to 60 with a 150-share deliverable while a standard 60 strike with 100 shares lists
alongside. Checked against Fidelity's contract-adjustment guidance, Schwab on
non-standard options, and OCC's own symbology memo rather than reasoned about. §2's
promise that an adjusted contract is a new identity with a predecessor link cannot be
kept when the new identity equals an existing one. It reaches D-W29,
`ContractIdentity` and 1.5, so it wants a decision.

`contracts` gains the constraint the answer allows, on the deliverable rather than
the tuple. Not on `vendor_symbol`, which is the field OCC uses: a synthetic chain
carries none, SQLite treats nulls in a unique index as distinct, and it would guard
nothing until Phase 8 while the duplicate-insert bug is live from 1.4. The residual
is recorded too, since OCC says a symbol without a numeric suffix only "almost
always" designates a standard option.

**The multiplier and the deliverable were one column.** They are two quantities and
`multiplier` was named for the one that does not change while being intended as the
one that does. Split. Which of the two committed capital uses is a separate question
and is **not** settled here: D-W17's first paragraph says the multiplier and its
third says the deliverable, and the arithmetic favours the third, but a reverse split
may behave differently and the aggregate exercise price may have to be a stated fact
per adjustment. Owed at Phase 3, which computes the metric, and to be checked against
OCC's adjustment memos.

That question was twice reasoned wrongly here from a sentence about premium quoting
before the arithmetic was run. `WORKED_EXAMPLE.md` cannot adjudicate it either: for a
standard contract both quantities are one hundred, so `strike x 100 x contracts` is
silent on which one the hundred is.

Phase 1's two registered rows move to checkpoint granularity, and `FIXTURES.md` rule
2 now says when a row belongs at phase granularity and when that becomes a defect. A
row left at phase granularity after its detail exists makes every checkpoint's
definition of done resolve to nothing.

### 2026-07-30 — corpus v1.19.0
Checkpoint 1.1 built and signed off. Schema 3: the six market-data tables, three
indexes, twelve triggers, a `CHECK` and a uniqueness constraint. 304 tests, 240
across twenty-four fixtures. `prompts/spent/phase-1.md` opens and Phase 0's file is
closed.

**Three findings came from a measurement answering a different question than the one
asked.** Measuring the decimal vocabulary's false-positive surface, which was zero,
found a false negative instead: the detector filtered `LAST` as an order keyword
before consulting the vocabulary, so `ORDER BY last` would have been dropped the
moment `last` became a column. Running the alias convention over real statements
rather than synthetic ones found two defects in the new detector, one of which would
have flagged thirteen legitimate trigger bodies. Demonstrating the twelve refusals
against a real store, rather than reading the twelve assertions that pass on either
wording, found a trigger message telling an operator to append a row carrying a
column that table does not have.

That is the same shape three times: the check that finds something is the one run
against the real subject rather than the one reasoned about.

**§2's identity claim is false and is recorded rather than corrected.** An adjusted
series can share underlying, expiry, right and strike with a standard contract and
differ only in the deliverable. Verified against Fidelity, Schwab and OCC's own
symbology memo, then demonstrated against the built schema: two contracts, one
tuple, both admitted by a constraint designed to admit exactly that pair. It reaches
D-W29, `ContractIdentity` and 1.5, so it wants a decision.

D-W17's two quantities were split into two columns. Which one committed capital uses
was deliberately not settled, and the arithmetic favouring the deliverable is in the
Phase 3 obligation rather than in a decision, because a reverse split may behave
differently and `WORKED_EXAMPLE.md` cannot adjudicate it either way.

The SQL alias obligation is discharged by convention, the first Phase 1 row to
close. Its cost is dated to 1.5, whose definition of done needs a self-join, and the
walk turns out to be expressible without one as a recursive CTE, measured against
the built schema.

**Six divergences between the schema document and the migration** were reconciled
after the fact, all introduced by this checkpoint. Nothing parses §4.1 as a schema,
so no check caught them, and the one that mattered was nullability: the document
marked ten columns `NOT NULL` by omission that the migration makes nullable.

### 2026-07-30 — corpus v1.20.0
Checkpoint 1.2 scoped. Three corrections land ahead of the code.

The fixture registry's build-state marker undercounted by five, still describing
0.7's close. The schema document's build-state paragraph still called market data
specification after 1.1 built it, and now maps each §4 section to its checkpoint.

BUILD_PLAN 1.2's detail was wrong about the read's shape, and it mattered: it said
the market-data read is `AsOfConfiguration`'s shape with a stamp in place of a
version, but that shape filters on one axis and a market-data read filters on two
independent ones, which session the row describes and when it was observed. The
second axis is the whole reason 1.1 put `observed_at` in the key. The detail now
states the two-filter shape, and why the absence of a `version` column is not an
omission: `observed_at` is in the primary key, so the tie `version` exists to break
cannot occur.

### 2026-07-30 — corpus v1.21.0
Checkpoint 1.2 built and signed off. The as-of read surface over the market data,
one type with no current-value counterpart, and §4.2's membership schema settled by
D-W35 and carried here because 1.3 could not be prompted without it. 318 tests, 244
across twenty-four fixtures.

Market data gets one read surface where configuration got two, and the difference
is argued rather than inherited: configuration has an operational consumer for
current values and market data has none, so no current-reading type exists to cast
to, which is the strongest form of the rule. The shape check asserts the as-of
parameter by name and type on every value-returning member, because a two-axis
read can take the session date and still leak the latest observation, which a
check asking only for a date would pass.

The column-alias rule was blind to every parenthesised expression, measured before
the chain read was written: the source arm was an identifier class, and the
character before `AS` in `MAX(observed_at) AS latest` is `)`, which that class
cannot match. The aggregate form is exactly what a naive chain read writes, so the
blindness would have been exercised this checkpoint. Widened, swept over the tree,
zero flags. A CTE header stays clean under the widened rule because its alias
group requires an identifier and the token after `AS` there is `(`, so the
distinguisher is in the pattern rather than in an exemption, and the chain read is
written as a CTE with declared column names.

The join 1.1 deferred is settled by measurement rather than migration:
`EXPLAIN QUERY PLAN` shows the uniqueness constraint's own index serving the
lookup, so there is no migration 4 for indexing. One is owed for a different
reason, found by reading the migration against the record: `underlying_bars`
refuses the bars the worked example supplies, and 1.4's detail carries the fix as
its first item.

`ResolveAtOrBefore` does not gain the transaction its remark predicted. 1.4 was
read rather than guessed: its read-back is verification after commit, and the
remark's ender was doubly wrong, naming a membership resolution 1.4 does not
contain and that would never pass through a config reader if it did.

### 2026-07-30 — corpus v1.22.0
Checkpoint 1.3 scoped. One obligation raised, two authored corrections to §4.2,
and 1.4's migration ordinal corrected while its detail is live intent.

The dividend gap becomes a carried obligation owed at Phase 3. Dividends appear
in the corpus three times, as an ingest source, a `corporate_actions` kind and an
early-exercise risk, and nowhere as a ledger entry, while D-W13's buy-and-hold
control names capital and window but not the dividends the held shares pay.
Between assignment and call-away the account holds shares, so omitting the
dividend understates every covered-call leg and misprices the control, which
biases the exact comparison the lab exists to make. Thirteen obligations stand.

§4.2 now names the resolution axis it left as "the latest row": the row with the
greatest (`effective_on`, `version`) governs. The axis matters because the
alternatives disagree on a real case: under latest-version, a correction fixing
an old join date would silently override a genuine later departure. The same
edit marks `reason` nullable, which the document's own convention otherwise
denies, on `config_rows.note`'s precedent.

1.4's "Migration 4 first" becomes "A migration first, before any ingest code",
because 1.3 takes migration 4 and the property was always the ordering rather
than the number.

### 2026-07-30 — corpus v1.23.0
Checkpoint 1.3 built and signed off. Migration 4 creates the membership record
with three triggers, the writer appends transitions in the config writer's
shape, and the as-of read resolves the sequence on both axes. 346 tests, 247
across twenty-five fixtures.

The read is its own type rather than a member of the market-data surface. The
one-surface guarantee there rests on market data having no operational
current-read consumer, which was never argued for membership and is probably
false for it: the watchlist is operator-managed state and Phase 8's ingest
plausibly reads current membership to know what to fetch. The mirrored
no-current tripwire says a current surface arrives as a decision that amends
it, which makes the addition deliberate rather than a drive-by.

The governing axis was measured rather than asserted. Latest version and
latest effective date agree except when a correction carries an earlier
effective date than a later genuine transition; flipping the window ordering
to version alone fails exactly the divergence test and passes the other five.
Under the chosen axis a correction supersedes a transition only by tying its
date, and correcting a date is a compensating pair.

The monotonic stamp question was answered yes, inside the migration, because
an applied migration's SQL is frozen. Version ordering constrains versions,
not visibility, so without the trigger an appended correction with a backdated
stamp would change what was believed at a past instant after the fact. The
snapshot tables deliberately carry no analogue; membership has `config_rows`'
geometry, version order crossing stamp order.

Two things the build found rather than planned. No from-previous-schema
migration test existed in the suite, because every store in the tree was
either empty or current until this checkpoint created a real gap; the upgrade
test now builds its previous-schema store from the frozen migration list
itself. And the alias detector flagged this checkpoint's own migration
comment, "version as config_rows", which was reworded rather than the detector
narrowed: prose inside a SQL literal is inside the scanner's jurisdiction.

### 2026-07-30 — corpus v1.24.0
Checkpoint 1.4 scoped. Its check is registered new,
FX-WorkedExampleChainPersists, and the detail settles two things it left
open: a second ingest at the same instant is refused by the keys while a new
instant appends alongside, the correction model arriving at the ingest level,
and no Worker verb ships, tests being the only caller until Phase 8's vendor
ingest needs an operator entry point.

`contract_quotes` was reconfirmed against `ContractQuote` by direct read, so
migration 5 is scoped to `underlying_bars` alone. The 1.2 finding that raised
the migration was itself a lesson in verifying one record and claiming both,
which is why the reconfirmation was done rather than assumed.

### 2026-07-30 — corpus v1.25.0
Checkpoint 1.4 built and signed off. Migration 5's rebuild, the write-side
seam, the chain writer, and the round trip against the document's own tables.
365 tests, 250 across twenty-six fixtures. Twelve carried obligations stand.

The enumeration rule paid immediately. The 1.2 finding named four columns and
`UnderlyingBar` makes five optional, `volume` being the fifth, so a migration
written from the sentence would have relaxed four and still refused the
record. A standing record-to-schema test keeps the enumeration a property:
pragma nullability against the record's optional properties, so a record
change names the migration owed.

The rebuild's triggers are demonstrated rather than assumed, because DROP
TABLE takes them with it and a forgotten recreation passes every schema
check. The refusals are asserted against the rebuilt table on a seeded row,
and a hand-populated schema-4 store is carried through the copy.

The write-side obligation closed with its teeth stated honestly: `AddStored`
renders every typed value through its stored form, the decimal path refusing
rather than rounding, and the chain writer binds exclusively through it;
exclusivity is review's to hold, a type-level check having been declined at
D-W33.

The writer demonstrates both second-run behaviours and its own atomicity: a
same-instant re-ingest is refused with the correction path named, a new
instant appends alongside with each observation visible to its own as-of,
and a mid-transaction collision after the header insert leaves no header
row. An upsert is impossible by construction, the append-only trigger
refusing the update half.

One premise correction: the markdown-table parser needed no extraction,
having been shared since 0.6. The duplicated half was the header
vocabularies, structural constants and chain-file load, which moved to a
shared oracle helper in a pure refactor before the persistence fixture
consumed them.

### 2026-07-30 — corpus v1.26.0
Checkpoint 1.5 scoped, the last of Phase 1, and the corpus's two oldest open
questions get their answers before any code.

D-W36 dissolves the adjusted-strike dilemma rather than deciding it.
Adjusted terms are transcribed from what the adjusting authority states,
never derived from a ratio: OCC publishes the terms per event, the
methodology is era-dependent rather than a formula, and the SEC record of
the 2007 change documents the rounding-to-eighths windfalls that deriving
reproduces. The refusing decimal path is the tripwire, so record-not-derive
is enforced by the seam that already exists. The PR #3 obligation closes
with neither of its two options ever running.

§2's identity claim resolves by adding the deliverable to identity, which
the store's uniqueness constraint has enforced since 1.1. The banner comes
down in the commit that makes the claim true.

Two obligations are raised at Phase 3 beside the closure. The
settlement-mechanics row applies the D-W36 treatment forward to every
mechanics fact the state machine will rest on: exercise-by-exception,
assignment knowledge versus occurrence, T+1 cash availability, the
ex-dividend early-assignment model, and dividend entitlement timing. The
completeness-pass row exists because consistency checks cannot see absence:
every check the repository has compares one part of the corpus against
another, and the dividend gap stood for eight checkpoints before a
conversation rather than a process surfaced it. Twelve obligations stood
before the closure, eleven after it, twelve after the mechanics row,
thirteen after the completeness row.

### 2026-07-30 — corpus v1.27.0
Checkpoint 1.5 built and signed off, and with it **Phase 1**. The
corporate-action mint, the lineage walk, and identity's fifth component. 378
tests, 253 across twenty-seven fixtures. Every Phase 1 obligation row closed;
thirteen carried obligations stand, five of them Phase 3's.

The fifth identity component touched six sites where three were known, and
the two beyond the anticipated fourth are the instructive ones: the
`Contract` record lost its deliverable copy the moment identity carried the
fact, and `ToString` had to render the component so two identities differing
only in deliverable cannot stringify identically, which the identity-order
fixture pins.

The mint is atomic both ways and observed: an adjustment that changes
nothing writes nothing, a successor collision rolls the already-inserted
event row back, and the predecessor reads back byte-identical by row
comparison. D-W36's tripwire is exercised where it matters, the stated
strike passing through the refusing decimal path inside the writer.

The alias convention's dated cost resolved as the 1.1 pin predicted: the
lineage walk shipped as the recursive CTE, no self-join was needed, and the
convention survived the one query that appeared to forbid it, four
checkpoints after the appearance.

One drift owned at this sign-off: the document-level build-state marker in
BUILD_PLAN went stale at 1.2 and three sign-off passes updated only the
phase marker beneath it. Nothing inherited the error; the marker is
corrected, and the archived prompt now carries the check that would have
caught it.

Phase 1 delivers a store that holds market data as it was known at any
instant, membership as state, chain ingest through a refusing decimal seam,
and corporate actions as stated successors with resolvable lineage, on
synthetic chains throughout, which is what it said it would deliver.

### 2026-07-30 — corpus v1.28.0
Phase 2's detail authored, five checkpoints: the worked example reconciled,
enumeration and membership, the contract constraints, the portfolio
constraints, and the feasible set. All three of the phase's carried
obligations are preconditions rather than work items, which is why 2.1 is a
checkpoint that writes no code: the same shape as the membership schema
blocking 1.3, one phase later and three times over. The nine Phase 2
fixtures move to checkpoint granularity; thirteen rows stay at phase
granularity across Phases 3 to 9, correct while their detail is unwritten.

The marker drift got its structural fix rather than better synchronisation.
BUILD_PLAN's header and README both restated phase build state and both now
point; each phase's state is written once in its own section, and the
present state twice by design, in this log's Current state and the
archive's. The sweep the fix demanded found two more stale claims:
SYSTEM_DESIGN said "not built" as a whole while promising per-section
markers it never gained, and ORIENTATION said nothing had shipped five
checkpoints after the store beneath it had. Both now state what is true,
and SYSTEM_DESIGN's §3.1, §3.2 and §8 carry markers enumerated from the
delivered work. §5 was deliberately left unmarked: its two clocks are the
fast and slow loops, which nothing has built, and GLOSSARY already records
that 0.5's clock is unrelated, so marking it would have asserted the exact
collision the glossary entry exists to prevent.

### 2026-07-30 — corpus v1.29.0
Checkpoint 2.1. The worked example's chain is reconciled with the gate it
teaches. The three surviving strikes' asks tighten to spreads of 6.12 to
7.02 percent of mid against the twelve percent cap, where 45.00 failed at
18.18 and 47.50 passed by three hundredths; the ladder extends downward at
its 2.50 spacing with a 42.50 failing the spread cap alone at 37.84 percent
and a 40.00 failing the premium floor alone at half of it; 52.50 and 55.00
stand unchanged as the two-reason case. No bid changed, so nothing
downstream of the fill moved and the feasible set is still the three the
later sections depend on. §3's banner comes down and its table carries
every constraint operand and every failing reason; the expiry window and
earnings clearance, which one snapshot cannot demonstrate, are stated as
checkpoint 2.3's. The v1.6.0 obligation, the table's oldest row, is
discharged: thirteen become twelve.

Both pinning fixtures passed with zero test-code change, which is the
oracle working as designed: it pins the symbol, the date, the expiry and
the right, and parses everything else from the document.

Two corrections rode along. `ContractQuote.cs` and the synthetic chain's
delta comments deferred to Phase 2 a question D-W23 had settled the day it
was written, the ceiling comparing absolute delta; both corrected, comment
only [BE1]. And 1.28.0's record said 2.1's detail states that it registers
no fixtures when the detail did not say it; the sentence is now on disk
where the record claimed it was.

### 2026-07-30 — corpus v1.30.0
Checkpoint 2.1 signed off. `prompts/spent/phase-2.md` opens with 2.1's
prompt and takes over the description of the present, and `phase-1.md` is
closed with its Current state frozen, stamped both with Phase 1's closing
version and with the version at which it stopped describing the present.
2.1's detail is reconciled against what shipped.

The archive's own build-state claim was wrong in both closed files. Each
opened by saying its Current state was the whole state of the repository and
the only description of the present, which stops being true the moment the
next phase's file opens, and `phase-0.md` had carried that sentence directly
above its own **Frozen** marker since 1.1's sign-off. Both now point at the
open phase's file. This is the same defect 1.28.0 fixed in the BUILD_PLAN
header and README, in the one pair of documents that round did not sweep,
because the sweep was for phase build state and this is present state. The
two designated present-state records were the thing being counted, so the
count was right and one of the two was lying about which it was.

### 2026-07-31 — corpus v1.31.0
Checkpoint 2.2, the first Phase 2 checkpoint that writes code, and the
fixture-naming rule narrowed. The generator enumerates and stops there: a
symbol, a simulated date and a position state in, contracts in identity order
out, filtered on membership and position state and on nothing else. 401 tests,
up twenty-three; schema unchanged at 5; no table, no decimal column and no
config key, because enumeration reads no bound.

Three decisions the detail's one sentence had left open are recorded in it. A
candidate carries the quote and declines the four economics columns, since the
quantity that computes committed capital is the open Phase 3 obligation and
building it here means choosing between the multiplier and the deliverable at
the checkpoint with no reason to. One simulated date reaches four parameters
across two reads, written out at the call sites because the two axes exist in
order to be able to differ. Enumeration is broad on purpose, which is what
leaves the gate a record to be audited against.

Two things review found that the detail had not thought to ask for. The
per-symbol membership read had to be unable to disagree with the set read
structurally rather than by agreeing across chosen cases, so one ranking serves
both; measuring then chose its form, because holding the text literally
constant through an `OR` on a parameter's nullness costs the index seek, `SCAN`
where the direct predicate gives `SEARCH`. And every chain in this repository
is puts, so three of the four position states were reported and none was
tested; a two-right chain fixed that, and reintroducing the defect showed five
of ten cases failing against a state-blind enumerator and four of four against
a membership read that always answers yes.

`CLAUDE.md` §5's rule against naming fixtures was followed in neither of its
two halves, because one of them was wrong: a spent prompt is frozen and records
what was asked, so naming fixtures there cannot go stale, where a checkpoint
detail is read for years and gains fixtures after it is written. Narrowed to
detail. Its predicted failure had already happened a second time, in 2.2's own
detail, which named one of its two fixtures.

The Source column was then audited against what each fixture reads, and the
first reading of it was wrong. `authored` does not mean "parses no document":
the configuration and registry fixtures parse four corpus files and are
correctly `authored`, because the column records whether the worked example is
the origin of the expectations. On that reading one further cell disagrees,
`FX-WorkedExampleChainPersists`, and `WORKED_EXAMPLE.md` §10 does not: it
claims only that its seven rows are registered, and rule 4 says adding a
fixture requires no propagation. A claim this log carried in draft, that §10
had gone silently incomplete, was withdrawn on that reading before it was
recorded here.

### 2026-07-31 — corpus v1.31.1
Two citations corrected, both raised by 2.2's audit and neither belonging to
that checkpoint, so they land after it merges rather than inside it.

`VALIDITY.md` §7 item 1 named `FX-PreRegRequired` for a risk
`FX-LearningBoundaryLagRespected` guards. Fifth instance of the citation
pattern, and the first inside a checklist that says of itself that it is
reviewed at every phase sign-off, so this one had been read past repeatedly
rather than sitting unread. The survival mechanism is worth recording: the
sentence gestured at the right test as "the boundary test in Phase 7" while
naming the wrong one, so a reader checking the clause found a true statement
beside the false one and stopped.

`FX-WorkedExampleChainPersists`' Source said `authored` where its expectations
are read out of the document through the same oracle calls its sibling makes.
Measured before the fix: nine rows named the worked example as their origin and
three fixtures reached it through `WorkedExampleOracle`, of which two said
`authored`, one corrected at 1.31.0 and one here. The cell was wrong more often
than right among the fixtures the column exists to describe, which is what
turned a second fix into a stated convention: Source records where expectations
come from, not whether a document is parsed. Ten rows name it now.

The convention had never been written down and was recovered by auditing the
column against what each fixture actually reads. An audit that started from the
plausible reading, that `authored` means no document is parsed, produced eight
false findings against the configuration and registry fixtures before the real
rule surfaced.

### 2026-07-31 — corpus v1.32.0
Checkpoint 2.3, the four contract constraints. Each reads its bound as of the
simulated date and none from a constant, which is the checkpoint's own
definition of done. 455 tests, up fifty-four; schema unchanged at 5; four
config keys added and the six gate bounds' consumers verified.

Two of the four families needed a decision before they needed code. The gate
could not reject a crossed quote on any ground D-W22 stated: that decision gave
a spread above the cap and a bid below the floor, and a crossed quote is
neither, its spread being negative and its bid high. Rejecting on it would have
been this corpus's citation pattern for the sixth time and the first instance
created rather than inherited, so the decision was amended before the code
cited it. Then two boundaries turned out unstated, D-W25's buffer edge and
D-W24's window, the latter resting on the convention that a range includes its
endpoints, which is exactly the convention the gate does not use. Four
decisions were touched where the detail anticipated none.

Earnings clearance was three builds rather than one constraint. The table had
existed since migration 3 with nothing reading it and nothing writing it, and a
synthetic chain could not express a report date, so the constraint was
untestable before a format, a writer and an as-of read existed. The session
vocabulary was authored fresh: a sweep for its values across the documents, the
source and the synthetic files returned only matches inside a SQLite binary.

The crossed-quote obligation closed, and the loader's refusal moved to the
gate. That refusal was right about the risk and wrong about the venue, because
Phase 8's ingest reaches the store without passing the loader, so a rule living
there would have been absent exactly when real data arrived.

Section 3's verdicts were written at 2.1, before any gate existed, and held on
the first run of the fixture that tests them.

Ten mutations were run and none passed everything, but D-W37's took two
attempts and the first result was misleading. Defaulting either half of the
bound resolution passed every test, because each half left the other still
raising. A mutation confined to one site is not a mutation of the behaviour, so
a passing mutation means either a weak test or an insufficient mutation, and
the two are told apart only by defeating every site the behaviour has. The same
class-versus-instance error appeared four times this checkpoint, in three
searches and that mutation; what caught the ones that were caught was a
compiler or an assertion counting, not a better search.

### 2026-08-01 — corpus v1.33.0
Checkpoint 2.4, the three portfolio caps and the gross-basis rule. 493 tests,
up thirty-eight; schema unchanged at 5; four config keys added and their
consumers verified; forty-two registry entries implemented against 0.2 to 2.4,
counted from disk. The gate is whole apart from assembly, and both families can
now fail one candidate.

**The obligation was four keys rather than three, and finding that was the
checkpoint's first work.** Every cap is a fraction of an account value that no
key, column or table held: `WORKED_EXAMPLE.md` §1 states 100,000 in prose and
nothing read it. It is configuration rather than a derived figure, and D-W11's
own rationale is the argument, since a denominator computed from the run's own
state moves with the run and a drawdown would loosen every cap at the moment it
should bind. The detail asking for three fractions was corrected before the code.

**Three of the four values are transcribed and only one is chosen, which is 0.8's
distinction applied rather than restated.** §1 states the equity and both cap
percentages and derives §3's headrooms from them. The simultaneous-assignment
fraction is the choice, and its reason is arithmetic rather than appetite: a
cash-secured put's committed capital is its assignment exposure, so a lower value
makes the total cap unreachable and a higher one never binds. 0.8's argument for
leaving these keys was about who decides, and it does not stop the decided values
coinciding with the example's. Calling them proposals would have been that
distinction inverted.

**Two kinds of vacuity face in opposite directions and neither implies the
other.** A cap tested against an empty portfolio passes whether or not it works,
which is 1.1's empty-table shape, so every cap fixture carries a book and turns
it off to show the exposure reaches the constraint. A cap whose bound is never
reached also passes whether or not it is wired, and §1 derives two headrooms
where only the per-name one reaches §3, so a total cap reading the wrong exposure
reproduces §3 exactly. Both headrooms are now asserted through the functions the
constraint compares against.

**One mutation of thirteen passed everything, and the seeded values were what
hid it.** The assignment limit reading `TotalCapFraction` instead of its own key
is undetectable while the two are held equal, and nothing forbids an operator
setting them apart, since no invariant ties them and the relationship changes at
Phase 3. Two assertions were added at a configuration the store does not hold and
could, and the verdict half is inside `FX-AssignmentStressRejects` rather than an
unregistered suite: it is the only assertion telling two constraints apart, so it
belongs where the registry points. `CONFIG_REFERENCE.md`'s own Notes now say the
indistinguishability is a condition of the equal fractions rather than a fact
about the caps, because an operator revising one reads that row and not the code. That is 2.3's lesson one level up: there a mutation confined to one site
left another site still raising, and here a value equal to its neighbour left a
field unreadable from any verdict. Defaulting only the decimal half of the shared
bound resolution reproduced 2.3's original result exactly, the integer half
keeping `GateBoundsTests` green.

**The citation pattern reaches six, and the method is now the finding.** §3 cited
the per-name headroom to D-W10, which states where the gate lives, where every
other constraint in that sentence cites the decision that states it. Found the
same way as the other five, by building the thing that rests on the citation.
Reading the documents alone has found none of the six.

A seventh was created and caught inside the same checkpoint, which is the first
time that has happened to a citation this build made rather than inherited. The
plan for 2.4 cited D-W25 as the precedent for stamping an amendment, and D-W25 is
one of three decisions 2.3 amended and did not stamp. D-W22, D-W24 and D-W25 now
carry their stamps, so the register shows four decisions moving where it showed
one.

**A definition of done can name a later checkpoint's subject, and 2.4's did.** It
required a cap to be evaluated against committed capital as the store records it,
and nothing persists at 2.4. Restated as the property it was protecting, one
computing site. Third instance after 0.6's and 2.1's, and the tell is grammatical
rather than technical: the clause described a state where the others describe an
act, and a state can belong to a checkpoint the clause does not.

D-W19 gains its boundary. It said "above basis" and left a strike exactly at
basis unsaid; a strike at basis recovers the outlay exactly, so excluding it
would forbid the break-even strike for no stated reason. §3's verdicts, written
at 2.1 before any gate existed, held on the first run of the completed gate with
nothing stripped.

### 2026-08-01 — corpus v1.34.0
Checkpoint 2.5 built and signed off, and with it **Phase 2**. The feasible set:
assembly, ordering, and the record of what the gate refused. 503 tests, up ten;
schema unchanged at 5; no config key, 2.5 introducing none; forty-three registry
entries implemented against 0.2 to 2.5, counted from disk. Thirteen carried
obligations stand and Phase 2 owes nothing.

**The smallest checkpoint of the phase found the defect that mattered most.**
`GatedCandidate`'s synthesised equality compared its reasons by reference, so two
candidates with the same contract and the same reasons in the same order were
unequal whenever the lists were separate instances, which is every time the gate
runs twice. D-W4 asks exactly that question and the type answered it wrong. It
surfaced from asserting that one evaluation repeats, which is 2.5's whole
definition of done; the alternative venue was Phase 4's
FX-ThreeMakersSameFeasibleSet, where it would have presented as a difference
between three makers that did not exist, which is the misdiagnosis D-W26's
rationale describes in a different register.

**Ordering needed a chain the repository did not have.** Identity orders on
expiry before strike and every chain here is one expiry, so no fixture could tell
an identity comparison from a strike comparison; the ordering suite supplies two
expiries scrambled. Removing the chain read's sort then failed exactly one test,
the read's own, because the store returned identity order on that chain anyway.
Which access path produced that was not measured, and it is recorded as unmeasured
rather than explained. The division is the intended one: the read owns the sort
and the test holding it, and the gate asserts the property it inherits rather
than restating the guarantee.

**A fourth outcome for a mutation, distinct from the three causes of a passing
one.** Making the gate re-sort its own output passed everything, and that is the
correct result: the sequence is already in that order, so the mutation changes no
behaviour. A null mutation is evidence rather than a gap, and telling it from a
weak test, an insufficient mutation and an unfalsifiable suite is a fourth
question to ask, not a fourth failure.

**The definition of done was wrong twice in one clause.** It asked for bytes
where nothing serialises a candidate, and it called 2.5 the first consumer of the
total order 1.5 completed when `AsOfMarketData.QuotesFor` has consumed it since
1.2. Both corrected before the code. That is the fourth definition of done whose
subject belongs to a later checkpoint, and correcting it found that v1.33.0's
record of the same class named the wrong earlier instance: it said 0.6 and 2.1
where 2.1's clause named two fixtures that both existed. The series is 0.5, 0.6,
2.4, 2.5, and the correction is recorded here rather than by editing v1.33.0,
which this log does not do.

Two Phase 4 obligations turned out to be one schema decision in two places: the
grain a feasible set is stored at, and how a set of gate reasons reaches one
column. They are not merged, two rows raised for two reasons at two versions
being the history, and each now names the other. That is this corpus's pointer
rule reaching a register, where it has until now applied to a fact stated twice.

Phase 2 delivers one feasible set per name and date, produced by enumeration and
filtered by a gate that sits inside the generator with every rejection reason
recorded, on synthetic chains throughout, which is what it said it would deliver.
Nothing persists a candidate, and no decision has been made.

### 2026-08-01 — corpus v1.34.1
`WORKED_EXAMPLE.md`'s build-state marker corrected. Raised by 2.5's marker sweep
and belonging to no checkpoint, so it lands after that one merges rather than
inside it, on v1.31.1's precedent.

The marker read **not built** while §2, §3 and §5 were reproduced end to end,
§1's caps had become configuration, and six of the eleven fixtures naming the
document were implemented. It was not stale so much as answering the wrong
question. This document is a specification expressed as arithmetic, so what gets
built is never the document but the machinery that reproduces it; "not built" was
true of the document and silent on that, which is why five checkpoints built
against it without anyone reading the marker as wrong.

**A marker whose sense differs from every other one has to say so.** Elsewhere a
marker describes the thing its section describes. Here it describes a second
thing entirely, and a reader applying the usual reading gets a true sentence and
the wrong picture, which is the same survival mechanism v1.31.1 recorded for a
citation: a true statement beside a misleading one stops the reader checking.

### 2026-08-01 — corpus v1.35.0
Phase 3's checkpoint detail authored, five checkpoints: the mechanics settled
before the machine, the completeness pass, the state machine and the ledger, the
fill model and the costs, and determinism end to end. Documentation only; no
code, and the suite is unchanged at 503.

**Two of the five write no code, and the reason is the phase's load rather than
its shape.** Eight of the thirteen standing obligations are owed at Phase 3, and
five of those eight determine what the state machine does rather than sitting
inside it: which quantity committed capital uses, what a covered call commits,
how dividends are recorded, the settlement mechanics, and the completeness pass
itself. A precondition answered late is a schema or a transition built twice,
which is why 3.1 and 3.2 come first. That is the shape membership's schema had at
1.3 and the worked example had at 2.1, at the largest scale it has appeared: one
phase, three times over, and now five.

**The mechanics are transcribed from OCC rather than recalled**, on D-W36's
precedent. This project reasoned twice from a secondary source about contract
adjustment and was wrong both times, so 3.1 requires each answer to cite a
primary rule and 3.3's definition of done is that every transition cites a
decision settled at 3.1.

3.4's test line called the assigned-trial fixture the fourth pin on
`WORKED_EXAMPLE.md`, and it is the seventh fixture to read that document and the
first to read its ledger. Corrected before the detail landed rather than after,
because a checkpoint's test line is acted on rather than read past.

**A figure carried forward from when it was true rather than measured when it was
written.** That is the shape, and it is the reason this corpus keeps removing
counts and ordinals from prose that would otherwise have to be revisited every
time the thing beneath it moves. The number of instances is the one figure this
entry does not assert.

### 2026-08-01 — corpus v1.36.0
Checkpoint 3.1 opened, and two of its seven mechanics settled. D-W38 resolves
expiry by exercise at one cent in the money; D-W39 places assignment after a
session's close and the account's knowledge of it the next morning. Two fixtures
registered against 3.3, where the transitions land. Documentation only; no code,
and the suite is unchanged at 503.

**The branch is deliberately unmerged, and that is a state rather than a stall.**
3.1 signs off when all seven are settled, and a half-settled 3.1 on `main` would
leave the corpus asserting two mechanics and silent on five with nothing marking
the difference. This entry is what makes the open state legible while it sits.

**What was retrieved.** The Options Industry Council's exercise and assignment
references, on 2026-08-01, cited in both decisions under headings that say what
they are. That is 3.1's requirement of OCC's own rules met by disclosure rather
than by claim: OIC describes OCC's procedure, and D-W38 states what that
procedure does not establish, which is that the one-cent threshold runs between
OCC and its clearing members rather than binding an account.

**What was not, and why.** Measured on 2026-08-01: `theocc.com` and
`infomemo.theocc.com` both return 403 to this environment while `sec.gov` serves
the filings, so primary rule text arrives through SEC rule filings and the
contract adjustment memos do not arrive at all. Five items stay open.

**A second route, recorded because it reached what the first could not.** A rule
whose text no SEC filing exhibit reproduces can still be read in the Federal
Register notice that approved it, and `govinfo.gov` serves those as GPO's own
text where `federalregister.gov`'s full-text path returns a bot wall. That is how
D-W38's citation moved from a characterisation of Rule 805 to the filing that set
it, which is the one thing 3.1 had recorded as unverifiable from here.

**The authority covers the clearing layer and the account layer is convention.**
Three mechanics now show it. OCC's one-cent threshold runs between OCC and its
clearing members and does not bind an account; OCC assigns exercise notices to a
Clearing Member while Rule 804 leaves the choice of writer to that member's own
fixed procedures, with the method deliberately outside Rule 803 since
SR-OCC-95-16 removed it; and no rule fixes when a customer is told. So a decision
that spans both layers cites two authorities or states that it has one, and the
lab's account-level behaviour is a model recorded as a model rather than a rule
transcribed. That is the same disclosure in three places, and it is what the
sourcing requirement buys that a secondary description would have hidden.

- **Committed capital's quantity.** The reverse-split half is settled: a 3-for-4
  reverse on a $50 option adjusts the deliverable to 75 shares with the strike
  unchanged, which preserves the aggregate exercise only as strike times
  multiplier. The forward half waits on the memo, since the older method reduced
  the strike as well and whether the current one does is what the memo settles.
  [D-W17] is not amended in either direction until it lands, and D-W36's figures
  are not reasoned from, their provenance being a search result rather than the
  memo.
- **T+1 cash availability.** Unretrieved. Its source is SEC Rule 15c6-1 as
  amended, and it decides whether a trial can open the next day or the day after.
- **Early assignment around ex-dividend.** Unretrieved, and it splits: the
  allocation mechanics have a primary source in OCC's rules, and whether a holder
  exercises early has none by nature, being that holder's choice. `VALIDITY.md`
  already names this as modelled by rule, so what is owed is the rule and its
  source rather than a decision about whether to model it.
- **Dividend entitlement timing, and whether a dividend enters `ledger_entries`
  and the buy-and-hold control** [D-W13]. Unretrieved, and the largest of the
  five, since the answer decides a ledger kind and a control's return. The
  entitlement half's source is unknown until retrieved; whether a dividend enters
  the ledger has no external source at all.
- **What a covered call commits.** Depends on the first three and goes last. No
  external source: it is a modelling choice about [D-W17]'s committed capital.

**Which of those cite an authority and which are chosen is stated before
retrieval rather than after**, because that is what stops the last one arriving
dressed as a fact. It is the distinction 2.4 drew between a transcribed value and
a chosen one, moved from numbers to mechanics.

**D-W39's second date lands in the schema now rather than at 3.3**, because the
decision determines it rather than leaving it open. [D-W35] makes `trials` and
`positions` projections rebuildable from `ledger_entries`, and a projection
cannot carry what its source lacks, so `known_on` is deduced onto the ledger
rather than chosen for it, and `positions` does not gain it for the same reason.
The table is Phase 3's and does not exist, so this is the schema being right
before it is built rather than a change to one.

**The nullability convention, measured because the new column turned on it.**
Nullability drift has been corrected three times: ten columns at 1.1, one at 1.3,
and every correction added a `NULL` marker. Section 4 holds 121 columns, of which
27 are marked `NULL` and 94 are silent, and no column anywhere in the document
marks non-nullability. Eleven of the eighteen tables are mixed and 64 of the 94
silent columns sit in one, so an unmarked column beside a marked one is what the
convention produces rather than an anomaly. `known_on` is silent for that reason
and not by omission.
