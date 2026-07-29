# PROGRESS

Appended to, never rewritten. The repository is the authority on build state.

## Current state

**Phase 0 complete.** Checkpoints 0.1 to 0.8 built and signed off. Phase 1
detail not written. The documentation corpus is at v1.15.1.

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
