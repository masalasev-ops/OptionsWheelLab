# Phase 0 Foundations: spent prompts

Every prompt spent while Phase 0 was in flight, in the order it was spent. A
prompt is filed under the phase in flight when it was spent, not the phase whose
code it touched, so filing needs no judgement.

Appended to, never rewritten. An entry is written once, when its prompt
completes, and is never edited afterwards. The file closes when Phase 0 signs
off; Phase 1 opens its own.

Each entry is written to be read on its own. **Asked** is the prompt verbatim,
**Delivered** names the commits and every deviation from the ask, **State after**
is an absolute snapshot rather than a change list, and **Open** is what the
prompt left owed.

**Only the last entry describes the present, and only until the next prompt is
spent.** Every earlier snapshot is what was true then. `PROGRESS.md` is the
authority on now. Reasoning about the current repository from an earlier entry
produces confident, wrong findings, which is the failure `CLAUDE.md` §1 exists
to prevent.

One file per phase rather than one per prompt, because a directory of forty
single-prompt files buries the thing being looked for.

## Standing instructions

Given during Phase 0 and applying to all work from the point they were given,
recorded here because they govern how every later prompt is executed.

- Commit subjects are prefixed with the phase name and stage, as
  `Phase 0 Foundations / 0.2 - <type>: <subject>`.
- The pull request description is updated on every check-in, not only at the
  end.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

## Index

| # | Prompt | Checkpoints | Corpus after | Commits | HEAD after |
|---|---|---|---|---|---|
| 1 | Repository skeleton and configuration binding | 0.1, 0.2 | v1.8.3 | 13 | `7071b02` |
| 2 | PR #1 review fixes | 0.2 | v1.9.1 | 7 | `9ef0575` |
| 3 | CONFIG_REFERENCE shared-row split | 0.2 | v1.9.2 | 2 | `014bfe8` |
| 4 | BUILD_PLAN reconciliation | 0.2 | v1.9.3 | 1 | `de2b461` |
| 5 | Spent-prompt archive | 0.2 | v1.9.4 | 1 | this entry's commit |

---

## 1. Checkpoint 0.1 + 0.2, repository skeleton and configuration binding

Spent 2026-07-27. Corpus v1.8.3 at completion.

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

On `phase-0/checkpoint-0.1-0.2`: `c7df333` solution and projects, `0b9eea5` CI,
`3a26a1b` smoke test, `241d581` composition binding, `31f9ed9`
FX-EveryConfigSectionBinds, `b03a633` FX-ConfigStoreClassHonoured, `2c4179e`
invariant predicates, `e62fd83` the two predicate fixtures, `55f84d8` verified
consumers, `b637a3e` PROGRESS, `7071b02` CI actions off Node 20.

Deviations from the ask:

- The prompt instructed stopping on any key whose storage class was unclear.
  That blocker was raised and resolved by the operator as D-W27, which arrived
  mid-prompt along with D-W26. The corpus moved v1.6.0 to v1.8.3 during
  execution.
- D-W26 was first reported as absent from the register. That was wrong: the grep
  was scoped to the repository directory while the tree was stale. Withdrawn
  before any code was written.
- `Microsoft.AspNetCore.OpenApi` was removed rather than referenced. Version
  10.0.9 pulls `Microsoft.OpenApi` 2.0.0, which carries advisory
  GHSA-v5pm-xwqc-g5wc, and the build's audit failed on it. Suppressing the audit
  to keep a package with no endpoint to describe was refused.
- `prompts/spent/` was not created, though `BUILD_PLAN.md` describes it. Reported
  as a finding.

### State after

| | |
|---|---|
| Corpus | v1.8.3 |
| Phase 0 | 0.1 and 0.2 built; 0.3 onward not started |
| Branch | `phase-0/checkpoint-0.1-0.2`, HEAD `7071b02` |
| PR | #1 open; `main` holds the corpus only |
| CI | green, 23 tests |

Solution, .NET 10, nullable, warnings as errors, central package management:
`OptionsWheelLab.Core`, `.Worker`, `.Api`, `.Tests`. One shared
`src/appsettings.json` linked into both hosts and the tests, loaded from
`AppContext.BaseDirectory`. No `Logging` section committed.

