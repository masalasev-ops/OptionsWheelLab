# FIXTURES

The single registry of test fixtures and source guards. Prompts and checkpoints
reference the entries registered against them here; they never enumerate names
inline.

Build state: **partly built**. The sixty-eight entries registered against 0.2 to
4.4 are implemented, being sixty-five fixtures and three guards; the rest belong
to checkpoints not yet reached. 1.2 and 2.1 registered none, which their details
state.

3.3 is the first checkpoint to discharge the entry-to-artefact direction against
a full set rather than against one or two: fifteen rows, fourteen files and a
named check in the script. Every earlier checkpoint met it too, but with counts
small enough that meeting it and noticing it were the same act. Twelve landed
before it signed off and three came from reviewing it afterwards, which is the
first time this registry has grown from a review rather than from a build.

3.4 registered nothing and implemented the one row already standing against it.
That row has been in this registry since v1.0.0, waiting for the ledger it reads.

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

**Source** records where a fixture's expectations come from, not whether it
parses a document. A fixture that parses `CONFIG_REFERENCE.md` to check its own
consistency is `authored`, because the expectation is the rule rather than the
document's content. A fixture whose expected values are read out of
`WORKED_EXAMPLE.md` names the sections they come from. This was unstated until
v1.31.1 and had to be recovered by auditing the column against what each fixture
reads, which is how two of the three fixtures that read the worked example came
to carry the wrong cell.

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
| FX-WorkedExampleChainPersists | fixture | 1.4 | the worked example's chain persists and reads back identical to the document's tables | WORKED_EXAMPLE §2, §5 |
| FX-CorporateActionMintsSuccessor | fixture | 1.5 | a split mints a stated successor with its predecessor recorded, the original row unchanged, and the lineage walk resolving all generations | authored |
| FX-GateRejectsAboveHeadroom | fixture | 2.4 | candidates breaching per-name headroom are rejected with a reason | WORKED_EXAMPLE §3 |
| FX-TotalCapRejectsAboveHeadroom | fixture | 2.4 | a candidate breaching the total committed-capital cap is rejected with a reason | authored |
| FX-AssignmentStressRejects | fixture | 2.4 | a candidate breaching the simultaneous-assignment limit is rejected with a reason | authored |
| FX-OffWatchlistRejected | fixture | 2.2 | no candidates for a non-member symbol | authored |
| FX-WorkedExampleEnumerates | fixture | 2.2 | the worked example's chain enumerates exactly the strikes §2 states and §3 claims, in identity order | WORKED_EXAMPLE §2, §3 |
| FX-WorkedExampleGateVerdicts | fixture | 2.3 | the worked example's chain gates to the verdicts §3 states, under the four contract constraints | WORKED_EXAMPLE §2, §3 |
| FX-CrossedQuoteRejected | fixture | 2.3 | a crossed quote is rejected with its own reason, not the spread cap | authored |
| FX-SpreadCapRejects | fixture | 2.3 | a candidate above the spread cap is rejected with its reason | authored |
| FX-PremiumFloorRejects | fixture | 2.3 | a candidate below the premium floor is rejected with its reason | authored |
| FX-GateRecordsAllReasons | fixture | 2.5 | a candidate failing two constraints carries both reasons | authored |
| FX-DeltaCeilingRejects | fixture | 2.3 | a candidate above the delta ceiling is rejected with its reason | authored |
| FX-DteWindowRejects | fixture | 2.3 | candidates on either side of the expiry window are rejected | authored |
| FX-EarningsClearanceRejects | fixture | 2.3 | a candidate whose life spans a buffered report date is rejected | authored |
| FX-CeilingNotInsidePolicyBand | fixture | 0.2 | the predicate holds that the delta ceiling is no tighter than any policy band | authored |
| FX-MaxDteBelowTrialBound | fixture | 0.2 | the predicate holds that MaxDte is below MaxTrialDays | authored |
| FX-GrossBasisBindsCallStrike | fixture | 2.4 | a call strike admitted by net basis and refused by gross basis is refused | WORKED_EXAMPLE §6.3 |
| FX-TrialCompleteIncludesAssignment | fixture | 3.4 | the assigned trial totals 498.05 | WORKED_EXAMPLE §6.3 |
| FX-RollCapCloses | fixture | 3.3 | a trial reaching the roll bound closes at market and resolves | authored |
| FX-ProjectionRebuildsFromLedger | fixture | 3.3 | `trials` and `positions` discarded and rebuilt from `ledger_entries` give the same rows, which is the condition on rewriting them at all [D-W35] | authored |
| FX-ExpiryResolvesAtOneCent | fixture | 3.3 | a short put closing one cent below its strike assigns; one closing at the strike expires worthless [D-W38] | authored |
| FX-AssignmentKnownNextSession | fixture | 3.3 | a decision on the day of assignment sees the pre-assignment state, and the following session sees the shares [D-W39] | authored |
| FX-ProceedsUsableOnSettlement | fixture | 3.3 | a trial closed by assignment cannot commit its proceeds on the session of the assignment and can on the following session [D-W40] | authored |
| FX-DividendReachesLedger | fixture | 3.3 | a dividend whose ex-date falls while a trial holds assigned shares produces a ledger entry, and one whose ex-date falls after the shares were called away does not [D-W41] | authored |
| FX-EarlyAssignmentOnDividend | fixture | 3.3 | a short call whose underlying goes ex-dividend by more than the call's remaining time value is assigned on the preceding session, and one where the time value is larger is not [D-W42] | authored |
| FX-CoveredCallCommitsNothingFurther | fixture | 3.3 | a trial holding assigned shares gates a call candidate against the committed capital it already carries, and the per-name headroom is unchanged by the call [D-W43] | authored |
| FX-OrdinaryDividendLeavesContractUnchanged | fixture | 3.3 | an ordinary dividend produces a ledger entry and no contract adjustment, and a non-ordinary one produces the adjustment its corporate action states [D-W44] | authored |
| FX-NextSessionSkipsAClosedDate | fixture | 3.3 | an assignment whose following date is absent from the calendar settles on the next date the calendar carries, and a date the calendar does not reach stops rather than resolving [D-W46] | authored |
| FX-UnmodelledActionStopsTheTrial | fixture | 3.3 | a merger on a held underlying stops the trial with the action recorded as its reason, and a split does not [D-W47] | authored |
| FX-StoredVocabulariesMatchTheirChecks | fixture | 3.3 | every declared stored vocabulary agrees with the CHECK enforcing it, and every vocabulary with no CHECK is named with its reason rather than skipped | authored |
| FX-StoppedTrialIsValuedAtTheClose | fixture | 3.3 | a trial holding shares that meets an unmodelled action reports entries summing to the marked value, not to the outlay [D-W49] | authored |
| FX-BoundClosePaysTheAsk | fixture | 3.3 | a forced close debits the ask, and a case where intrinsic and ask differ shows which was used [D-W49] | authored |
| FX-NoShareCountInOptionCash | guard | 3.3 | no file under `src/` prices an option from a share count: cash from a contract multiplies by the multiplier and the deliverable says only how many shares move [D-W17] | authored |
| FX-RunIsByteIdentical | fixture | 3.5 | a composed run produces byte-identical output across two invocations | authored |
| FX-RunRefusesAChoiceTheStateCannotHonour | fixture | 3.5 | a supplied choice a trial's state cannot accept is refused by name rather than skipped | authored |
| FX-NoNondeterministicSql | fixture | 3.5 | no SQL under `src/` calls a function whose value varies between runs, and the barred list is checked against the bundled binary | authored |
| FX-RecordCarriesFeasibleSet | fixture | 4.2 | a decision is re-scorable from its record alone | authored |
| FX-DecisionsShareOneFeasibleSet | fixture | 4.2 | two decisions made against the same symbol, session and right reference one stored set rather than two copies, and their portfolio verdicts differ where their books do | authored |
| FX-ThreeMakersSameFeasibleSet | fixture | 4.3 | all makers holding the same position in a name receive byte-identical candidate sets | WORKED_EXAMPLE §3 |
| FX-WorkedExampleDecisions | fixture | 4.3 | the three makers reproduce §4's choices from §3's feasible set, and the random maker's draw is reproducible from its seed alone | WORKED_EXAMPLE §4 |
| FX-RollAtTheThreshold | fixture | 4.4 | a maker acts at seven days and not at eight, rolls when a bound has not been reached, and closes when one has [D-W54] | authored |
| FX-TrialBoundsFixedAtOpen | fixture | 4.4 | a trial spanning a configuration change is bound by the values in force when it opened, a trial opened after the change is bound by the new ones, and a rebuild reaches the same verdict as the run | authored |
| FX-MakersDriveTheRun | fixture | 4.5 | three makers driving one chain produce three trials, three ledgers and one decision record, with no contract supplied by the test [D-W55, D-W56] | authored |
| FX-OneSetThreeBooks | fixture | 4.5 | two makers with different books receive the same candidate identities with the same contract-level reasons and different portfolio reasons, from one shared evaluation [D-W52] | authored |
| FX-ExcursionRecordedOnWin | fixture | 5 | a positive outcome still carries its adverse excursion | WORKED_EXAMPLE §6.2 |
| FX-NoAnnualizeInObjective | fixture | 5 | equal returns over unequal durations store identically | authored |
| FX-FastSlowDivergenceFires | fixture | 5 | inverted fast and slow rankings raise the monitor | WORKED_EXAMPLE §7 |
| FX-RegretUsesSlowScore | fixture | 5 | regret is computed from the trial-complete score | WORKED_EXAMPLE §8 |
| FX-NoLearnerOutputInJudgingPath | fixture | 6 | no judging component reads anything the learner produced | authored |
| FX-RiskDriftFiresOnBorrowedTail | fixture | 6 | falling regret with widening excursion raises the check | authored |
| FX-LearningBoundaryLagRespected | fixture | 7 | the learner sees only trials closed before the boundary | authored |
| FX-PreRegRequired | fixture | 9 | the forward run refuses to start without a committed pre-registration | authored |
