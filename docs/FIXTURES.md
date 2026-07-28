# FIXTURES

The single registry of test fixtures. Prompts and checkpoints reference the
fixtures registered against them here; they never enumerate fixture names inline.

Build state: **none built**.

## Why this file exists

In a sibling project a build prompt listed its fixtures by name. Two fixtures
were later added elsewhere, and the prompt went silently incomplete, so building
from it would have omitted the highest-value check in that phase. Naming
fixtures in one place and pointing at it removes that failure by construction.

## Rules

1. Every fixture appears here exactly once, registered against one checkpoint.
2. A fixture file on disk with no entry here fails the build, and an entry here
   with no file fails the build (FX-RegistryMatchesDisk).
3. Fixture data is synthetic unless explicitly marked otherwise. Synthetic is
   preferred because assignment, early exercise, and roll-cap cases can be
   constructed deliberately rather than waited for.
4. Adding a fixture is not a doc change requiring propagation. That is the point.

## Registry

| Fixture | Checkpoint | Asserts | Source |
|---|---|---|---|
| FX-EveryConfigSectionBinds | 0.2 | no configuration section binds to nothing | authored |
| FX-MigrateFromEmpty | 0.3 | migration from empty is correct and idempotent | authored |
| FX-ApiCannotWrite | 0.3 | the Api connection is read-only | authored |
| FX-MoneyRoundTrip | 0.4 | adversarial decimals survive storage | authored |
| FX-TickerDashForm | 0.4 | dot and dash ticker forms normalise together | authored |
| FX-NoAmbientClock | 0.5 | no ambient DateTime call outside the clock | authored |
| FX-RegistryMatchesDisk | 0.6 | this registry and the fixture directory agree | authored |
| FX-ConfigStoreClassHonoured | 0.2 | parses the Store column from CONFIG_REFERENCE.md and asserts no appsettings section has a root classed `rows` | authored |
| FX-ConfigWriteRefusesInvariantBreach | 0.8 | a config version violating a cross-key invariant is refused and no row is written | authored |
| FX-ConfigResolvesAsOf | 0.3 | a key resolves to the version in force on the simulated date | authored |
| FX-NoCurrentConfigReadOnSimulatedPath | 0.3 | no simulated-date component reads current config | authored |
| FX-PitMembershipExcludesLaterJoiner | 1 | as-of membership excludes later joiners | authored |
| FX-SnapshotNeverRewritten | 1 | a vendor correction appends rather than updates | authored |
| FX-GateRejectsAboveHeadroom | 2 | candidates breaching per-name headroom are rejected with a reason | WORKED_EXAMPLE §3 |
| FX-OffWatchlistRejected | 2 | no candidates for a non-member symbol | authored |
| FX-SpreadCapRejects | 2 | a candidate above the spread cap is rejected with its reason | authored |
| FX-PremiumFloorRejects | 2 | a candidate below the premium floor is rejected with its reason | authored |
| FX-GateRecordsAllReasons | 2 | a candidate failing two constraints carries both reasons | authored |
| FX-DeltaCeilingRejects | 2 | a candidate above the delta ceiling is rejected with its reason | authored |
| FX-DteWindowRejects | 2 | candidates on either side of the expiry window are rejected | authored |
| FX-EarningsClearanceRejects | 2 | a candidate whose life spans a buffered report date is rejected | authored |
| FX-CeilingNotInsidePolicyBand | 0.2 | the predicate holds that the delta ceiling is no tighter than any policy band | authored |
| FX-MaxDteBelowTrialBound | 0.2 | the predicate holds that MaxDte is below MaxTrialDays | authored |
| FX-GrossBasisBindsCallStrike | 2 | a call strike admitted by net basis and refused by gross basis is refused | WORKED_EXAMPLE §6.3 |
| FX-TrialCompleteIncludesAssignment | 3 | the assigned trial totals 498.05 | WORKED_EXAMPLE §6.3 |
| FX-RollCapCloses | 3 | a trial reaching the roll bound closes at market and resolves | authored |
| FX-ThreeMakersSameFeasibleSet | 4 | all makers receive byte-identical candidate sets | WORKED_EXAMPLE §3 |
| FX-RecordCarriesFeasibleSet | 4 | a decision is re-scorable from its record alone | authored |
| FX-ExcursionRecordedOnWin | 5 | a positive outcome still carries its adverse excursion | WORKED_EXAMPLE §6.2 |
| FX-NoAnnualizeInObjective | 5 | equal returns over unequal durations store identically | authored |
| FX-FastSlowDivergenceFires | 5 | inverted fast and slow rankings raise the monitor | WORKED_EXAMPLE §7 |
| FX-RegretUsesSlowScore | 5 | regret is computed from the trial-complete score | WORKED_EXAMPLE §8 |
| FX-NoLearnerOutputInJudgingPath | 6 | no judging component reads anything the learner produced | authored |
| FX-RiskDriftFiresOnBorrowedTail | 6 | falling regret with widening excursion raises the check | authored |
| FX-LearningBoundaryLagRespected | 7 | the learner sees only trials closed before the boundary | authored |
| FX-PreRegRequired | 9 | the forward run refuses to start without a committed pre-registration | authored |
