# CHANGELOG

## [1.19.0] — 2026-07-30

Checkpoint 1.1 signed off.

### Added
- `prompts/spent/phase-1.md`, carrying 1.1's prompt with its review rounds folded in
  and the Current state that describes the present. Phase 0's file is closed and its
  Current state is frozen as a record of that phase's close.

### Changed
- `BUILD_PLAN.md` 1.1 is reconciled against what shipped. Five things were larger
  than its scope, and three of them were found by measuring something the detail
  assumed rather than by reviewing what was written.
- The build-state markers say Phase 1 is in progress and that 1.1 is built and
  signed off, 1.2 to 1.5 being live intent.

### Notes
- 1.1 shipped a schema the document did not fully specify: twelve triggers, three
  indexes, a `CHECK`, a uniqueness constraint and two foreign keys, where only the
  tables were in the detail. The foreign keys were asked for by nothing and are
  raised rather than assumed.
- Three of the checkpoint's findings came from measurement answering a question other
  than the one asked. Measuring the decimal vocabulary's false-positive surface found
  a false negative; running the alias convention over real statements found two
  detector defects; demonstrating the twelve refusals found a trigger message that
  claimed a column its table does not have.

## [1.18.0] — 2026-07-29

Checkpoint 1.1.

### Added
- `contracts` carries `multiplier` and `deliverable_shares` as separate columns.
  `multiplier` is what a quoted premium multiplies by and an adjustment does not
  change it; `deliverable_shares` is what one contract conveys on exercise and an
  adjustment does. The single `multiplier` column was named for one and intended as
  the other. Neither is described by what consumes it, because which of the two the
  outcome metric uses is open.
- `UNIQUE (symbol, expiry, right, strike, deliverable_shares)` on `contracts`. The
  identity tuple alone is not unique: an adjusted series can carry a strike that
  collides with a standard one on the same underlying and expiry, and the deliverable
  is what separates them. Deliberately weaker than a constraint on the tuple, which
  would forbid a collision that occurs, and it still stops the same contract being
  inserted twice. Not `vendor_symbol`, because a synthetic chain carries none and
  SQLite treats nulls in a unique index as distinct, so it would guard nothing until
  Phase 8 while the duplicate-insert bug is live from 1.4.
- Three indexes on the market-data tables, each naming the query it serves. The three
  keyed tables need none: a primary key ending in `observed_at` is already the index
  an as-of read wants.
- A carried obligation owed at Phase 3: settle which quantity committed capital uses.
  D-W17's first paragraph says the contract multiplier and its third says the
  deliverable, and they differ for an adjusted contract. To be checked against OCC's
  contract adjustment memos rather than a secondary source.
- `FIXTURES.md` rule 2 says when a row sits at phase granularity and when that becomes
  a defect: a row is registered against a phase until that phase's detail is written
  and against a checkpoint once it is.

### Changed
- Phase 1's two registered rows move to checkpoint granularity now that its detail
  names five checkpoints. FX-SnapshotNeverRewritten to 1.1,
  FX-PitMembershipExcludesLaterJoiner to 1.3. Phases 2 to 9 stay at phase
  granularity, which is correct while their detail is unwritten.
- `BUILD_PLAN.md` 1.1 no longer says `AppendOnlyTables` gains the six. 0.7 declared
  all six forward, so 1.1 adds no names and what it owes is the reverse direction.
- `vendor_symbol` is nullable. The field OCC uses to distinguish an adjusted series
  is the one a synthetic chain cannot supply, and everything before Phase 8 runs on
  synthetic chains.

- Fixture FX-NoSqlAliases (1.1): no SQL in `src/` aliases a table or a column. This
  discharges the alias obligation raised at 0.4 and widened at 0.7, by the
  convention half of it rather than by resolution. Resolving an alias in the decimal
  detector needs the detector to know which table a column belongs to, because the
  vocabulary is unqualified column names, and that is the problem 1.1 declined when
  it kept `DecimalColumns` unqualified. Both known-miss tests are deleted, which the
  obligation named as part of closing it. **What it costs**: a self-join must alias
  one side, so the convention forbids one, and 1.5's definition of done requires a
  historical join across a split. The cost is recorded against 1.5 by name rather
  than against a hypothetical, so it is revisited with a real query. Measured: the
  walk is expressible without an alias as a recursive CTE, which names the working
  set rather than renaming the table, and it runs against migration 3 over a
  three-generation chain.