Configuration. One section bound: `Eodhd` to `EodhdOptions`. Six sections
deliberately unbound as `rows`-classed: `Risk`, `Gate`, `Costs`, `Policy`,
`Trial`, `Scoring`. `CONFIG_REFERENCE.md` carries 22 key rows, four of which
name two keys each. No value set that the document marks unset.

Tests, 23. Four fixtures registered against 0.2, all implemented:
FX-EveryConfigSectionBinds, FX-ConfigStoreClassHonoured,
FX-CeilingNotInsidePolicyBand, FX-MaxDteBelowTrialBound.

Repository root holds `README.md`, `CLAUDE.md`, `ORIENTATION.md`, `PROGRESS.md`.

Not built: the store, migrations, the config read service and its as-of
resolver, the clock, money and ticker primitives, the append-only CI greps.

### Open

- Phase 11: re-add `Microsoft.AspNetCore.OpenApi` against a version clearing the
  audit.
- 0.8: wire the two cross-key invariants to the config write path.
- `FIXTURES.md` rule 2 has nothing enforcing it.
- Neither config fixture catches a key absent from `CONFIG_REFERENCE.md`.
- `prompts/spent/` does not exist.

---

## 2. PR #1 review fixes

Spent 2026-07-28. Corpus v1.8.3 at spend, v1.9.1 at completion.

### Asked

> PR #1 REVIEW FIXES — one pass, additional commits on
> phase-0/checkpoint-0.1-0.2. Supersedes all earlier prompts in this thread.
>
> Part 1 runs first: the registry edits gate the tests in Part 2.
> All Part 1 text is authored content supplied verbatim; applying it is
> transcription, not authoring, so CLAUDE.md 10 is satisfied.
>
> Part 1 supplied, verbatim, seven documentation edits: FIXTURES rule 2 split by
> direction with FX-RegistryMatchesDisk moved to 0.2 and
> FX-EveryBoundKeyIsDocumented registered there; BUILD_PLAN carried obligations
> and an added 0.2 DoD; CLAUDE.md 4a on dependencies and a 10 clause on
> two-author documents; README corpus layout; CONFIG_REFERENCE Consumer column
> defined as `component via TypeName`; CHANGELOG 1.9.1; PROGRESS v1.9.1.
>
> Part 2, six code items: C1 the Store parser must fail on an unclassified row;
> C2 the committed secrets example belongs inside the binding check; C3
> FX-EveryBoundKeyIsDocumented, walking settable properties of bound options
> types, with a judgement requested on whether a fourth direction is worth
> having; C4 FX-RegistryMatchesDisk at 0.2, file-to-entry only; C5 RepoRoot must
> survive a solution file rename; C6 move ORIENTATION.md and PROGRESS.md into
> docs/ in a renames-only commit.
>
> Not in scope: the OpenApi removal stands, now a Phase 11 carried obligation.
>
> Report: HEAD sha and commits added, the four demonstrations, the C3 judgement,
> and blockers separately from findings.

The full text as supplied is preserved in the pull request thread. This entry
summarises Part 1's seven edits and Part 2's six items rather than restating
them, because each was transcribed verbatim into the corpus and is readable
there.

### Delivered

`5473a76` corpus v1.9.1, `31c0407` RepoRoot both filenames, `ca2d4c7` Store
parser fails on unclassified rows, `d67e83b` secrets example inside the binding
check, `394ea7b` FX-EveryBoundKeyIsDocumented, `4c20f2a` FX-RegistryMatchesDisk,
`9ef0575` corpus layout move.

Deviations from the ask:

- C4 was first implemented with an entry-to-file assertion scoped to a hardcoded
  list of landed checkpoints, which the prompt forbids. Removed before
  committing; only file-to-entry ships.
- A stray `docs/CLAUDE.md` was found, being root `CLAUDE.md` plus the §4a edit
  delivered to the wrong directory. §4a was applied at root per the supplied
  layout rule and the untracked duplicate deleted.
- C3's fourth direction was judged not worth building. It would fire on every
  `rows`-classed key by design, and restricting it to `app`-classed keys
  duplicates FX-EveryBoundKeyIsDocumented.

### State after

| | |
|---|---|
| Corpus | v1.9.1 |
| Phase 0 | 0.1 and 0.2 built |
| Branch | `phase-0/checkpoint-0.1-0.2`, HEAD `9ef0575` |
| PR | #1 open |
| CI | green, 33 tests |

