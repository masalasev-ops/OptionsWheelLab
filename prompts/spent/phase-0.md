# Phase 0 Foundations: spent prompts

Every prompt spent while Phase 0 was in flight, in the order it was spent. A
prompt is filed under the phase in flight when it was spent, not the phase whose
code it touched, so filing needs no judgement.

**Current state** below is overwritten by every prompt and is the only part of
this file describing the present. It is the whole state of the repository, read
in one pass without opening another document.

Entries carry only what cannot age: **Asked**, the prompt verbatim, and
**Delivered**, the commits and every deviation from the ask. Both are statements
about a moment that has passed and stay true forever. No entry describes the
present, so no entry goes stale. Entries are appended and never edited.

One file per phase rather than one per prompt, because a directory of forty
single-prompt files buries the thing being looked for. The file closes when
Phase 0 signs off; Phase 1 opens its own.

## Standing instructions

Given during Phase 0 and applying to all work from the point they were given.

- Commit subjects are prefixed with the phase name and stage, as
  `Phase 0 Foundations / 0.2 - <type>: <subject>`.
- The pull request description is updated on every check-in, not only at the
  end.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

---

# Current state

As of prompt 6. Corpus v1.9.5.

## Build

| | |
|---|---|
| Phase 0 | 0.1 and 0.2 built; 0.3 onward not started |
| Branch | `phase-0/checkpoint-0.1-0.2` |
| PR | #1 open, not merged; `main` holds the documentation corpus only |
| CI | green, 36 tests, restore and build and test on push to `main` and every pull request |

.NET 10 solution, nullable enabled, warnings as errors, central package
management: `OptionsWheelLab.Core` holding the composition root and options
types, `.Worker` and `.Api` as thin hosts both calling it, and `.Tests`.

One shared `src/appsettings.json` linked into both hosts and the test project,
loaded from `AppContext.BaseDirectory` because the generic host and the web host
default their content roots differently. No `Logging` section is committed, so
every top-level section must bind and the binding test needs no framework
allowlist. `appsettings.Secrets.json` is gitignored with a committed empty
`.example` and loads optionally, so a fresh clone builds without it.

## Configuration

One section bound: `Eodhd` to `EodhdOptions`, verified by reading the
composition root.

Six sections deliberately unbound because `CONFIG_REFERENCE.md` classes them
`rows` and a registered options type is itself a current-value accessor: `Risk`,
`Gate`, `Costs`, `Policy`, `Trial`, `Scoring`.

`CONFIG_REFERENCE.md` carries 26 key rows, one key per row. Three Consumer cells
are verified as `Ingest via EodhdOptions`; 23 carry **Unverified**. No value is
set that the document marks unset.

## Tests

36 across six fixtures plus the 0.1 smoke test.

| Fixture | Tests |
|---|---|
| FX-ConfigStoreClassHonoured | 12 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-EveryConfigSectionBinds | 6 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-RegistryMatchesDisk | 1 |

All six fixtures registered against 0.2 are implemented and named for their
registry entry. The suite parses `CONFIG_REFERENCE.md` and `FIXTURES.md`, so
both are load-bearing rather than descriptive: an edit breaking their table
shape fails the build.

The two cross-key invariants are pure predicates in `Core` over supplied values,
with no host, no config store, no startup wiring and no clock.

## Layout

Repository root holds `README.md` and `CLAUDE.md` only. Every other document is
in `docs/`. Spent prompts are in `prompts/spent/`.

## Not built

The store, migrations and `migrate.ps1`. The config read service and its as-of
resolver. The deterministic clock. Money and ticker primitives. The append-only
CI greps. Every checkpoint from 0.3 onward.

## Open

- **Phase 11**: re-add `Microsoft.AspNetCore.OpenApi` against a version whose
  `Microsoft.OpenApi` dependency clears the audit. Version 10.0.9 pulls
  `Microsoft.OpenApi` 2.0.0, carrying advisory GHSA-v5pm-xwqc-g5wc. Recorded in
  `BUILD_PLAN.md` carried obligations.
- **0.8**: wire the two cross-key invariants to the config write path, and
  FX-ConfigWriteRefusesInvariantBreach.
- **0.3**: the config read service and its as-of resolver, FX-ConfigResolvesAsOf
  and FX-NoCurrentConfigReadOnSimulatedPath.
- No blockers.

---

# Entries