### Removed
- The Phase 1 carried obligation on SQL aliases, discharged by FX-NoSqlAliases.

### Reconciled
- `DATA_AND_SCHEMA.md` §4.1 now records nullability, the foreign keys and the
  `CHECK`, which the schema block did not carry. The document marks `NULL`
  explicitly where it means it, so ten columns the migration makes nullable read as
  `NOT NULL`: `corporate_actions.ratio` and `.amount`, and eight of
  `contract_quotes`. That is the difference between a chain having to supply a gamma
  and being allowed to omit one, which is what `ContractQuote` settled at 0.6.
- §3 said a delete or an update against a snapshot table fails the build. It also
  fails in the store from 1.1. The two guards cover different writers, and the
  sentence described only the one that reads `src/`.
- `AppendOnlyTables` said two of its ten tables exist and eight do not. Eight exist
  and two do not; `decisions` and `candidates` are Phase 4. Its inline comment still
  said none of the snapshot tables existed yet.
- Current state in `prompts/spent/phase-0.md` carried the same count.

### Fixed
- `DATA_AND_SCHEMA.md` §2 says an option contract's identity is the tuple of
  underlying, expiry, right and strike. **It is not.** An adjusted series can share
  all four with a standard contract and differ only in the deliverable, which the
  sources confirm for a three-for-two split. §2's own promise that an adjusted
  contract is "a new identity with a recorded predecessor link" is unkeepable when
  the new identity equals an existing one. Recorded against §2 rather than corrected,
  because it reaches D-W29's rationale, `ContractIdentity`'s equality and checkpoint
  1.5, and wants a decision.

## [1.17.0] — 2026-07-29

Phase 1 checkpoint detail authored. Documentation only; no code.

### Added
- D-W35: records are append-only, projections may be rebuilt. A record is the only
  place a fact is held, so rewriting it destroys the fact;
  `watchlist_membership` is one. A projection is derived from an append-only source
  and may be updated in place, `trials` and `positions` being projections of
  `ledger_entries`. **The condition is not free**: a projection may be rewritten
  only where a test discards it, rebuilds it from its source and gets the same
  rows, without which it is a rewritable table with a flattering name. This
  answers the question the Phase 1 effective-dating obligation asks; the schema and
  vocabulary work it implies is still Phase 1's.
- Fixture FX-ProjectionRebuildsFromLedger at Phase 3, which D-W35 names as the
  condition on rewriting a projection at all. Registered because a decision naming
  a fixture the registry does not carry is a citation resting on nothing.
- Phase 1 checkpoint detail, five checkpoints: the market-data schema, as-of reads,
  membership as state, chain ingest, and corporate actions with the predecessor
  link. "Phase 1 and beyond" becomes "Phase 2 and beyond".
- A carried obligation owed at Phase 4: store one feasible set per name and date
  rather than one per decision. `candidates` is keyed on `decision_id`, so three
  makers acting on one set write it three times while [D-W4] requires the three to
  be byte-identical. Storing once and referencing thrice makes that true by
  construction and divides the largest uncertain table by three.

### Fixed
- Three snapshot tables were keyed so a correction could not append, contradicting
  D-W8 and the fixture registered to test it. `underlying_bars`,
  `chain_snapshots` and `contract_quotes` take `observed_at` into the key. Found by
  reading the schema against what rests on it, before any of the three existed.

### Notes
- Phase 1 is the first phase whose detail was written after its preconditions were
  settled rather than alongside them. The effective-dating question forced that
  order, because it decides a column shape and a detail written before it would
  have described a schema that might be wrong.

## [1.16.0] — 2026-07-29

Findings from a review of Phase 0, the first independent pass over the code.