Configuration unchanged: one section bound, `Eodhd` to `EodhdOptions`, and six
`rows`-classed sections deliberately unbound. `CONFIG_REFERENCE.md` carries 22
key rows, four naming two keys each, with the three `Eodhd` Consumer cells
reading `Ingest via EodhdOptions` and the other 19 carrying **Unverified**.

Tests, 33 across six fixtures plus the smoke test: FX-ConfigStoreClassHonoured
9, FX-CeilingNotInsidePolicyBand 7, FX-EveryConfigSectionBinds 6,
FX-EveryBoundKeyIsDocumented 5, FX-MaxDteBelowTrialBound 4,
FX-RegistryMatchesDisk 1. The suite now parses `CONFIG_REFERENCE.md` and
`FIXTURES.md`.

Repository root holds `README.md` and `CLAUDE.md` only.

### Open

- Phase 11: the OpenApi re-add, now recorded in `BUILD_PLAN.md` carried
  obligations.
- 0.8: wire the cross-key invariants to the config write path, and
  FX-ConfigWriteRefusesInvariantBreach.
- `prompts/spent/` does not exist.

---

## 3. CONFIG_REFERENCE shared-row split

Spent 2026-07-28. Corpus v1.9.1 at spend, v1.9.2 at completion.

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
> D2 docs/CONFIG_REFERENCE.md — state the rule the split enforces, supplied
> verbatim: one key per row, a row naming two keys leaves the second unreadable,
> a suffix-only second token is not a key path, constraints go in Notes.
>
> D3 docs/CHANGELOG.md — prepend 1.9.2, supplied verbatim.
>
> D4 docs/PROGRESS.md — corpus version line to v1.9.2, then append, supplied
> verbatim.
>
> C1 Enforce one key per row. Extend FX-ConfigStoreClassHonoured. Test: a key
> cell containing more than one backticked token fails, naming the row and every
> token found. Replace the existing A_row_carrying_two_keys_yields_its_section_root
> test, which asserts correct handling of a form the document now forbids. Test:
> the real CONFIG_REFERENCE.md passes. DoD: re-joining Gate:MinDte and
> Gate:MaxDte in a scratch copy fails the suite. Demonstrate, revert.
>
> Report: HEAD sha, the commits added, the demonstration, and the row count
> before and after.

### Delivered

`b1256f1` one key per row and corpus v1.9.2, `014bfe8` the enforcing test.

No deviations. 22 key rows became 26, the D1 diff touched only the four shared
rows, and the demonstration fired naming both tokens before being reverted.

### State after

| | |
|---|---|
| Corpus | v1.9.2 |
| Phase 0 | 0.1 and 0.2 built |
| Branch | `phase-0/checkpoint-0.1-0.2`, HEAD `014bfe8` |
| PR | #1 open |
| CI | run 30329438361 green, 36 tests |

Configuration unchanged: `Eodhd` to `EodhdOptions`, six `rows`-classed sections
unbound. `CONFIG_REFERENCE.md` carries 26 key rows, one key per row, the rule
stated in the document and enforced by a test.

Tests, 36: FX-ConfigStoreClassHonoured 12, FX-CeilingNotInsidePolicyBand 7,
FX-EveryConfigSectionBinds 6, FX-EveryBoundKeyIsDocumented 5,
FX-MaxDteBelowTrialBound 4, FX-RegistryMatchesDisk 1, plus the 0.1 smoke test.

### Open

- Phase 11: the OpenApi re-add.
- 0.8: wire the cross-key invariants to the config write path.
- `prompts/spent/` does not exist.

---

## 4. BUILD_PLAN reconciliation

Spent 2026-07-28. Corpus v1.9.2 at spend, v1.9.3 at completion.

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

### State after

| | |
|---|---|
| Corpus | v1.9.3 |
| Phase 0 | 0.1 and 0.2 built |
| Branch | `phase-0/checkpoint-0.1-0.2`, HEAD `de2b461` |
| PR | #1 open |
| CI | run 30329944863 green, 36 tests |

