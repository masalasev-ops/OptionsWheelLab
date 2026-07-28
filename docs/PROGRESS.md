# PROGRESS

Appended to, never rewritten. The repository is the authority on build state.

## Current state

**Phase 0 in progress.** Checkpoints 0.1 and 0.2 built. 0.3 onward not started.
The documentation corpus is at v1.9.4.

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