### Added
- Fixture FX-EveryPolicyBandIsChecked (0.8): every `Policy:*:DeltaMax` row in
  `CONFIG_REFERENCE.md` appears in `ConfigKeys.PolicyBandCeilings`. Without it an
  incomplete band list makes a violating configuration **pass**, because D-W23's
  ceiling is compared only against the bands the list names. That differs in kind
  from an incomplete catch-list, which still catches what is on it. It was also
  scheduled to happen: the learner acts from policy rows, so its band arrives at
  Phase 4 and nothing would have failed had it never been listed.
- **A rule about which direction a declared vocabulary is checked in.**
  `DecimalColumns` and `AppendOnlyTables` are checked list to document, because
  there the error is a name with no table behind it. `PolicyBandCeilings` is
  checked document to list, because there the error is a band with no entry. Each
  is checked standing in the direction in which absence causes the bad outcome,
  and the other direction is a definition of done on the checkpoint that adds the
  thing. Recorded at the vocabulary and in the fixture, since the apparent
  inconsistency is what a reader meets first.
- A carried obligation owed at Phase 8: extract the market rules out of
  `SyntheticChainReader`. Refusing a negative bid, a negative ask and a crossed
  market are statements about what a market can be, not JSON concerns, and they
  sit as private statics on the reader, so the vendor ingest could only duplicate
  them. Coupled to the Phase 2 crossed-quote decision, which determines whether
  the crossed rule moves to the gate instead of into the shared definition.

### Fixed
- `ConfigWriter.Append` returned a version it had not necessarily written. It
  committed, then read `MAX(version)` on a fresh command outside any transaction,
  while its own summary said it returns the version written. The insert reports
  its own version through `RETURNING` now, inside the transaction that wrote it.
  Correct before only because the store has a single writer [D-W1], and it would
  have failed by returning a plausible number rather than by raising, which is the
  worst way for it to fail. `RETURNING` needs SQLite 3.35 and the bundled engine
  is 3.53.3, measured from the binary at 0.5.
- `SqliteConnectionStringBuilder.Pooling = false` carried no reason. It is
  load-bearing on Windows: with pooling on the native file handle outlives
  `Dispose`, so the snapshot copy and the temp-store teardown fail on a locked
  file. Every other non-obvious choice in that file states its reason, and this is
  the one that looks like a tidy-up when ingest throughput first matters.
- `ConfigRowQuery.ResolveAtOrBefore` takes no transaction where `ResolveCurrent`
  does, and nothing said the asymmetry was deliberate. Recorded, with what ends
  it: Phase 1's ingest writes chains while resolving membership as-of [D-W9], and
  meets exactly the behaviour that forced the parameter onto the other method.
- `SyntheticChainReader.RefuseImpossibleMarket` was documented as "the one domain
  rule this reader enforces" while enforcing three.

## [1.15.1] — 2026-07-29

### Fixed
- The worked-example reconciliation was recorded in a document banner and a dated
  log entry and never migrated into Carried obligations when that table was
  created at v1.9.1. Three checkpoints have since taken dependencies on the
  example. The obligation is unchanged; only its location is.
- Carried obligations said it holds work deferred out of a checkpoint, which
  excluded the entry above: it was raised in the corpus at v1.6.0, before any
  checkpoint had run. The introduction now admits an obligation raised in the
  corpus and says its Raised column carries a corpus version rather than a pull
  request. A register that admits only one origin is how an obligation comes to
  exist only in the place nobody reads, which is the failure this table was
  created to prevent.
- The version sequence skips 1.9.0 with no explanation. Noticed at 0.5, proposed,
  not applied, and then relied on wrongly by the clause that produced this
  obligation row. A note sits above the 1.9.1 heading now, which is where a reader
  meeting the gap is looking. A gap with no note reads as a missing entry, and the
  first thing anyone does with a missing entry is guess at it.

## [1.15.0] — 2026-07-29

Checkpoint 0.8 signed off, and with it Phase 0.

