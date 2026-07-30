# FIXTURES

The single registry of test fixtures and source guards. Prompts and checkpoints
reference the entries registered against them here; they never enumerate names
inline.

Build state: **partly built**. The twenty-seven entries registered against
0.2 to 1.3 are implemented, being twenty-five fixtures and two guards; the
rest belong to checkpoints not yet reached. 1.2 registered none, which its
detail states.

## Why this file exists

In a sibling project a build prompt listed its fixtures by name. Two fixtures
were later added elsewhere, and the prompt went silently incomplete, so building
from it would have omitted the highest-value check in that phase. Naming
fixtures in one place and pointing at it removes that failure by construction.

## Rules

1. Every fixture appears here exactly once, registered against one checkpoint.
2. Enforcement runs in two directions and they land at different times,
   because most entries here belong to checkpoints not yet built. The
   artefact an entry points at depends on its Kind, but the directions are
   the same for both.
   - **Artefact to entry, always enforced.** An `FX-*.cs` file with no
     entry here fails the build, and so does a named check in
     `guards.ps1` with no entry here. Safe from the first one onward, so
     FX-RegistryMatchesDisk is registered at 0.2 rather than 0.6.
   - **Entry to artefact, per checkpoint.** An entry whose checkpoint has
     landed must have its artefact: a file for a `fixture`, a named check
     in the script for a `guard`. This cannot be a standing assertion
     without the suite knowing which checkpoints are complete, so it is a
     definition of done on each checkpoint instead. A checkpoint that
     registers no entries says so in its detail, rather than discharging
     this vacuously. An obligation that passes because its subject set is
     empty is not discharged, it is unexercised.

     A row is registered against a phase until that phase's checkpoint
     detail is written, and against a checkpoint once it is. Detail is
     written one phase ahead, so a row sits at phase granularity for at
     most one phase. A row left at phase granularity after its detail
     exists makes every checkpoint's definition of done resolve to
     nothing, which is this rule discharging on an empty set rather than
     on its subjects.
3. Fixture data is synthetic unless explicitly marked otherwise. Synthetic is
   preferred because assignment, early exercise, and roll-cap cases can be
   constructed deliberately rather than waited for.
4. Adding a fixture is not a doc change requiring propagation. That is the point.

## Registry

Every entry carries a **Kind**. `fixture` means a C# test, one `FX-*.cs` file in
the test project. `guard` means a named check inside `guards.ps1`, which must
fail even when the build does not.