`BUILD_PLAN.md` build state reads Phase 0 in progress. 0.1 records the shared
configuration file and the deliberate absence of a `Logging` section. 0.2's
binding DoD covers every committed configuration file, its Consumer DoD asks for
`component via TypeName`, and a note records that the suite parses
`CONFIG_REFERENCE.md` and `FIXTURES.md`.

Configuration and tests unchanged from entry 3.

### Open

- Phase 11: the OpenApi re-add.
- 0.8: wire the cross-key invariants to the config write path.
- `prompts/spent/` does not exist.

---

## 5. Spent-prompt archive

Spent 2026-07-28. Corpus v1.9.3 at spend, v1.9.4 at completion.

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

One commit adding this file and the revised `BUILD_PLAN.md` prompts rule, plus
`CHANGELOG.md` 1.9.4 and `PROGRESS.md`.

Deviations from the ask:

- The rule prose in `BUILD_PLAN.md` is authored content under `CLAUDE.md` §10.
  It was drafted, shown, and applied on instruction.
- A preview offered one file per prompt with four entries. That was rejected in
  favour of one file per phase, and rejected again for referring the reader to
  `PROGRESS.md` for outcomes, which is the back-and-forth the archive exists to
  remove. Each entry now carries an absolute state snapshot.
- Entries 1 and 2 summarise their prompts rather than reproducing every line.
  Prompt 1 ran to roughly seventy lines and prompt 2 to a hundred, and both were
  transcribed into the corpus as they were spent. The clauses that changed what
  was built are quoted; the rest is readable in the pull request thread. Entries
  3, 4 and 5 are close to verbatim.
- Four conversational instructions that shaped later work but produced no
  commits of their own are recorded under Standing instructions rather than as
  entries.

### State after

| | |
|---|---|
| Corpus | v1.9.4 |
| Phase 0 | 0.1 and 0.2 built; 0.3 onward not started |
| Branch | `phase-0/checkpoint-0.1-0.2`, HEAD is this entry's commit |
| PR | #1 open, not merged; `main` holds the corpus only |
| CI | 36 tests |

Solution, .NET 10, nullable, warnings as errors, central package management:
`OptionsWheelLab.Core`, `.Worker`, `.Api`, `.Tests`. One shared
`src/appsettings.json` linked into both hosts and the tests, loaded from
`AppContext.BaseDirectory`. No `Logging` section committed, so every top-level
section must bind and the binding test needs no framework allowlist.
`appsettings.Secrets.json` is gitignored with a committed empty `.example`.

Configuration. One section bound: `Eodhd` to `EodhdOptions`, verified by reading
the composition root. Six sections deliberately unbound because
`CONFIG_REFERENCE.md` classes them `rows` and a bound options type is a
current-value accessor: `Risk`, `Gate`, `Costs`, `Policy`, `Trial`, `Scoring`.
`CONFIG_REFERENCE.md` carries 26 key rows, one key per row, three Consumer cells
verified as `Ingest via EodhdOptions` and 23 carrying **Unverified**. No value is
set that the document marks unset.

Tests, 36 across six fixtures plus the 0.1 smoke test:
FX-ConfigStoreClassHonoured 12, FX-CeilingNotInsidePolicyBand 7,
FX-EveryConfigSectionBinds 6, FX-EveryBoundKeyIsDocumented 5,
FX-MaxDteBelowTrialBound 4, FX-RegistryMatchesDisk 1. All six fixtures
registered against 0.2 are implemented and named for their registry entry. The
suite parses `CONFIG_REFERENCE.md` and `FIXTURES.md`, so both are load-bearing
rather than descriptive.

Repository root holds `README.md` and `CLAUDE.md` only; every other document is
in `docs/`.

Not built: the store, migrations, the config read service and its as-of
resolver, the deterministic clock, money and ticker primitives, the append-only
CI greps, and every checkpoint from 0.3 onward.

### Open

- Phase 11: re-add `Microsoft.AspNetCore.OpenApi` against a version whose
  `Microsoft.OpenApi` dependency clears the audit. Recorded in `BUILD_PLAN.md`
  carried obligations.
- 0.8: wire the two cross-key invariants to the config write path, and
  FX-ConfigWriteRefusesInvariantBreach.
- 0.3: the config read service and its as-of resolver, FX-ConfigResolvesAsOf and
  FX-NoCurrentConfigReadOnSimulatedPath.
- No blockers.