### Added
- Nineteen of the twenty-three `rows`-classed keys carry a value, written at
  version 1 in one transaction. Provenance is stated per key in
  `CONFIG_REFERENCE.md` and is three kinds: the six `Gate:` keys are the values
  their decisions proposed [D-W22 to D-W25]; the seven `Policy:` keys,
  `Costs:CommissionPerContract` and `Costs:FillPoint` are transcribed from
  `WORKED_EXAMPLE.md` §1; `Trial:MaxRolls`, `Trial:MaxTrialDays` and the two
  `Scoring:` keys are judged. `Trial:MaxTrialDays` at 120 is the least free of the
  four: D-W24 puts it above `Gate:MaxDte`, and the worked example's own trial runs
  109 days, so a lower bound would force-close that trial before its third expiry
  and make its stated total unreachable. The two `Scoring:` values are constrained
  by nothing in this corpus and are recorded as free judgement rather than left to
  look derived.
- Fixture FX-EveryAppKeyBinds (0.8): every `app`-classed row in
  `CONFIG_REFERENCE.md` has a bound settable property on a registered options
  type. The mirror of FX-EveryBoundKeyIsDocumented, which walks the types and
  checks the document, where this walks the document and checks the types.
  **The assertion is not new.** It landed at 0.4 in a suite deliberately outside
  the registry, because a phase definition of done was held not to be a fixture.
  Registering it makes it discoverable from `FIXTURES.md` rather than only from
  the file it lived in, and it is moved rather than copied: two tests asserting
  one thing with two failure messages is how a fact kept in two places drifts.
- `seed.ps1`, beside `migrate.ps1`. The case for it was symmetry until it was
  measured: with `Storage__Path` unset the verb throws from `StoreLocation` with
  the right words under a stack trace, where the script refuses cleanly. Two steps
  of one setup sequence, and the second reporting the identical mistake worse than
  the first is what it fixes.

### Changed
- D-W23's proposed 0.35 for `Gate:MaxDelta` is chosen rather than inherited. The
  ceiling is argued from D-W4: a control drawing from a smaller opportunity set
  than the gate admits would make a difference between it and the learner partly
  permission rather than judgement. The 0.10 floor is a separate question the same
  argument does not reach, and is recorded as inherited from `WORKED_EXAMPLE.md`
  §1 rather than argued.
- `CONFIG_REFERENCE.md` says the store is the authority on what is in force. A
  value in the Notes column is version 1 and the reason for it; a revision inserts
  version + 1 and does not edit the document, so a value the store has since
  revised is history rather than a contradiction.
- `BUILD_PLAN.md`'s build-state marker says Phase 0 is complete. The first time
  this corpus says a phase is done.

### Fixed
- D-W23 carried an `Open, and to be settled at Phase 0.8` clause and 0.8 settled
  it. Leaving it open after the checkpoint that answers it is the stale-corpus
  failure this project keeps correcting.
- `CONFIG_REFERENCE.md` declined a standing check on the reverse binding
  direction because most keys are deliberately unbound until their own phase. That
  reasoning covers `rows` keys and has not reached `app` keys since `Eodhd` bound
  at 0.2, an `app` key being bound from `appsettings` by definition. The paragraph
  now says which class it speaks for and names the check that covers the other. It
  had been half wrong for six checkpoints and read as complete, because a
  paragraph declining a check does not say which keys it is declining it for.

## [1.14.0] — 2026-07-29

### Added
- D-W34: a write that makes a cross-key invariant unevaluable is refused. An
  invariant over two keys cannot be evaluated while only one exists, so skipping
  the check passes vacuously until the last key lands, which is the state the
  enforcement exists to prevent. Its consequence is the mechanism rather than a
  side effect: neither `Gate:MaxDte` nor `Trial:MaxTrialDays` can be written
  alone, so the pair is atomic by the write path rather than by the seeder's
  discipline. Scoped so a write touching no invariant key is still permitted into
  an empty store.
- D-W34 added to the Data and identity line of the topical index.
- Two carried obligations for the four `rows`-classed keys 0.8 leaves unset. Two
  rows rather than one, because the reasons and the consumers differ: the risk
  fractions are withheld on authority [D-W11] and are owed at Phase 2, which
  consumes them; the assignment fee is withheld for want of any statement and is
  owed at Phase 3, whose assignment path first computes with it. Leaving a key
  unseeded and leaving it unscheduled are different, and only the first was
  deliberate.

