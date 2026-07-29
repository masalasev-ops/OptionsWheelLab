# CHANGELOG

## [1.10.0] — 2026-07-28

### Added
- D-W30: the clock tells wall-clock time and nothing else. The injected clock
  returns the instant the process is running at, and a simulated date is never
  obtained from it. It is D-W26's rule arriving through a different door: a
  component that wants the simulated date and reaches for the clock gets an
  answer that is plausible, non-null and wrong.
- D-W30 places the clock at composition and entry points only. Nothing below them
  reads a clock, which keeps 0.5 a wiring change and keeps tests supplying a
  fixed instant directly rather than through a fake.
- D-W30 added to the Data and identity line of the topical index.
- D-W26 states that a written version is never altered, and that `config_rows` is
  append-only on that authority. Resolving as-of a past date answers what was in
  force then, which means anything only if a version still says what it said; an
  update in place would make a past answer unverifiable rather than wrong.
- `BUILD_PLAN.md` states what a prompt does when `PROGRESS.md` reports a corpus
  version other than the one the prompt names: establish what changed, proceed
  only where the drift demonstrably does not reach what the prompt depends on,
  and say so in the report. Written as a convention rather than restated per
  prompt, because a gate that only says stop makes every docs-only bump either
  halt a checkpoint or teach the gate to be ignored.
- `GLOSSARY.md` defines Clock and Determinism. "Clock" already carried four
  meanings here: `SYSTEM_DESIGN.md` §5's two clocks are the daily and per-cycle
  loops, and §7 has the forward, subscription and evidence clocks. None of them
  is the wall-clock source D-W30 names.
- `FIXTURES.md` registers FX-ClockIsNotADateSource at 0.5. D-W30 names it and
  this is the single registry, so the registration follows from the decision.
- `BUILD_PLAN.md` states that a checkpoint's detail names everything the
  checkpoint ships, including corrections it carries that nothing in the
  checkpoint caused. The detail is what the build is measured against, so a
  checkpoint shipping more than its detail predicts leaves the detail describing
  an idealised version of the work. Recording the difference only here, in the
  changelog, is how that document becomes ceremonial.
- `BUILD_PLAN.md` 0.5 names its own design, being D-W30 and the two mechanisms
  its fixtures need, and carries a section listing the corrections it ships that
  the clock did not cause. Its detail asked for four things and the checkpoint
  shipped fourteen.

### Changed
- `BUILD_PLAN.md` 0.5 said the clock is "injected everywhere", which D-W30's
  placement clause contradicts. 0.5 is not built, so its detail was corrected as
  live intent.
- `BUILD_PLAN.md` 0.5's byte-identical definition of done had no subject, because
  there is no simulated run at 0.5. Restated as identical stored rows compared as
  table contents, since a SQLite file is not a deterministic rendering of its
  contents and a byte comparison would fail for reasons that are not about the
  clock. The output-level property is carried to Phase 3, the first checkpoint
  with a run to make.
- `BUILD_PLAN.md` 0.5 gains the two definitions of done carried from 0.2, which
  0.2 says apply to every checkpoint from there on and which 0.5, 0.6 and 0.7 all
  lacked.
- `BUILD_PLAN.md` 0.6 and 0.7 record that registering their entries is due when
  their prompts are written. Both instructed implementing a set that is empty.

### Fixed
- `FIXTURES.md` conflated two kinds of check. Rule 2 assumed every entry is a
  `.cs` file, which was true when it was written and stopped being true at 0.4,
  when the first check landed in a script. The registry gains a Kind column and
  rule 2 is restated per kind.
- The 0.4 floating-point guard was never registered, so the registry did not list
  every check the build enforces. Registered as FX-NoFloatingPoint.
- The triggers enforcing `config_rows`' append-only property cited D-W8, which
  governs snapshots and does not reach a versioned configuration table. Snapshots
  carry `observed_at` and correct by appending a new observation; this table
  carries `set_at` and `version`. The property follows from D-W26, which now
  states it.