| # | Prompt | Checkpoints | Commits |
|---|---|---|---|
| 1 | Repository skeleton and configuration binding | 0.1, 0.2 | 13 |
| 2 | PR #1 review fixes | 0.2 | 7 |
| 3 | CONFIG_REFERENCE shared-row split | 0.2 | 2 |
| 4 | BUILD_PLAN reconciliation | 0.2 | 1 |
| 5 | Spent-prompt archive | 0.2 | 1 |
| 6 | Stale-free archive restructure | 0.2 | 1 |

---

## 1. Checkpoint 0.1 + 0.2, repository skeleton and configuration binding

Spent 2026-07-27.

### Asked

> CHECKPOINT 0.1 + 0.2 — Repository skeleton and configuration binding
> Repo: OptionsWheelLab (new, empty).
>
> Read before starting, in this order: CLAUDE.md, README.md, SYSTEM_DESIGN.md
> (§7 phase map), BUILD_PLAN.md (§0.1, §0.2), CONFIG_REFERENCE.md, FIXTURES.md.
> CLAUDE.md is binding for how you work, not only for what you build.
>
> Rules from CLAUDE.md that bear directly on this checkpoint:
> - §1 Verify before asserting. Any claim about what a section binds to is
>   confirmed by reading the composition code, not by a grep for the key name.
> - §3 CONFIG_REFERENCE.md records the VERIFIED consumer, meaning you read the
>   composition and confirmed the binding. Correct any row you find to be wrong.
> - §3 No magic numbers at call sites. A value that could plausibly be tuned is a
>   config key with an entry in CONFIG_REFERENCE.md.
> - §4 No whole-file rewrites. Churn commits separate from content commits.
> - §5 Fixtures are referenced from FIXTURES.md, never enumerated inline. Read
>   the assertions from that file.
> - §8 Distinguish a blocker from a finding when you report.
>
> 0.1 Repository skeleton
> - .NET 10 solution: OptionsWheelLab.Worker, OptionsWheelLab.Api,
>   OptionsWheelLab.Core, OptionsWheelLab.Tests.
> - CLAUDE.md at repo root. Docs corpus under docs/.
> - appsettings.Secrets.json in .gitignore, with appsettings.Secrets.example.json
>   committed alongside carrying empty values and no real credentials.
> - CI workflow running restore, build, test on a clean checkout.
> - Test: solution builds; a trivial test passes in OptionsWheelLab.Tests.
> - DoD: CI green on a fresh clone with no local state.
>
> 0.2 Configuration binding
> - Every appsettings section binds to a typed options class registered at
>   composition. Create the options types for the appsettings-shaped sections in
>   CONFIG_REFERENCE.md, leaving values unset where that document marks them
>   unset. Do not invent values.
> - Implement the fixtures registered against checkpoint 0.2 in FIXTURES.md.
> - The binding test must ENUMERATE the options types registered in composition
>   and fail on any section present in appsettings.json that binds to nothing. A
>   section that binds to nothing is the defect this checkpoint exists to prevent,
>   so a test that only asserts known sections populate does not satisfy it.
> - DoD: the test fails when a stray section is added to appsettings.json and
>   passes when it is removed. Demonstrate both.
> - DoD: CONFIG_REFERENCE.md updated so every key you bound names its verified
>   consuming type.
>
> Out of scope for this checkpoint
> - The store, migrations, and any table. That is 0.3.
> - The config_rows-backed configuration path and the as-of resolver. That is 0.3
>   and D-W26. Do not introduce a current-value config accessor that 0.3 would
>   have to remove.
> - Setting values for any key CONFIG_REFERENCE.md marks unset. Those are 0.8.
>
> Constraints
> - No DateTime.Now or DateTime.UtcNow. The clock abstraction lands in 0.5; do
>   not introduce a call that will need removing.
> - No double or float in any type that will carry money.
>
> Stop and ask rather than guess
> - CONFIG_REFERENCE.md does not state, per key, whether it is appsettings-bound
>   or config_rows-backed. Where that is unclear for a key, stop and report it as
>   a blocker rather than choosing. Guessing wrong here is expensive: an
>   appsettings-bound value is not as-of resolvable at all [D-W26], so the choice
>   determines whether a later replay can reproduce the session.
>
> Report at the end
> - HEAD sha and the commit list for this checkpoint.
> - Projects created.
> - Every appsettings section bound, with its verified consuming type, and any
>   CONFIG_REFERENCE.md row you corrected.
> - The two demonstrations required by the 0.2 DoD.
> - Blockers separately from findings.

### Delivered

On `main`: `dbfdf87` corpus baseline, `9dd9866` gitignore and gitattributes.