### Changed
- `BUILD_PLAN.md` 0.8 names ten unset keys rather than four. It named the two
  `Trial:` and two `Scoring:` keys, while `CONFIG_REFERENCE.md` and D-W22 to
  D-W25 assign the six `Gate:` keys to Phase 0.8 as well. The two documents
  disagreed and the checkpoint detail was the one that was wrong.
- `BUILD_PLAN.md` 0.8 records that the seven `Policy:` keys are seeded too. They
  carry no unset marker, so a strict reading would leave them out, and without
  them D-W23's invariant cannot be exercised at all: the predicate passes
  vacuously against an empty band set.
- `BUILD_PLAN.md` 0.8 states that provenance is judged per key rather than per
  section, which is what `Policy:` already was and `Costs:` was not.
- `BUILD_PLAN.md` 0.8's first definition of done says the consumer is **named**
  rather than verified. Every consumer is Phase 2 or later, so a definition of
  done reading as satisfiable at 0.8 was not.
- `BUILD_PLAN.md` 0.8 carries the Phase 0 definition of done, this being the last
  checkpoint and nothing after it being left to demonstrate the phase's.
- `SYSTEM_DESIGN.md` §8 is rewritten. Its subject closes at 0.8, so a section
  describing an openness that has ended is replaced by one naming what is now in
  force, where the provenance is recorded, and which four keys are owed rather
  than open.

### Fixed
- `SYSTEM_DESIGN.md` §3.5 said the makers select inside the same delta and expiry
  bands. That is true for expiry and false for delta: `WORKED_EXAMPLE.md` §1 has
  said 0.20 to 0.30 against 0.10 to 0.35 since v1.0.0, so the two have never
  shared a delta band. Found by seeding the values the sentence describes. It now
  also records the coupling the schema does not, that `Policy:Random:` carries no
  DTE keys because the random maker reads the baseline's window, which a reader of
  `CONFIG_REFERENCE.md` alone would take for an omission.
- `SYSTEM_DESIGN.md` §8 was loose before 0.8 made it false. It said "two values"
  while naming four keys, the roll bounds being two and the divergence threshold
  and window being two, and omitted the six gate constraints entirely.

## [1.13.0] — 2026-07-29

### Added
- D-W33: the source guards stay a text scan and a fixture, and neither is
  replaced by a Roslyn analyser. One check of four would gain anything, being
  inferred types for the floating-point guard; the two SQL checks gain nothing
  because an analyser returns the same string literal a fixture already has and
  does not parse SQL. The package costs ten transitive pins and a project
  overriding the build props. It also records what would reopen it.
- The topical index gains a **Verification mechanisms** line. The six existing
  lines are about the experiment rather than about how the repository checks
  itself, and "Isolation and controls" means control arms. D-W28 joins D-W33
  there while staying on Data and identity, since it is both and the index is a
  finding aid rather than a partition.

### Changed
- The carried obligation to decide between a text scan and a Roslyn analyser is
  **closed with a no-change answer**, not lapsed. D-W33 discharges it, and the
  row is removed rather than left standing, because an obligation that outlives
  its answer reads as unfinished.

### Notes
- The measurement refuted the reason the guards were split. `guards.ps1` claimed
  a guard must fail even when the build does not; a probe with a violation in one
  file and a type error in another reported both, so the claim was false. What an
  analyser cannot survive is a failed restore, where none runs and only the NuGet
  error appears. The script runs before restore, so the property it actually has
  is that it reports when restore does not succeed. Narrower, true, and enough —
  and the script now says that instead of the claim it could not support.

## [1.12.0] — 2026-07-29

### Added
- D-W32: the migration ledger is never rewritten, so `schema_migrations` enters
  the append-only vocabulary on a decision rather than on §4.0's prose. Its
  rationale is not the one the other tables have: a store's schema version is
  derived from the ledger rather than stated anywhere, so a rewritable ledger
  makes a store unable to answer what it is, and two snapshots either side of a
  rewrite would restore to different schemas while claiming the same version.
  Predicted by the plan before the check was built, using the method `CLAUDE.md`
  §1 gained this checkpoint, which makes it the third instance of the pattern and
  the first found deliberately rather than by accident.
