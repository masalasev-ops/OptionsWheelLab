# CHANGELOG

## [1.9.5] — 2026-07-28

### Changed
- `prompts/spent/phase-0.md` replaces its per-entry state snapshots with one
  **Current state** section, overwritten by every prompt. Entries keep what was
  asked and what was delivered.
- `BUILD_PLAN.md` restates the prompts rule to match.

### Notes
- 1.9.4 gave each entry its own snapshot so the state could be read in one take.
  That worked for the last entry and made every earlier one a description of a
  state no longer true, which is hard to navigate and invites the stale-state
  findings `CLAUDE.md` §1 exists to prevent. A reader landing mid-file met
  correct-looking numbers that were a version out of date.
- The fix is to separate what ages from what does not. What was asked and what
  was delivered are statements about a moment that has passed and stay true
  forever. State is current or it is wrong, so it lives in exactly one place and
  is overwritten.
- The shape mirrors `PROGRESS.md`, which already carries an overwritten Current
  state above an appended Log, so it is the corpus's own pattern rather than a
  new one.

## [1.9.4] — 2026-07-28

### Added
- `prompts/spent/phase-0.md`, the first spent-prompt archive. Five entries
  covering every prompt spent while Phase 0 has been in flight.

### Changed
- `BUILD_PLAN.md` restates how prompts are archived. One file per phase,
  appended to, rather than one file per prompt. An entry carries what was
  asked, what was delivered including every deviation, an absolute snapshot of
  the repository after it, and what it left open.

### Notes
- One file per phase because a directory of forty single-prompt files buries
  the thing being looked for.
- Each entry carries an absolute snapshot rather than a change list, so the
  current state is derived from the last entry in a single read rather than by
  reconstructing it from a chain of deltas across several documents. The cost
  is that every earlier entry describes a state that is no longer true, so the
  file states plainly that only the last entry describes the present and
  `PROGRESS.md` is the authority on now. Without that line the archive would
  manufacture the stale-state findings `CLAUDE.md` §1 exists to prevent.
- Deviations are recorded per entry because a prompt alone cannot answer
  whether it was followed, which was the back-and-forth the archive removes.

## [1.9.3] — 2026-07-28

### Changed
- `BUILD_PLAN.md` build state moves from Phase 0 not started to Phase 0 in
  progress, with 0.1 and 0.2 built.
- `BUILD_PLAN.md` 0.1 records the shared configuration file and the deliberate
  absence of a `Logging` section, both of which existed only in commit
  messages.
- `BUILD_PLAN.md` 0.2's first definition of done covers every committed
  configuration file rather than `appsettings.json` alone, matching the check
  that ships.