On the branch: `c7df333` solution and projects, `0b9eea5` CI, `3a26a1b` smoke
test, `241d581` composition binding, `31f9ed9` FX-EveryConfigSectionBinds,
`b03a633` FX-ConfigStoreClassHonoured, `2c4179e` invariant predicates, `e62fd83`
the two predicate fixtures, `55f84d8` verified consumers, `b637a3e` PROGRESS,
`7071b02` CI actions off Node 20.

Deviations from the ask:

- The prompt instructed stopping on any key whose storage class was unclear.
  That blocker was raised and resolved by the operator as D-W27, which arrived
  mid-prompt along with D-W26. The corpus moved v1.6.0 to v1.8.3 during
  execution.
- D-W26 was first reported as absent from the register. That was wrong: the grep
  was scoped to the repository directory while the tree was stale. Withdrawn
  before any code was written.
- `Microsoft.AspNetCore.OpenApi` was removed rather than referenced, because its
  transitive `Microsoft.OpenApi` failed the build's vulnerability audit.
  Suppressing the audit to keep a package with no endpoint to describe was
  refused.
- `prompts/spent/` was not created, though `BUILD_PLAN.md` described it.
  Reported as a finding.

---

## 2. PR #1 review fixes

Spent 2026-07-28.

### Asked

Two parts. Part 1 supplied seven documentation edits verbatim, to be transcribed
rather than authored: `FIXTURES.md` rule 2 split by direction with
FX-RegistryMatchesDisk moved from 0.6 to 0.2 and FX-EveryBoundKeyIsDocumented
registered at 0.2; `BUILD_PLAN.md` carried obligations and an added 0.2
definition of done; `CLAUDE.md` 4a on dependencies and a 10 clause on two-author
documents; `README.md` corpus layout; `CONFIG_REFERENCE.md` Consumer column
defined as `component via TypeName`; `CHANGELOG.md` 1.9.1; `PROGRESS.md` v1.9.1.

Part 2 supplied six code items, each with its test and definition of done:

> C1 Store-column parser must fail on an unclassified row. ConfigReferenceParser
> skips any row whose Store cell is not exactly `rows` or `app`, so a malformed
> cell such as `**rows**` drops that key from the contract silently.
>
> C2 The committed secrets example is inside the binding check.
> FX-EveryConfigSectionBinds reads only src/appsettings.json, so a section in
> src/appsettings.Secrets.example.json binding to nothing is invisible.
>
> C3 FX-EveryBoundKeyIsDocumented, the third direction. For every BoundSection,
> walk the public settable properties of its options type, composing the key path
> from section path and property name, recursing through nested options classes.
> Report a judgement, do not build it: the fourth direction, a documented key
> that nothing binds, would fire on every rows-classed key by design. Say whether
> it is worth having at all.
>
> C4 FX-RegistryMatchesDisk at 0.2, file-to-entry only. Do NOT implement
> entry-to-file as a standing assertion; it is now a per-checkpoint DoD.
>
> C5 RepoRoot must survive a solution file rename.
>
> C6 Corpus layout. git mv ORIENTATION.md and PROGRESS.md into docs/, in a commit
> touching nothing else.
>
> Not in scope: the OpenApi removal stands. It is a Phase 11 carried obligation
> now. Do not re-add it here.

### Delivered

`5473a76` corpus v1.9.1, `31c0407` RepoRoot both filenames, `ca2d4c7` Store
parser fails on unclassified rows, `d67e83b` secrets example inside the binding
check, `394ea7b` FX-EveryBoundKeyIsDocumented, `4c20f2a` FX-RegistryMatchesDisk,
`9ef0575` corpus layout move.

Deviations from the ask:

- C4 was first implemented with an entry-to-file assertion scoped to a hardcoded
  list of landed checkpoints, which the prompt forbids. Removed before
  committing; only file-to-entry shipped.
- A stray `docs/CLAUDE.md` was found, being root `CLAUDE.md` plus the §4a edit
  delivered to the wrong directory. §4a was applied at root per the supplied
  layout rule and the untracked duplicate deleted.
- C3's fourth direction was judged not worth building. It would fire on every
  `rows`-classed key by design, and restricting it to `app`-classed keys only
  duplicates FX-EveryBoundKeyIsDocumented.

---

## 3. CONFIG_REFERENCE shared-row split

Spent 2026-07-28.

### Asked