- D-W32 added to the Data and identity line of the topical index.
- `CLAUDE.md` §1: a citation is verified by what rests on it, not by reading it.
  Two citations named a decision for a property that decision did not state, both
  were right about the rule and wrong about its authority, and building the thing
  that enforces the rule is what found both.
- `DATA_AND_SCHEMA.md` §4.0 documents `schema_migrations`, which 0.3 created and
  §4 never carried. §4 documented seventeen tables that do not exist and omitted
  one of the two that do.
- `DATA_AND_SCHEMA.md` §4.1 defines the **snapshot tables**. The phrase is used
  in four documents and was defined in none. `contracts` is one despite carrying
  no `observed_at`, because a corporate action mints a new identity rather than
  editing a row.
- A carried obligation at Phase 1 for effective-dating, covering
  `watchlist_membership`, `positions` and `trials`. Each carries a nullable close
  column that makes a state change an update while §4.2 says rows are never
  deleted, so the schema and the rule disagree in three places for one reason.
  Raised at 0.7 when drawing the vocabulary made the disagreement visible, and
  widened by this checkpoint's own report, which found the second and third
  instances. Amended rather than duplicated, for the same reason the alias
  obligation was: one problem seen at three levels is one obligation, and closing
  it for one table would leave the others to be rediscovered at Phase 3 and
  Phase 4.

### Changed
- `CLAUDE.md` §2 item 2 states the rule and hands the table list to the check.
  It named snapshot tables, `decisions` and `candidates` and stopped, while
  `config_rows` was the only one of them that existed.
- `BUILD_PLAN.md` 0.7 says the check is a fixture rather than a source guard.
  0.4's criterion decides it, and a measurement settles it beyond the criterion:
  `guards.ps1` strips raw string literals by design and every SQL statement here
  lives in one, so a pattern added to the script would match nothing in the tree
  by construction.
- `BUILD_PLAN.md` 0.7 gains the two definitions of done carried from 0.2, which
  0.5 and 0.6 received and it did not.
- The Phase 1 alias obligation is amended rather than duplicated. It named one
  detector and one known-miss test; 0.7 adds a second detector with a table-alias
  miss, and it is one alias problem seen at two levels. Deleting both known-miss
  tests is now part of closing it.

### Fixed
- `decisions` and `candidates` were cited to D-W3 for a property D-W3 did not
  state. Same shape as the D-W8 citation corrected at 0.5, found the same way, by
  building the check that rests on it. D-W3 now says a recorded decision is never
  rewritten.
- 0.7's constraint said the check distinguishes by table rather than by location.
  Building it measured three mechanisms, and scan scope is one of them. Corrected
  from the measurement; no decision determined it, and the correction states why
  scope is not an exemption.
- Three documents described this guard as a CI grep. `BUILD_PLAN.md` was
  corrected at v1.9.9 and these were not, so the wording survived in a decision's
  Test line, a schema section and a design section until building the check made
  all three false.
- §4.5 left `note` unmarked where `Migrations.cs` declares it nullable and where
  §4 marks nullability elsewhere. One word, and the one case §4 can be wrong
  rather than merely early: every other block describes a table nobody has built.
- D-W32's scope clause named two of the three effective-dated tables. Listing two
  of three reads as a boundary rather than as an example, so `trials` is named
  alongside them.
- D-W32's scope clause carried a count and the count was wrong: it said six kinds
  where the measurement is four decisions, five kinds and ten tables. Removed
  rather than corrected, because it would have gone stale regardless once
  `watchlist_membership` resolves. That is the same treatment 0.7's own constraint
  gets in this version, and for the same reason — a number that moves when
  unrelated work lands is not a property of the rule.

## [1.11.0] — 2026-07-29

### Added
- D-W31: synthetic chains are written by hand, and the format serves that. It
  states what the format optimises for rather than what the format is, so a later
  phase changing the format supersedes nothing and a later phase changing the
  property has something to supersede. It settles the open question at 0.6 in
  favour of a domain shape over a schema-mirroring one.