- `BUILD_PLAN.md` 0.2's Consumer definition of done asks for `component via
  TypeName`, matching the convention 1.9.1 introduced.

### Added
- `BUILD_PLAN.md` 0.2 notes that the test suite parses `CONFIG_REFERENCE.md`
  and `FIXTURES.md`, so both are load-bearing rather than descriptive.

### Notes
- Found reviewing 0.1 and 0.2 against what shipped. The checkpoint text was
  substantially accurate and no built thing contradicted it; the divergences
  were one false build-state marker and four places where the plan described
  less than what was built. A definition of done that understates the check is
  the more dangerous of the two, because it reads as satisfied while leaving
  the extra coverage undefended by any stated requirement.

## [1.9.2] — 2026-07-28

### Changed
- `CONFIG_REFERENCE.md` splits its four shared rows so every row names one
  key, and states the rule.

### Notes
- Found verifying PR #1. The parser reads the first backticked token per key
  cell, so `Gate:MaxDte` and three Policy suffixes were undocumented as far
  as any check could tell. Latent while only `Eodhd` binds; at Phase 2 it
  would have made FX-EveryBoundKeyIsDocumented fire against a key the
  document contains, and the usual answer to a fixture that fires wrongly is
  to weaken it.

## [1.9.1] — 2026-07-28

### Added
- FX-EveryBoundKeyIsDocumented at 0.2: every settable key on a bound options
  type must have a row in `CONFIG_REFERENCE.md`.
- `BUILD_PLAN.md` **Carried obligations**, holding work deferred out of a
  checkpoint. First entry: re-adding `Microsoft.AspNetCore.OpenApi` at
  Phase 11.
- `CLAUDE.md` 4a: never suppress a vulnerability advisory to keep a
  dependency.

### Changed
- `FIXTURES.md` rule 2 restated. As written it required every registered
  entry to have a file, which cannot hold while most entries belong to
  unbuilt checkpoints, so nothing could enforce it. Split by direction.
- FX-RegistryMatchesDisk moved from 0.6 to 0.2.
- `CONFIG_REFERENCE.md` defines the Consumer column as `component via
  TypeName` once verified.
- `CLAUDE.md` 10: `CONFIG_REFERENCE.md` and `PROGRESS.md` have two authors
  and are never delivered as whole files.
- `README.md` states the corpus layout.

### Notes
- The registry gap is the original defect inverted: 0.2 was built to catch a
  configuration section nobody reads; FX-EveryBoundKeyIsDocumented catches a
  configuration key nobody writes.

## [1.8.3] — 2026-07-27

### Fixed
- `BUILD_PLAN.md` Phase 0 definition of done contradicted [D-W27]. It required
  every key in `CONFIG_REFERENCE.md` to be proven to bind, but a `rows`-classed
  key never binds from `appsettings`, and proving it did is the defect
  FX-ConfigStoreClassHonoured fails on. Restated as: every `app`-classed key
  proven to bind, and no `rows`-classed key bound from `appsettings`.
- `Eodhd:BaseUrl` carried neither a value nor an **Unset** marker, unlike every
  other deliberately-empty key. Marked **Unset**, set at Phase 8.
- `CONFIG_REFERENCE.md`'s build-state line asserted that no consumer was
  verified, which goes stale the moment a checkpoint lands. Restated as partly
  verified, with unverified rows carrying an explicit marker.

### Notes
- All three were found by reading the documents against each other during
  checkpoint planning and were reported rather than closed, per `CLAUDE.md` §10.
  The Phase 0 wording predates D-W27 and was missed when §0.2 was rewritten,
  which is the reconciliation question in §11 failing to be asked.

## [1.8.2] — 2026-07-27

### Fixed
- `BUILD_PLAN.md` §0.2 now carries the D-W27 binding scope and the two
  invariants as predicates.

  **This edit was claimed in 1.8.0 and did not land.** The 1.8.0 entry states
  that §0.2 was changed; the replacement silently matched nothing and the
  section kept its original wording. The claim is struck rather than removed, so
  the failure is visible. Anyone who synced 1.8.0 or 1.8.1 has a §0.2 that
  contradicts D-W27.

### Added
- `CLAUDE.md` §10, who authors what. Authored content (decisions, checkpoint
  scope, fixture registrations, rule prose) arrives as a corpus sync and is
  reported rather than closed by a build. Verified content (the Consumer
  column, HEAD shas, test counts, `PROGRESS.md`) is corrected directly by
  whoever ran the build. The reconciliation question moves to §11.
- `CLAUDE.md` §10 also requires confirming the working copy is current before
  reporting a corpus entry as absent.

### Notes
- The 1.8.0 failure is the exact defect `CLAUDE.md` §1 exists to prevent, made
  by the author of §1. An edit was asserted to have landed without a read-back
  confirming it. Edits to this corpus now assert their match target and fail
  loudly rather than no-op.

## [1.8.1] — 2026-07-27

### Changed
- FX-ConfigStoreClassHonoured's registry description now names its mechanism:
  it parses the Store column from `CONFIG_REFERENCE.md` and asserts no
  appsettings section has a root classed `rows`.

### Notes
- That makes the Store column a machine-checked contract rather than prose, so
  a `rows`-classed section gaining an appsettings binding fails the build
  instead of being caught in review. Same pattern as FX-RegistryMatchesDisk
  reading this file.
- The two config fixtures close the loop from opposite directions and neither
  closes it alone. FX-ConfigStoreClassHonoured catches a `rows` section that
  gains a binding; FX-EveryConfigSectionBinds catches a section that binds to
  nothing.

## [1.8.0] — 2026-07-27

### Added
- D-W27: configuration storage class follows the read path. A value read while
  producing or scoring a simulated decision is a config row; everything else is
  bound from `appsettings`.
- `CONFIG_REFERENCE.md` gains a **Store** column on every key.
- Fixtures FX-ConfigStoreClassHonoured (0.2) and
  FX-ConfigWriteRefusesInvariantBreach (0.8).

### Changed
- D-W23 and D-W24 amended: the two cross-key invariants are enforced when a
  config version is written, not at startup. Their fixture descriptions and the
  `CONFIG_REFERENCE.md` note are restated to match. The invariants themselves
  are unchanged; only the enforcement point moves.
- `BUILD_PLAN.md` §0.2 now binds only `app`-classed sections and delivers the
  two invariants as pure predicates. §0.8 gains the write-time enforcement DoD.

### Notes
- The enforcement-point correction fixes an error introduced in 1.6.0. "Checked
  at startup" assumed a value bound once at boot. Config rows are versioned and
  insertable while the process runs, so a startup check leaves every later
  version unguarded. It survives as a backstop, not as the contract.
- D-W27 exists because storage class and change authority are separate
  questions. [D-W11] governs who may change the risk caps; it says nothing
  about how they are resolved, and an operator-set value still needs as-of
  resolution when an earlier decision is re-scored.

## [1.7.0] — 2026-07-27

### Changed
- `CLAUDE.md` rewritten and expanded from 51 to 208 lines, carrying the working
  discipline developed in AlphaLab. New sections: verify before asserting,
  configuration, git and commits, decisions and evidence, long runs, working
  style, and the reconciliation question.
- Each transplanted rule states the reason it exists. A rule whose cost is
  invisible gets negotiated away.

### Added
- D-W26: configuration is resolved as-of a simulated date, never as-now, with
  two fixtures against checkpoint 0.3 and the config read service added to that
  checkpoint.

### Notes
- D-W26 closes a real gap rather than restating a convention. The corpus already
  had append-only versioned config, but nothing required a simulated date to
  resolve config as of that date. Because a policy revision inserts a new
  version, any later re-scoring or replay would have read configuration the
  original session never ran under, and the resulting parity failure presents as
  impure inputs rather than as a resolution bug.
- Rules deliberately not transplanted: AlphaLab's arena, plant, and calibration
  machinery has no counterpart here, and its three-seat AI structure does not
  map onto three decision-makers, which are simulated policies rather than model
  seats.

## [1.6.0] — 2026-07-27

### Added
- D-W22 contract-level liquidity filter, D-W23 delta ceiling, D-W24 expiry
  window, D-W25 earnings clearance. All are gate constraints, all structural and
  not learner-proposable [D-W11].
- Config keys under `Gate:`, all unset pending Phase 0.8, plus two cross-key
  invariants checked at startup rather than trusted.
- Eight fixtures: six gate rejections against Phase 2, two startup invariants
  against Phase 0.2.

### Changed
- `SYSTEM_DESIGN.md` §3.4 split into portfolio constraints and contract
  constraints, which answer different questions.
- The gate now records every failing reason for a candidate rather than the
  first.

### Notes
- The liquidity filter's primary purpose is measurement rather than risk. Regret
  is measured against the best candidate in the feasible set, so an
  untransactable quote corrupts every decision scored that day and not only a
  decision that selects it.
- D-W25 forecloses a learnable question, whether elevated pre-earnings implied
  volatility is worth harvesting. The trade is recorded in the decision rather
  than left implicit.

### Known conflict
- `WORKED_EXAMPLE.md` predates D-W22 to D-W25 and contradicts them. Flagged in
  place at §3, not silently corrected. Unresolved.

## [1.5.0] — 2026-07-27

### Changed
- Project renamed from `WheelLab` to `OptionsWheelLab` across the corpus,
  including the namespace roots in [D-W1] and `BUILD_PLAN.md` §0.1 and the
  architecture diagram title.

### Notes
- Rationale for the name: the bare word "wheel" reads as a physical wheel with no
  options context. "Options" supplies that context. "Strategy" was considered and
  dropped, because a repository named for validating a strategy contradicts
  [D-W2] before a reader opens a document.
- The decision prefix remains `D-W`. Re-prefixing a register breaks every
  reference already pointing at it, which is the same rule as never renumbering.
- `WheelLab.html`, the mockup source from passes 1 and 2, keeps its filename. It
  predates the rename and is a historical artefact.

## [1.4.0] — 2026-07-26

### Added
- `PRIMER_THE_WHEEL.md`. The strategy in plain terms, independent of this lab:
  the loop, a worked pass with numbers, the source of the return, the payoff
  shape, four failure modes, and why a high win rate misleads.

### Changed
- `README.md` and `ORIENTATION.md` now point at the primer before the glossary.

### Notes
- The primer states the payoff asymmetry plainly, that maximum gain is the
  premium while downside is nearly the full committed capital, because that
  asymmetry is the reason the lab's risk machinery exists rather than a
  qualification on it.

## [1.3.0] — 2026-07-26

### Added
- `GLOSSARY.md`. Two layers defined separately: standard options terminology, and
  this lab's own invented terms. Registered in `README.md`.

### Changed
- `UI_MOCKUPS.md` §4 rewritten. The previous version listed only the lab's
  invented terms and assumed options fluency, so the options layer went
  undefined everywhere. Both layers are now named, and explaining both in place
  is a product requirement rather than a suggestion.
- `UI_MOCKUPS.md` §6 now gives each screen a plain opening sentence naming the
  question it answers, using neither vocabulary.
- `UI_MOCKUPS.md` §5 now states that density applies to the data and not to the
  explanation.
- `ORIENTATION.md` now names its options-fluency assumption up front and points
  at `GLOSSARY.md`.

### Notes
- Cause of the comprehension failure in mockup pass 1: the terms were used
  correctly on every screen and explained on none. The prototype was internally
  consistent and unreadable to its own owner.

## [1.2.0] — 2026-07-26

### Added
- `UI_MOCKUPS.md` §10: first mockup pass reviewed, four defects logged as UI-1 to
  UI-4, all four closed by amending the brief.

### Changed
- `UI_MOCKUPS.md` §6.3 now states that gross basis governs the covered call
  constraint and net basis governs nothing. The previous wording said only that
  one of them governs, and the mockup asserted the inverse [D-W19].
- `UI_MOCKUPS.md` §6.2 now states that both scores are post-resolution and no
  maker acts on a rank [D-W6, D-W20].
- `UI_MOCKUPS.md` §6.1 now constrains headline gap figures to the full evaluation
  window or an explicit descriptive marker [`VALIDITY.md` §4].
- `UI_MOCKUPS.md` §6.5 now requires the stress figure to be shown against its own
  limit rather than the total capital cap.

### Notes
- The schema survived the mockup pass with no changes required.

## [1.1.0] — 2026-07-26

### Added
- `UI_MOCKUPS.md`. Six screens specified, with the brief for producing them and
  an empty section for the results.

### Notes
- The verdict screen carries the risk drift panel on the same screen as the
  regret curve, stated as a requirement rather than a layout preference
  [`VALIDITY.md` §3].
- Each screen names the tables it reads, so that a screen which cannot be drawn
  from `DATA_AND_SCHEMA.md` surfaces as a schema defect before Phase 1.

## [1.0.0] — 2026-07-26

Documentation corpus authored from scratch. Supersedes v0.1 (2026-07-12), which
is not recoverable.

### Added
- `README.md`, `ORIENTATION.md`, `SYSTEM_DESIGN.md`, `DECISIONS.md`,
  `VALIDITY.md`, `DATA_AND_SCHEMA.md`, `BUILD_PLAN.md`, `WORKED_EXAMPLE.md`,
  `FIXTURES.md`, `CONFIG_REFERENCE.md`, `CLAUDE.md`, `PROGRESS.md`.
- Diagrams: `diagrams/architecture.svg`, `diagrams/wheel-state-machine.svg`.
- Decisions D-W1 to D-W21.

### Changed from v0.1
- The lab is reframed as a decision laboratory measured on improvement in
  decisions, not as a validation lab for the wheel strategy [D-W2].
- The risk gate moved inside the candidate generator so all makers receive an
  identical feasible set [D-W10].
- The outcome metric became a first-class specification rather than an implied
  one [D-W17, D-W18, D-W19, D-W20, D-W21].
- Three parallel decision-makers replaced a single policy under test [D-W4].
- The phase map grew from 7 phases to 12, with the data purchase boundary made
  explicit at Phase 8.

### Notes
- D-W1 to D-W13 reuse numbers first issued in v0.1. Because v0.1 is not
  recoverable, the entries in `DECISIONS.md` are the authoritative definition of
  those numbers, and any external reference to a v0.1 D-W number predates this
  corpus.