| Fixture | Kind | Checkpoint | Asserts | Source |
|---|---|---|---|---|
| FX-EveryConfigSectionBinds | fixture | 0.2 | no configuration section binds to nothing | authored |
| FX-MigrateFromEmpty | fixture | 0.3 | migration from empty is correct and idempotent | authored |
| FX-ApiCannotWrite | fixture | 0.3 | the Api connection is read-only | authored |
| FX-MoneyRoundTrip | fixture | 0.4 | adversarial decimals survive storage | authored |
| FX-TickerDashForm | fixture | 0.4 | dot and dash ticker forms normalise together | authored |
| FX-NoDecimalOrderingInSql | fixture | 0.4 | no SQL orders, ranges over, or aggregates a decimal column | authored |
| FX-NoFloatingPoint | guard | 0.4 | no `double` or `float` and no named floating-point entry point anywhere in the tree | authored |
| FX-NoAmbientClock | guard | 0.5 | no ambient DateTime call outside the clock | authored |
| FX-ClockIsNotADateSource | fixture | 0.5 | no simulated-date path derives its date from the clock | authored |
| FX-WorkedExampleChainLoads | fixture | 0.6 | the chain and bars in WORKED_EXAMPLE §2 and §5 round-trip through the loader | WORKED_EXAMPLE §2, §5 |
| FX-ChainLoadsInIdentityOrder | fixture | 0.6 | quotes are yielded in contract identity order, and loading twice gives one sequence | authored |
| FX-MalformedChainFailsWhole | fixture | 0.6 | a chain with one malformed contract yields nothing rather than the valid ones before it | authored |
| FX-NoRewriteOfAppendOnlyTables | fixture | 0.7 | no statement in `src/` deletes from or updates a table the append-only vocabulary covers | authored |
| FX-NoSqlAliases | fixture | 1.1 | no SQL in `src/` aliases a table or a column, which is what makes both SQL detectors sound without either resolving aliases | authored |
| FX-RegistryMatchesDisk | fixture | 0.2 | every fixture file on disk has an entry here and is named for it | authored |
| FX-EveryBoundKeyIsDocumented | fixture | 0.2 | every settable key on a bound options type has a row in CONFIG_REFERENCE.md | authored |
| FX-ConfigStoreClassHonoured | fixture | 0.2 | parses the Store column from CONFIG_REFERENCE.md and asserts no appsettings section has a root classed `rows` | authored |
| FX-EveryAppKeyBinds | fixture | 0.8 | every `app`-classed row in CONFIG_REFERENCE.md has a bound settable property on a registered options type | authored |
| FX-EveryPolicyBandIsChecked | fixture | 0.8 | every `Policy:*:DeltaMax` row in CONFIG_REFERENCE.md appears in `PolicyBandCeilings`, so the delta ceiling is checked against every band that exists | authored |
| FX-ConfigWriteRefusesInvariantBreach | fixture | 0.8 | a config version violating a cross-key invariant is refused and no row is written | authored |
| FX-ConfigResolvesAsOf | fixture | 0.3 | a key resolves to the version in force on the simulated date | authored |
| FX-NoCurrentConfigReadOnSimulatedPath | fixture | 0.3 | no simulated-date component reads current config | authored |
| FX-SnapshotRestoresIdentically | fixture | 0.3 | a store restored from its snapshot resolves the values it did before the mutation | authored |
| FX-PitMembershipExcludesLaterJoiner | fixture | 1.3 | as-of membership excludes later joiners | authored |
| FX-SnapshotNeverRewritten | fixture | 1.1 | a vendor correction appends rather than updates | authored |
| FX-GateRejectsAboveHeadroom | fixture | 2 | candidates breaching per-name headroom are rejected with a reason | WORKED_EXAMPLE §3 |
| FX-OffWatchlistRejected | fixture | 2 | no candidates for a non-member symbol | authored |
| FX-SpreadCapRejects | fixture | 2 | a candidate above the spread cap is rejected with its reason | authored |
| FX-PremiumFloorRejects | fixture | 2 | a candidate below the premium floor is rejected with its reason | authored |
| FX-GateRecordsAllReasons | fixture | 2 | a candidate failing two constraints carries both reasons | authored |
| FX-DeltaCeilingRejects | fixture | 2 | a candidate above the delta ceiling is rejected with its reason | authored |
| FX-DteWindowRejects | fixture | 2 | candidates on either side of the expiry window are rejected | authored |
| FX-EarningsClearanceRejects | fixture | 2 | a candidate whose life spans a buffered report date is rejected | authored |
| FX-CeilingNotInsidePolicyBand | fixture | 0.2 | the predicate holds that the delta ceiling is no tighter than any policy band | authored |
| FX-MaxDteBelowTrialBound | fixture | 0.2 | the predicate holds that MaxDte is below MaxTrialDays | authored |
| FX-GrossBasisBindsCallStrike | fixture | 2 | a call strike admitted by net basis and refused by gross basis is refused | WORKED_EXAMPLE §6.3 |
| FX-TrialCompleteIncludesAssignment | fixture | 3 | the assigned trial totals 498.05 | WORKED_EXAMPLE §6.3 |
| FX-RollCapCloses | fixture | 3 | a trial reaching the roll bound closes at market and resolves | authored |
| FX-ProjectionRebuildsFromLedger | fixture | 3 | `trials` and `positions` discarded and rebuilt from `ledger_entries` give the same rows, which is the condition on rewriting them at all [D-W35] | authored |
| FX-ThreeMakersSameFeasibleSet | fixture | 4 | all makers receive byte-identical candidate sets | WORKED_EXAMPLE §3 |
| FX-RecordCarriesFeasibleSet | fixture | 4 | a decision is re-scorable from its record alone | authored |
| FX-ExcursionRecordedOnWin | fixture | 5 | a positive outcome still carries its adverse excursion | WORKED_EXAMPLE §6.2 |
| FX-NoAnnualizeInObjective | fixture | 5 | equal returns over unequal durations store identically | authored |
| FX-FastSlowDivergenceFires | fixture | 5 | inverted fast and slow rankings raise the monitor | WORKED_EXAMPLE §7 |
| FX-RegretUsesSlowScore | fixture | 5 | regret is computed from the trial-complete score | WORKED_EXAMPLE §8 |
| FX-NoLearnerOutputInJudgingPath | fixture | 6 | no judging component reads anything the learner produced | authored |
| FX-RiskDriftFiresOnBorrowedTail | fixture | 6 | falling regret with widening excursion raises the check | authored |
| FX-LearningBoundaryLagRespected | fixture | 7 | the learner sees only trials closed before the boundary | authored |
| FX-PreRegRequired | fixture | 9 | the forward run refuses to start without a committed pre-registration | authored |