- D-W31 added to the Data and identity line of the topical index.
- `README.md` records that the corpus rule governs documents only: code lives in
  `src/` and `tests/`, spent prompts in `prompts/`, and hand-written synthetic
  chains in `synthetic/`. None of those are documents and none belong in `docs/`.
  Named `synthetic/` rather than `fixtures/` because 0.5 established that a
  fixture is a registry entry, and a `fixtures/` directory holding no `FX-*.cs`
  would undo that the week after it was drawn.
- `BUILD_PLAN.md` carried obligations gains the crossed-quote question, owed at
  Phase 2. Refusing bid above ask at the loader makes a crossed or locked market
  unwritable, so nothing can exercise the gate against one, which is a hole in
  D-W31's own premise that deliberate cases are writable.

### Changed
- `BUILD_PLAN.md` 0.6 names what the checkpoint ships and the detail was silent
  on: that the loader produces objects rather than rows, since no market-data
  table exists; that it defines the quote and bar types, there being none; that
  values parse through the stored forms on the way in and refuse rather than
  round; that output is in contract identity order rather than file order; that
  `WORKED_EXAMPLE.md` §2 and §5 are the acceptance test; and where chains live.
- `BUILD_PLAN.md` 0.6 gains the two definitions of done carried from 0.2, the
  second discharged empty because the loader takes text and resolves no location,
  which is what keeps a configuration key out of this checkpoint.

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
- `BUILD_PLAN.md` 0.6 separates the two things this corpus calls fixtures. A
  check is a registry entry, a `fixture` or a `guard`; a synthetic chain is test
  data. 0.6 builds the loader for the second, and does not read `FIXTURES.md`,
  which registers checks and holds no data. Surfaced by 0.5's Kind column, which
  is what made the two kinds of check distinct enough for the third thing to be
  visible.
- `CLAUDE.md` §2 item 4 states the property rather than enumerating the forms.
  It listed two, `guards.ps1` catches six, and the list will grow again. Same
  shape as 0.7's constraint counting five and enumerating four.
- FX-ClockIsNotADateSource catches SQLite's bare-call clock forms. Its date and
  time functions default their time value to `'now'` when it is omitted, so
  `datetime()`, `date()`, `time()`, `julianday()`, `unixepoch()` and
  `strftime('%Y')` all return the current time while carrying neither `'now'` nor
  `CURRENT_`. Measured against the bundled SQLite 3.53.3 rather than taken from
  documentation, which also turned up `strftime` with the time value omitted, a
  form that was not raised.
- FX-ClockIsNotADateSource catches `'subsec'` and `'subsecond'` as a time value,
  and pins the measurement that bounds them. A first argument that is a modifier
  rather than a time value does not imply `'now'`: SQLite parses it as a time
  value, fails, and returns NULL. Measured across all twenty-four documented
  modifiers, twenty-two behave that way and only those two do not. That bounds
  the residual at two forms rather than at the whole modifier set, so both are
  caught and no known limit is owed for modifiers. The patterns are positional,
  because the same word applied to a supplied time value is a legitimate
  modifier.
- The clock-reading function list is enumerated from the binary rather than from
  documentation. Of the 168 functions the bundled SQLite registers, exactly seven
  read the wall clock, one of which, `timediff`, had not been considered. That
  list is the standing residual: an upgrade adding an eighth returns here, and a
  test asserts every named function still exists.

### Removed
- `BUILD_PLAN.md` 0.6's definition of done, "adding a fixture file without
  registering it fails the build", was FX-RegistryMatchesDisk from 0.2 restated,
  so 0.6 would have discharged on work another checkpoint did. Replaced with what
  the loader must do, and with the format question named rather than answered:
  rows per table loads trivially and reads badly by hand, a chain per name per
  date is the reverse, and these are written by hand.

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

> 1.9.0 was never issued. Two blocks of changes were drafted under 1.9.0 and
> 1.9.1 and landed in a single sync, so they are recorded as one version
> rather than two. Nothing references 1.9.0.

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