> CONFIG_REFERENCE SHARED-ROW SPLIT — additional commits on
> phase-0/checkpoint-0.1-0.2, or a follow-up branch if PR #1 has merged.
>
> Why: ConfigReferenceParser yields the first backticked token per key cell.
> Four documented keys sit in shared rows and are invisible to it. Nothing
> fires while only Eodhd binds; at Phase 2 FX-EveryBoundKeyIsDocumented reports
> Gate:MaxDte as undocumented against a key the document contains.
>
> D1 docs/CONFIG_REFERENCE.md — one key per row. Replace the Gate expiry-window
> row with two rows and the three Policy band rows with six, all supplied
> verbatim. Leave every other row untouched, including all **Unverified** cells.
> DoD: 26 key rows where there were 22, and git diff touches only those four
> rows.
>
> D2 docs/CONFIG_REFERENCE.md — state the rule the split enforces: one key per
> row, a row naming two keys leaves the second unreadable to anything parsing
> this document, a suffix-only second token is not a key path at all, and keys
> that constrain each other say so in their Notes instead.
>
> D3 docs/CHANGELOG.md — prepend 1.9.2.
>
> D4 docs/PROGRESS.md — corpus version line to v1.9.2, then append.
>
> C1 Enforce one key per row. Extend FX-ConfigStoreClassHonoured.
> - Test: a key cell containing more than one backticked token fails, naming the
>   row and every token found.
> - Replace the existing A_row_carrying_two_keys_yields_its_section_root test. It
>   asserts correct handling of a form the document now forbids, so it would keep
>   passing while documenting the wrong contract.
> - Test: the real CONFIG_REFERENCE.md passes.
> - DoD: re-joining `Gate:MinDte` and `Gate:MaxDte` into one row in a scratch
>   copy fails the suite. Demonstrate, revert.
>
> Report: HEAD sha, the commits added, the demonstration, and the row count
> before and after.

### Delivered

`b1256f1` one key per row and corpus v1.9.2, `014bfe8` the enforcing test.

No deviations. 22 key rows became 26, the D1 diff touched only the four shared
rows, and the demonstration fired naming both tokens before being reverted.

---

## 4. BUILD_PLAN reconciliation

Spent 2026-07-28.

### Asked

> Based on what has been built so far can you review the Build plan and update
> 0.1 and 0.2 accordingly if needed

### Delivered

`de2b461` reconcile BUILD_PLAN 0.1 and 0.2 with what shipped, corpus v1.9.3.

Deviations from the ask:

- The ask was to update. `CLAUDE.md` §10 reserves checkpoint scope to the corpus
  author, so the amendment was drafted and the conflict raised before applying.
  The operator chose to apply as drafted.
- Nothing in the repository changed. No built thing contradicted the plan; the
  divergences were one false build-state marker and four passages describing
  less than what was built.

---

## 5. Spent-prompt archive

Spent 2026-07-28.

### Asked

Four exchanges, the first three designing the archive and the last authorising
it.

> ok can you give me a preview of how the prompts spent would look like?

> hmm i have different idea. I don't want so many cluttered prompts spent. I want
> 1 file per phase. So phase 0 should be all contained in one prompt spent until
> phase 0 is over and so on

> i want the prompts spent to be structured in such a way as to derive to the
> current state from the prompt in a single take without having to go back and
> forth

> ok do the rule and make the prompt, revise PR and pr desc and push

### Delivered

`2c892c2` spent-prompt archive and corpus v1.9.4, creating
`prompts/spent/phase-0.md` and revising the `BUILD_PLAN.md` prompts rule.

Deviations from the ask:

- The rule prose in `BUILD_PLAN.md` is authored content under `CLAUDE.md` §10.
  It was drafted, shown, and applied on instruction.
- A preview offered one file per prompt. That was rejected for one file per
  phase, and rejected again for referring the reader to `PROGRESS.md` for
  outcomes.
- Entries carried a per-entry state snapshot. Four of the five described states
  that were no longer true, which prompt 6 removed.

---

## 6. Stale-free archive restructure

Spent 2026-07-28.

### Asked

> no i do not want stale lines. It becomes very difficult to navigate. Remove
> every stale like with the truth of the current state.

Following an explanation that four of the five per-entry snapshots described
states later prompts had superseded, entry 1 for example recording 23 tests,
corpus v1.8.3 and 22 key rows, every one of which a later prompt changed.

### Delivered

One commit restructuring `prompts/spent/phase-0.md`, revising the
`BUILD_PLAN.md` prompts rule, and corpus v1.9.5.

The per-entry **State after** and **Open** blocks are removed and replaced by a
single **Current state** section, overwritten by every prompt. Entries keep
**Asked** and **Delivered**, which are statements about a moment that has passed
and cannot age.

Deviations from the ask:

- The instruction read literally would put current state into every entry,
  making five identical blocks. One overwritten section was built instead, since
  the stated problem was navigation.
- This mirrors `PROGRESS.md`, which already carries an overwritten Current state
  above an appended Log, so the shape is the corpus's own rather than new.