### Notes
- Surfaced by FX-NoAmbientClock at 0.5, which is registered and is a script
  check. The 0.4 guard was the same shape and raised no conflict only because it
  was absent from the registry, which is the defect rather than the escape.
- `BUILD_PLAN.md` 0.7's constraint said five statements in the tree carry the
  banned text and then enumerated four. Measured: six, being two trigger DDL
  statements, three tests asserting those triggers reject an `UPDATE` or a
  `DELETE`, and one `UPDATE` against a scaffold table created inside a test. That
  sixth is what settled the mechanism, because its *table* lies outside the rule
  rather than its location being exempt, so the check distinguishes by table and
  needs no exemption list. The count is now out of the constraint entirely: four
  of the six are tests, so it moves whenever a test is written and would be wrong
  again before 0.7 is built.

## [1.9.10] — 2026-07-28

### Changed
- `BUILD_PLAN.md` says when a checkpoint is determined fully built: after review
  has closed and before the merge, not when the last line is written. Review is
  part of the determination because it changes what shipped. At 0.4 it changed
  the deliverable four times and the prompt had been archived before any of it,
  so replaying that prompt would not have reproduced the tree. Without this the
  rule reproduces the staleness it was written to prevent, because "fully built"
  reads as "I have finished writing it".
- The archive's Current state no longer records which branch the work sits on or
  which pull requests have merged. Git holds both exactly, and a fact kept in two
  places drifts, which is the same defect corrected four other times at 1.9.9.
  They were also the only fields that could not be known at the moment a
  checkpoint is determined fully built, since a merge commit does not exist until
  after it, so removing them closes the one timing gap in the new rule rather
  than adding a step to work around it.
- The no-squash policy moves from that table to Working rules in force, beside
  the two workflow rules it belongs with, since it is a policy rather than an
  observation.
- Working rules records that a pull request description describes the change as
  it stands rather than accumulating a section per review round. An appended
  section cannot retract an earlier one, so PR #3 ended up asserting a superseded
  rule alongside the rule that replaced it.

## [1.9.9] — 2026-07-28

### Added
- D-W29: stored decimals are canonical and are not ordered in SQL. It exists
  because strike participates in contract identity, so canonicalisation is a
  correctness requirement rather than tidiness: `50` and `50.00` are the same
  number and different `TEXT`, and a non-canonical form would give one contract
  two identities and split its history without failing.
- D-W29 states that the scale is a fidelity requirement for vendor-supplied
  values and a rounding policy for computed ones, so there are two entry points
  and the caller chooses. One could not be both.
- D-W29 states that every decimal reaching a `TEXT` column passes through the
  canonical form and no call site formats a decimal itself.
- D-W29 added to the Data and identity line of the topical index.
- FX-NoDecimalOrderingInSql at 0.4.
- `DATA_AND_SCHEMA.md` §2 states that a ticker has two forms, the bare dash form
  for the store and the exchange-suffixed form for the vendor, and that the
  suffix is added at the boundary and never stored.
- `DATA_AND_SCHEMA.md` §2 states the stored date form and warns that
  `InvariantGlobalization` makes the invariant short date `MM/dd/yyyy`, so a date
  stringified without an explicit format is culture-independent and still wrong.
- `DATA_AND_SCHEMA.md` §4.1 pins `right` as `put` or `call`, lower case.
- `CLAUDE.md` §10: authored prose that a landed decision has already superseded
  is corrected rather than reported, the authority being the decision and never
  the code.
- A standing check that every `app`-classed key in `CONFIG_REFERENCE.md` binds.
  Phase 0's definition of done requires it and nothing enforced it, so it passed
  by coincidence. The reverse direction is not standing for `rows`-classed keys,
  because most are deliberately unbound until their own phase, but that reasoning
  does not reach `app`, where a key is bound from `appsettings` by definition.
- `CLAUDE.md` §1: a code comment is not a corpus record. What makes a record is
  where it will be read, not whether it survives, so an obligation noted beside
  the code that has it and nowhere the planning for that work will look is not
  recorded. Written after a 0.4 report claimed an obligation was in the archive
  when it was in a fixture comment.

### Changed
- `DATA_AND_SCHEMA.md` §4 states that every decimal, not only money, is stored
  in the canonical fixed-scale form, that the scale is a single declared
  constant, and that decimal columns are not ordered, ranged over, or aggregated
  in SQL.
- D-W17 now reads "strike times the contract multiplier times contracts". One
  hundred is the standard multiplier and not a constant of the metric: an
  adjusted contract carries its own deliverable in `contracts.multiplier`, so a
  metric hardcoding one hundred would misprice every position in a name that has
  split.

### Fixed
- `BUILD_PLAN.md` states the three states a checkpoint's detail passes through.
  Not built, it is live intent and is corrected whenever something that has
  landed changes what must be built. Signed off, it is frozen. Between them sits
  a single event, determining the checkpoint fully built, at which the detail is
  reconciled against what shipped AND the prompt is appended to the archive with
  Current state overwritten. Both halves belong to that moment, which is also
  what keeps Current state true: it is then written after the last change rather
  than before it.
- **An earlier draft of this same version said built detail is a record and is
  never reconciled.** That collapsed the last two states into one and would have
  discarded the reconciliation v1.9.3 performed on 0.1 and 0.2. Struck rather
  than quietly replaced, because the two rules give opposite instructions for
  0.4.
- 0.4's detail reconciled against what shipped, that being the two decimal entry
  points, the date and contract-right stored forms, the typed configuration
  accessors, the total order on contract identity, and the source guard as a
  script rather than a bare grep.
- `BUILD_PLAN.md` 0.5 and 0.7 said their guards were CI greps, written before
  any guard existed. Both now extend the source guards 0.4 established, stated
  as the rule rather than the implementation, since 0.7 may replace it.
- `BUILD_PLAN.md` 0.6 named FX-RegistryMatchesDisk, which `FIXTURES.md` rule 2
  registers at 0.2 and which shipped there, and described it as failing on
  either direction, which the same rule rejects. 0.6 delivers the loader.
- `BUILD_PLAN.md` 0.5 and 0.6 gain the reference clause instead of naming
  fixtures inline, which is what the enumeration rule already required of them.
- Carried obligations gains the four items 0.4 deferred. They existed only in
  the archive, which is not where planning for the phase that owns them looks,
  and the archive's Owed list now points at the register rather than copying it.
- The suffix `Pct` named a percentage while every description said fraction, and
  one proposed value was written as a percentage. Renamed to `Fraction` so the
  name states the unit: `Risk:PerNameCapFraction`, `Risk:TotalCapFraction`,
  `Risk:SimultaneousAssignmentLimitFraction`, and `Gate:MaxSpreadFractionOfMid`
  proposed 0.12 rather than 12. D-W22 updated to match. Every one of these keys
  is unset and unconsumed, so the rename cost nothing and could not have been
  done free again.
- `DATA_AND_SCHEMA.md` §4 and `BUILD_PLAN.md` 0.3 both said `migrate.ps1` calls
  the snapshot tool internally first. The runner holds the guarantee and the
  mechanism is `VACUUM INTO` [D-W28], which the same section of
  `DATA_AND_SCHEMA.md` already said two paragraphs later. Corrected under the
  decision that determines it.
- `README.md` reported corpus version 1.0.0.

### Notes
- The three corrections above are reconciliations rather than a build overruling
  a document. The test applied was whether a landed decision already determines
  what the text should say; where it does, the text is corrected and the
  decision cited. That rule is now in `CLAUDE.md` §10 so it does not have to be
  re-derived each time a decision lands.
- `docs/WheelLab.html` still carries the old key names. It is the rendered
  mockup prototype from `UI_MOCKUPS.md` §10, a historical artefact fixed at the
  moment it was produced rather than a document that tracks the corpus, so §10
  records that it predates the rename and the file is left alone.

## [1.9.8] — 2026-07-28

### Added
- D-W28: snapshots are taken with `VACUUM INTO` rather than by copying the
  database and its write-ahead log. It also states the shape a snapshot has on
  disk, being one file named `snapshot-<filename-form timestamp>.db` beside the
  store, so a restore knows what to look for and what to ignore.
- FX-SnapshotRestoresIdentically at 0.3.
- D-W28 added to the Data and identity line of the topical index.
- `CLAUDE.md` 4b: test seams. `InternalsVisibleTo` where the public alternative
  would be worse, recorded at the reference site.

### Changed
- `DATA_AND_SCHEMA.md` states the `VACUUM INTO` mechanism, that no lock is
  required and no writer is blocked, and that the first run has nothing to copy
  as a base case rather than an exception.

### Reversed
- **The 0.3 prompt specified copying the `.db`, `-wal` and `-shm`, and the build
  rejected `VACUUM INTO` on the grounds that the three-file copy was what the
  corpus specified. That reasoning is now overturned by what building it
  showed.** Holding an exclusive lock across the copy, which the copy needs so a
  writer cannot tear it, byte-range locks `-shm` and makes it unreadable. The
  lock and the three-file copy were never jointly satisfiable, so the
  implementation had to drop `-shm` and record a departure from this document.
- The reversal is the point rather than an embarrassment. The original rejection
  was correct given what was specified; the specification was wrong, and only
  writing it revealed that.

### Notes
- Recorded as a decision rather than left in prose and a doc comment. The
  mechanism was specified in `DATA_AND_SCHEMA.md`, contradicted by the code, and
  justified in a C# remark, which is three homes and no owner for a choice this
  consequential.
- Taken at 0.3 deliberately. The store holds one migration and almost nothing
  else, and the mechanism only becomes more expensive to change as data
  accumulates.

## [1.9.7] — 2026-07-28

### Added
- `DATA_AND_SCHEMA.md` pins the two timestamp forms and forbids comparing a
  date against a timestamp.
- `DATA_AND_SCHEMA.md` states WAL journal mode.
- D-W27 gains a bootstrap clause: a value needed to open the store cannot be
  stored in the store, so it is `app`-classed by necessity rather than by the
  read-path criterion.
- `DATA_AND_SCHEMA.md` gives the filename form of a timestamp, since the stored
  form contains colons.
- `CONFIG_REFERENCE.md` gains `Storage:Path`, the absolute directory holding the
  store and its snapshots.

### Changed
- `BUILD_PLAN.md` 0.3 states that the as-of and current-value config surfaces
  are separate types, so D-W26 is enforced by API shape rather than by scanning
  callers.
- `CONFIG_REFERENCE.md` records that the reverse binding direction is a
  per-checkpoint definition of done, not a standing check.

### Fixed
- D-W26 said `set_at` "precedes" the as-of date, which reads strict. Resolution
  is inclusive, matching every other as-of read.

### Notes
- Both the reverse-direction rule and the two-surfaces rule are the same
  pattern, now used four times: a check whose subjects mostly belong to unbuilt
  checkpoints cannot be a standing assertion, so it becomes either a definition
  of done or a shape the code cannot violate. 0.6 and 0.7 will meet it again.
- Nothing pinned a date format, while twenty-one columns store dates as TEXT and
  every as-of read compares them as strings. 0.3 is the first checkpoint to
  write and compare one, so its choice would have become the standard by
  default.
- `Storage:Path` rather than `Store:Path`, because the second collides with the
  **Store** column in a document the suite parses.

## [1.9.6] — 2026-07-28

### Changed
- `prompts/spent/phase-0.md` carries one prompt per checkpoint rather than one
  per ask. Corrections found while building are folded back into the
  checkpoint's prompt.
- `BUILD_PLAN.md` restates the prompts rule to match.

### Notes
- The archive had accumulated an entry per conversational exchange: a review
  pass, a document split, and two entries about the archive's own design. None
  of them is a checkpoint, and reading them in order described how the work
  wandered rather than how to reach the result.
- Replaying one prompt per checkpoint reproduces the state without replaying
  the mistakes, which is what the archive is for.
- The cost, recorded rather than hidden: the file no longer shows what was
  actually asked at the time, so it cannot answer whether a checkpoint was built
  as first specified or as later corrected. That history stays in the commit log
  and the pull request thread. The archive is now a reproduction instrument
  rather than a record of the conversation.

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
