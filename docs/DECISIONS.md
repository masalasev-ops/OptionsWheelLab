# DECISIONS

The chronological register. Looked up, not read start to finish. The narrative
lives in `SYSTEM_DESIGN.md`.

Numbers are never reused and never renumbered. A superseded entry keeps its
number and gains a status pointing at what replaced it.

**Note on D-W1 to D-W13.** These numbers were first issued on 2026-07-12 in
SYSTEM_DESIGN.md v0.1, which is not recoverable. The entries below are authored
fresh on 2026-07-26 against the current design and should be treated as the
original definition of those numbers. Any external reference to a v0.1 D-W number
predates this file and cannot be relied on.

## Topical index

**Purpose and measurement**: D-W2, D-W3, D-W5, D-W17, D-W18, D-W20, D-W21, D-W49, D-W54
**Isolation and controls**: D-W1, D-W4, D-W6, D-W13, D-W41, D-W45, D-W52
**Data and identity**: D-W7, D-W8, D-W9, D-W15, D-W26, D-W27, D-W28, D-W29, D-W30, D-W31, D-W32, D-W34, D-W35, D-W36, D-W39, D-W44, D-W46, D-W47, D-W48, D-W52, D-W53
**Risk**: D-W10, D-W11, D-W14, D-W19, D-W23, D-W25, D-W37, D-W43, D-W53
**Gate constraints**: D-W10, D-W22, D-W23, D-W24, D-W25
**Settlement mechanics**: D-W38, D-W39, D-W40, D-W41, D-W42, D-W43, D-W44, D-W46
**Costs and fills**: D-W12, D-W49, D-W50
**Scope**: D-W12, D-W16, D-W45
**Verification mechanisms**: D-W28, D-W33, D-W51

## Status legend

`active` — in force.
`superseded-by D-Wnn` — replaced entirely.
`amended-by D-Wnn` — still in force as modified.

---

### D-W1 Separate repository and store
`active` · 2026-07-26, amended 2026-07-27

OptionsWheelLab is a separate repository with its own SQLite store, its own
Worker and Api, and its own snapshot directories. It shares the EODHD
subscription with AlphaLab and shares nothing else.

The project and namespace root are both `OptionsWheelLab`. The decision prefix
stays `D-W`, unchanged, because renumbering or re-prefixing a register breaks
every reference that already points at it.

Rationale: the two labs have different units of study and different verdict
horizons, and coupling their stores would make either one's schema changes a
hazard to the other.

---

### D-W2 The success criterion is improvement in decisions
`active` · 2026-07-26

The lab succeeds if the decision-maker's decisions measurably improve over time,
judged against a control that does not learn. Whether the wheel is profitable is
a secondary question, reported separately and never used as the success measure.

Consequence: the lab produces a falsifiable result regardless of whether any
configuration of the wheel beats its controls.

---

### D-W3 The unit of study is a decision
`active` · 2026-07-26

Every decision is recorded with the feasible set exactly as it stood, the
features of every candidate in it, the choice made, and which maker made it.

Consequence: the decision record is the primary artefact of the system, and its
loss is the one unrecoverable data loss in the design.

**A recorded decision is never rewritten.** The record exists so a decision can
be re-scored later from what stood at the time, which holds only if what stood at
the time is still there. An update in place would leave the record present and
the decision unrecoverable, which is the loss this decision names rather than a
different one.

Test FX-RecordCarriesFeasibleSet: a recorded decision can be re-scored from the
record alone, with no access to live state.

---

### D-W4 Three decision-makers run in parallel on identical data
`active` · 2026-07-26, amended 2026-08-04 (the property is conditional)

A frozen baseline, a random-within-band control, and the learner act every day on
the same feasible set with the same fill rules, keeping separate ledgers.

Rationale: without a frozen arm running the same schedule, an improving curve
cannot be distinguished from an environment that became easier.

Test FX-ThreeMakersSameFeasibleSet: on a day when the three makers hold the same
position in a name, all three are offered byte-identical candidate sets. Their
positions diverge by design and the divergence is the experiment, so the property
is conditional on the states coinciding and the test asserts it there.

---

### D-W5 The scorer scores every candidate that was available
`active` · 2026-07-26

After a trial closes, the outcome of every candidate in that day's feasible set
is computed, and the chosen candidate receives a rank and a regret within the set.

Rationale: this is the property that makes the wheel a usable environment for
measuring decision quality, and it is why the feasible set must be recorded in
full rather than summarised.

---

### D-W6 Firewall on learner output
`active` · 2026-07-26

No output of the learning channel reaches any component that judges the learner:
not the scorer, the feature grader, the improvement instrument, the risk drift
check, or the risk caps. Features derived from a previous learner decision are
excluded from candidate features.

Test FX-NoLearnerOutputInJudgingPath: a static check enumerating the judging
components' inputs and asserting none originates in the learning channel.

---

### D-W7 Data source
`active` · 2026-07-26

Option chains come from the EODHD options add-on, a marketplace product purchased
separately from the base subscription. Underlying bars, dividends, and the
earnings calendar come from the base plan.

Operational note: the base All-In-One plan does not include options data. The two
are separate purchases and only the add-on unlocks this lab.

---

### D-W8 Chain snapshots are append-only and point-in-time
`active` · 2026-07-26

A stored snapshot records what was observable on that date and is never
rewritten. Corrections arrive as new rows carrying their own observation stamp.

Test FX-NoRewriteOfAppendOnlyTables: no statement in `src/` deletes from or
updates a table the append-only vocabulary covers.

---

### D-W9 Watchlist membership is state, not a filter
`active` · 2026-07-26

The store records entry and exit dates for watchlist membership, and any query
about a past date resolves membership as of that date.

Rationale: applying today's watchlist retrospectively selects names that survived,
which excludes exactly the cases the risk machinery exists to catch and makes a
historical run incapable of failing.

Test FX-PitMembershipExcludesLaterJoiner: a name that joined after the as-of date
does not appear in that date's feasible set.

---

### D-W10 The risk gate lives inside the candidate generator
`active` · 2026-07-26

The generator enumerates candidates and then applies the risk gate, and the
survivors are the feasible set handed to all three makers.

Rationale: gating after the choice would give the three makers different effective
opportunity sets, so a difference between them would partly be permission rather
than judgement.

---

### D-W11 Risk caps are structural
`active` · 2026-07-26

Per-name committed capital, total committed capital, and the
simultaneous-assignment stress limit are configuration set by the operator. The
learner may not propose changes to them.

Rationale: the available sample almost certainly contains no crash, so any
conclusion the learner forms about tail risk is drawn from data that lacks the
tail. Risk the sample cannot price is controlled by structure.

---

### D-W12 Fill model
`active` · 2026-07-26

Sells at the bid, never the mid, with explicit per-contract commission and
assignment fees. The rule is fixed in advance and is not a tunable parameter.

Rationale: end-of-day granularity means the realised fill is never observed, and
filling at the mid manufactures an edge from the accounting alone.

---

### D-W13 Controls beyond the control makers
`active` · 2026-07-26

The lab also runs buy-and-hold on the same underlyings with the same capital over
the same window, and a hold-cash floor. These are reported separately from the
improvement measurement.

---

### D-W14 Rolling permitted, bounded
`active` · 2026-07-26

A position may be rolled, but a rolled chain terminates at a bound of `MaxRolls`
or `MaxTrialDays`, whichever binds first, at which point the position closes at
market. One trial runs from first open through to return to cash. A roll is
itself a recorded decision and is scored against the counterfactual of accepting
assignment.

Consequence: fixes the trial unit that the scorer and the walk-forward learning
boundary both depend on.

Open: `MaxRolls`, `MaxTrialDays`. Phase 0 config.

Test FX-RollCapCloses: a position reaching the bound closes at market and the
trial resolves to a scorable outcome.

---

### D-W15 Stored history has two separated uses
`active` · 2026-07-26

Stored chains serve first as a machinery harness, proving the loop correct, the
scorer leak-free, and the metric well behaved. A learn-and-validate split may
also be run, but its held-out result is committed as a pre-registered prediction
before the forward run begins and is not counted as evidence of improvement on
its own.

Rationale: two or three folds in one volatility regime cannot separate
improvement from fitting.

Test FX-PreRegRequired: the forward run refuses to start unless a hash-committed
pre-registration file exists and predates the first forward decision.

---

### D-W16 Wheel proper, ownership constraint retained
`active` · 2026-07-26

The candidate universe is restricted to point-in-time watchlist names the lab
would accept holding. Assignment is a designed leg, not an exit to be avoided.
General premium selling on non-watchlist names is out of scope.

Rationale: the ownership constraint is the primary structural risk control, and
the sample is unlikely to contain a crash, so risk is controlled by structure
rather than by inference from calm data. It also keeps buy-and-hold meaningful as
a benchmark.

Test FX-OffWatchlistRejected: the candidate generator emits nothing for a symbol
absent from watchlist membership as of that date.

---

### D-W17 Outcome metric: denominator and horizon
`active` · 2026-07-26, amended 2026-08-02 (the multiplier, sourced)

The outcome of a decision is return on capital committed at the strike, being
strike times the contract multiplier times contracts, measured from trial open
through to return to cash, with assigned shares marked into the same number.

Consequence: assignment is inside the metric rather than outside it, so the
strategy's downside cannot leave the measurement.

One hundred is the contract multiplier, and no adjustment changes it. OCC's
By-Laws once specified two methods, one of which reduced the strike and raised
the deliverable together: a 3-for-2 took a $60 option calling for 100 shares to
a $40 strike calling for 150. Those methods produced rounding windfalls and were
retired. Under the method in force, an adjustment moves the deliverable and not
"the strike prices or the values used to calculate aggregate exercise prices and
premiums", so the same 3-for-2 leaves a $50 option at $50 with a 150-share
deliverable, and an exercising call holder "would continue to pay $50 times
100". The same applies to reverse splits. So committed capital is strike times
the multiplier in every case, and a metric reading the deliverable would
misprice every adjusted position, which is the error this paragraph previously
asserted.

Source: Release No. 34-54748, File No. SR-OCC-2006-01, 71 FR 67415,
21 November 2006, for the proposed method and the retired ones; approved by
Release No. 34-55258, File No. SR-OCC-2006-01, 72 FR 7701, 16 February 2007.
Retrieved 2026-08-02.

Test: `WORKED_EXAMPLE.md` reproduces the figure to the cent.

---

### D-W18 Stored outcome is not annualized
`active` · 2026-07-26

The stored and optimized figure is the raw period return. Duration in days is
carried as a separate field. Annualization occurs only at report time.

Rationale: annualization inside the objective creates a preference for short
trades for arithmetic reasons rather than real ones.

Test FX-NoAnnualizeInObjective: two fixtures with identical return and differing
duration produce identical stored outcomes and differing reported annualized
figures.

---

### D-W19 Gross basis governs the covered call constraint
`active` · 2026-07-26, amended 2026-08-01 (boundary)

After assignment, cost basis is recorded both gross, with premium tracked
separately, and net, with premium reducing basis. The "above basis" constraint on
covered call strikes is evaluated against gross basis. Reports state which
convention they display.

A strike exactly at gross basis is admissible. The constraint exists to prevent
a call capping recovery below the cash outlay, and a strike at basis recovers it
exactly, so excluding it would forbid the break-even strike for no stated
reason. Called away at basis, the trial returns its premium and no capital loss,
which is the worst outcome this constraint is meant to permit rather than the
best it is meant to prevent.

Rationale: netting premium into basis permits call strikes below the cash outlay,
which caps recovery below entry and lets accumulated premium subsidise
progressively worse strike selection. The total remains positive while the banked
premium covers the gap, which is why the drift is easy to miss and has to be
prevented structurally rather than detected in the profit and loss.

Test FX-GrossBasisBindsCallStrike: a strike that net basis would admit and gross
basis rejects is rejected by the generator.

---

### D-W20 Two scores, learner optimizes the slow one
`active` · 2026-07-26

The scorer produces a fast mark-at-expiry score, comparable across every
candidate in a chain, and a slow trial-complete score per D-W17. The learner's
objective is the trial-complete score. The fast score serves cross-candidate
ranking within a day and is monitored for systematic divergence from the slow
score.

Open: divergence threshold and window. Phase 0 config.

Test FX-FastSlowDivergenceFires: a fixture in which fast and slow rankings
decouple raises the divergence monitor.

---

### D-W21 Adverse excursion recorded, not optimized
`active` · 2026-07-26

Maximum adverse excursion over the trial window, computed from daily underlying
bars, is recorded alongside every outcome and feeds the risk drift check. It is
not part of the learner's objective.

Rationale: the endpoint hides the path, so excursion is what separates a good
decision from a lucky one; making it an objective would invite the learner to
game it.

Test FX-ExcursionRecordedOnWin: a trial that expires worthless after going deep
in the money carries a large excursion on a positive outcome.

---

### D-W22 Contract-level liquidity filter
`active` · 2026-07-27, amended 2026-07-31 (crossed markets)

The gate rejects a candidate whose quoted spread exceeds
`Gate:MaxSpreadFractionOfMid` of the mid, or whose bid falls below
`Gate:MinPremium`. Proposed defaults are 0.12, being twelve percent of mid, and
0.30, both Phase 0.8 config.

It also rejects a quote whose bid exceeds its ask. A crossed market is not a
quote at a bad price but an artefact of a stale or broken feed, and it is the
sharpest case of the rationale below: its spread as a fraction of mid is
negative, so a cap meant to reject wide markets admits it, and it can present as
the best available while being untransactable. Recorded as its own reason rather
than as the spread cap, because a negative spread is not a spread above a cap and
the audit trail should not say it was.

Rationale, and the first reason matters more than the second. **The filter
protects the measurement, not only the trade.** The scorer computes an outcome
for every candidate in the feasible set [D-W5], and regret is measured against
the best of them. An illiquid contract carries a quote that does not represent a
transactable price, so admitting one corrupts the regret figure for every
decision that day, including decisions about liquid contracts, because the
untransactable candidate can present as the best available. Liquidity is
therefore a precondition of the counterfactual being meaningful.

The second reason is ordinary cost drag. Fills are at the bid [D-W12], so a wide
spread means the realised credit sits far below the mid the chain suggests, and
below some absolute premium the per-contract commission consumes a large
fraction of the credit.

Consequence: the gate rejects on both grounds independently and records every
failing reason rather than the first, so a candidate failing two constraints
shows both.

Test FX-CrossedQuoteRejected: a quote whose bid exceeds its ask is rejected with
the crossed reason recorded.
Test FX-SpreadCapRejects: a candidate whose spread exceeds the cap is rejected
with the spread reason recorded.
Test FX-PremiumFloorRejects: a candidate whose bid falls below the floor is
rejected with the premium reason recorded.
Test FX-GateRecordsAllReasons: a candidate failing two constraints carries both
reasons, not one.

---

### D-W23 Delta ceiling in the gate
`active` · 2026-07-27, amended 2026-07-27 (enforcement point)

The gate rejects a candidate whose absolute delta exceeds `Gate:MaxDelta`.
Proposed default 0.35, Phase 0.8 config.

Rationale: delta is the best available proxy for assignment probability, and
premium rises with it. A learner rewarded on outcomes in a calm sample will drift
toward higher delta, because in calm conditions that is simply better. That drift
is the failure mode the risk drift check detects [D-W21]; the ceiling makes the
worst of it impossible rather than merely visible after the fact. It is structural
and not learner-proposable [D-W11].

The ceiling is an outer bound on catastrophe, not a strategy parameter, so it
must be set no tighter than the loosest policy band in use [D-W4]. A ceiling
inside a policy band would silently override that policy rather than bound it.

**Settled at 0.8.** Both are held at 0.35, and the coincidence is now
deliberate. The control spans what the gate admits, because a control
drawing from a smaller opportunity set than the gate allows would make a
difference between it and the learner partly permission rather than
judgement [D-W4], which is the failure [D-W10] names in the neighbouring
case.

The lower bound is a separate question and this argument does not reach
it. There is no `Gate:MinDelta`, so `Policy:Random:DeltaMin` at 0.10 sits
strictly inside what the gate admits and the control is bounded below by
a chosen number. It is inherited from `WORKED_EXAMPLE.md` §1 rather than
argued. Whether `Gate:MinPremium` already excludes most of what lies
below it is a measurement over real chains, and belongs to the phase that
has them.

**Amendment, 2026-07-27.** This decision originally said the invariant is
checked at startup. That wording assumed a value bound once at boot. Under
[D-W27] the operands are config rows, which are versioned and insertable while
the process runs, so a startup check leaves every later version unguarded.
Enforcement is at config-write time: a version violating the invariant is
refused rather than recorded and detected on a later boot. Startup remains a
cheap backstop against a store written by something else, but it is the
secondary guard. The invariant itself is unchanged.

Test FX-DeltaCeilingRejects: a candidate above the ceiling is rejected with the
delta reason recorded.
Test FX-CeilingNotInsidePolicyBand: the predicate holds that `Gate:MaxDelta` is
no tighter than any configured policy band's upper bound.

---

### D-W24 Days-to-expiry window in the gate
`active` · 2026-07-27, amended 2026-07-27 (enforcement point), amended
2026-07-31 (inclusive range)

The gate rejects a candidate whose days to expiry fall outside the inclusive
range `Gate:MinDte` to `Gate:MaxDte`. Proposed defaults 7 and 70, Phase 0.8
config.

Rationale for the upper bound: capital sits committed for the whole life of the
contract, and long-dated contracts carry wider spreads while returning less per
day committed.

Rationale for the lower bound: at end-of-day granularity the fill model is least
defensible for contracts about to expire [D-W12], and the assignment model is an
approximation whose error is unmeasured [`VALIDITY.md` §5]. Very short-dated
contracts are where both assumptions are weakest.

**Constraint relationship, and it binds.** `Gate:MaxDte` must be less than
`Trial:MaxTrialDays` [D-W14]. An opening contract longer-dated than the trial
bound would guarantee a forced close at market before its own expiry, which makes
the trial's outcome an artefact of the bound rather than of the decision.

**Amendment, 2026-07-27.** Enforcement moves from startup to config-write time,
for the reason stated in the amendment to [D-W23]. The invariant is unchanged.

Test FX-DteWindowRejects: candidates on either side of the window are rejected
with the DTE reason recorded.
Test FX-MaxDteBelowTrialBound: the predicate holds that `Gate:MaxDte` is less
than `Trial:MaxTrialDays`.

---

### D-W25 Earnings clearance in the gate
`active` · 2026-07-27, amended 2026-07-31 (buffer edge)

The gate rejects a candidate whose contract life contains a scheduled earnings
date, plus a buffer of `Gate:EarningsClearanceDays` on either side. Proposed
default 7, Phase 0.8 config.

Rationale: an earnings report is a scheduled binary event capable of producing a
single-session move larger than a year of collected premium. It is exactly the
class of tail the available sample cannot price [D-W11], and a learner would draw
the wrong lesson from any sample in which the events happened to resolve mildly.

**This forecloses a question the lab could otherwise have asked, and the trade is
deliberate.** Implied volatility is elevated ahead of earnings, so whether
harvesting that elevation pays is a genuine and learnable question, and gating it
removes it from study. It is being given up because a wrong answer is expensive
in a way the instrument cannot detect, while the cost of not asking is only a
narrower research question. Revisit if the forward record ever spans enough
earnings cycles to price them, which will take years rather than months.

Buffered on both sides because the calendar date itself moves. A vendor date can
shift by days, and an unbuffered filter would admit a contract that turns out to
span the report.

The buffer is inclusive of its edge: a report exactly
`Gate:EarningsClearanceDays` from either end of the contract's life is inside the
window and rejects. A window excluding its own edge would admit a report at
precisely the distance the buffer was sized for, which is the case the buffer
exists to catch, and the shift it guards against moves a date by days rather than
by fractions of one.

Note the comparisons here are not uniform across the gate and each is stated
where it binds: the spread cap and the delta ceiling reject on "exceeds", the
premium floor rejects a bid strictly below it [D-W22], which WORKED_EXAMPLE §3
corroborates rather than supplies, the expiry window admits its own bounds
[D-W24], and this buffer includes its edge. They differ because the quantities
differ, so the rule is that each decision states its own boundary rather than
that one convention governs.

Test FX-EarningsClearanceRejects: a candidate whose life contains a report date
within the buffer is rejected with the earnings reason recorded.

---

### D-W26 Configuration is resolved as-of, never as-now
`active` · 2026-07-27

Any component reading configuration on behalf of a simulated date resolves it as
of that date, being `MAX(version)` among rows whose `set_at` is at or before it,
rather than reading the current value. There is no code path that serves a
simulated date from current configuration.

Rationale, and it is structural rather than defensive. The lab's own workflow
changes configuration: a policy revision inserts a new version [D-W4], and
Phase 0.8 values are expected to be revised. So by the time any tool re-scores,
replays, or audits an earlier session, current configuration differs from what
that session ran under. A tool reading current config fails its own
reproduction check, and the failure presents as impure inputs rather than as a
configuration-resolution bug, which is the wrong place to look. The sibling
project found this after the misdiagnosis.

Same class as the leakage rule [D-W6]: an input that must be read as-of and not
as-now.

**Known limit, recorded rather than solved.** Values bound from `appsettings`
rather than stored as config rows are not as-of resolvable at all, so
reproducing an earlier session rests on those blocks being unchanged, which the
store cannot verify. Any tool that reproduces prior sessions records the
appsettings-bound values it ran under alongside its output, so a later parity
failure can be checked against them instead of being routed into a store
investigation.

Resolution is inclusive of the as-of instant, matching `observed_at <= as_of`
for every other as-of read. An earlier wording said "precedes", which read as
strict inequality and would have made configuration written on a simulated date
invisible to that date.

**A written version is never altered.** Resolving as-of a past date answers what
was in force then, which means anything only if a version, once written, still
says what it said. An update in place would not make a past answer wrong so much
as unverifiable, since nothing would record that it had changed. `config_rows` is
therefore append-only on this decision's authority: a correction inserts
version + 1, and the store enforces it rather than trusting the caller.

Test FX-ConfigResolvesAsOf: a key with three versions resolves to the version in
force on the simulated date, not the newest.
Test FX-NoCurrentConfigReadOnSimulatedPath: a static check that no component
serving a simulated date calls the current-value accessor.

---

### D-W27 Configuration storage class follows the read path
`active` · 2026-07-27

A configuration value read while producing or scoring a simulated decision is
stored as a config row and resolved as-of [D-W26]. Every other value is bound
from `appsettings`.

By that criterion: `Risk`, `Gate`, `Costs`, `Policy`, `Trial` and `Scoring` are
config rows, because each is read on a simulated-date path. `Eodhd` is
appsettings, along with connection strings, log levels, and paths, none of which
participate in a decision.

Rationale: the classification follows from how a value is READ, not from who is
permitted to change it. Those are separate questions and conflating them is the
available mistake. [D-W11] says the learner may not propose changes to the risk
caps, which is a statement about authority and is enforced by what the learning
channel may write [D-W6]. It says nothing about resolution. An operator raising
a cap in September is legitimate under D-W11, and re-scoring an August decision
still needs the August value. Binding the caps from a file would import D-W26's
known limit onto the most consequential values in the lab, where a later parity
failure would be hardest to diagnose.

Consequence: a registered options class bound from `appsettings` is itself a
current-value accessor, whether or not anything consumes it yet. So a section
classified as a config row is never given an appsettings-bound options type as a
placeholder, because that creates the second path D-W26 exists to prevent.

Consequence for enforcement: cross-key invariants over config rows are enforced
when a version is written, not at startup. See the amendments to [D-W23] and
[D-W24].

**Bootstrap values are `app` by necessity, not by criterion.** A value the
process needs in order to open the store cannot be stored in the store, so the
store's own location, its journal mode, and anything else consumed before the
first query are `app`-classed regardless of what reads them later. The read-path
criterion does not reach them, and saying they participate in no decision is the
weaker argument: the connection factory is on every path including simulated
ones. The reason is circularity, not irrelevance.

Test FX-ConfigStoreClassHonoured: no options type bound from `appsettings`
exists for a section classified as a config row.

---

### D-W28 Snapshots are taken with VACUUM INTO
`active` · 2026-07-28

A store snapshot is produced by `VACUUM INTO` a timestamped file, not by copying
the database and its write-ahead log.

Rationale. The file-copy form required an exclusive lock held across the copy,
to stop a writer tearing it. That lock byte-range locks the `-shm` file, so the
lock and the three-file copy specified together were not jointly satisfiable,
and the implementation had to drop `-shm` and record a departure. `VACUUM INTO`
runs in a read transaction: it is atomic, blocks no writer, needs no lock, and
produces one file rather than a set whose members can disagree.

Cost, accepted. The result is a defragmented rebuild rather than a
byte-identical copy, so a snapshot cannot be compared to its source by hash, and
a corrupt source is rebuilt rather than preserved for forensics. Nothing in this
corpus asks for either, and a rollback artefact needs logical identity rather
than byte identity.

Timing. Recorded at 0.3, when the store holds one migration and almost nothing
else. The mechanism only becomes more expensive to change as data accumulates,
and by Phase 8 the store carries forward data that cannot be reconstructed.

A snapshot is a single file named `snapshot-<filename-form timestamp>.db`,
sitting beside the store. It is a complete standalone database, so it can be
opened and inspected directly rather than restored first, and a restore is a
file copy.

Test FX-SnapshotRestoresIdentically: a store snapshotted, mutated, and restored
from the snapshot resolves the same values it did before the mutation.
Test: a snapshot taken while a reader holds the store succeeds.
Test: a snapshot taken while a writer holds the store succeeds, and the snapshot
contains the committed state and not the uncommitted.

---

### D-W29 Stored decimals are canonical and are not ordered in SQL
`active` · 2026-07-28

A decimal stored in a `TEXT` column is written in one canonical fixed-scale
form, so a given number has exactly one stored representation. No query orders,
ranges over, or aggregates a decimal column; comparison and arithmetic happen in
code after parsing.

Canonical because identity depends on it. An option contract's identity is the
tuple of underlying, expiry, right and strike, and strike is a decimal. `50` and
`50.00` round-trip to the same value and are different `TEXT`, so a
non-canonical form would give one contract two identities and split its history
without failing.

Not ordered in SQL because the form is not order-preserving. Lexicographic
comparison puts `"9.50"` above `"10.00"`, and negatives invert again. An
order-preserving encoding is possible but costs readability in every column for
a property only some queries want.

The scale is a fidelity requirement for vendor-supplied values and a rounding
policy for computed ones. A vendor value refuses rather than rounds, because a
scale below the vendor's precision must fail ingestion rather than lose a digit
quietly. A computed value rounds explicitly, because division is non-terminating
in general and no finite scale is sufficient for a ratio. One entry point could
not be both, so there are two and the caller chooses.

Every decimal reaching a `TEXT` column passes through the canonical form. No
call site formats a decimal itself.

Consequence, recorded so it is inherited rather than rediscovered. Phase 5 ranks
candidates and Phase 6 aggregates outcomes. Neither may do so in SQL over a
decimal column. If SQL-side ranking is wanted then, it is a schema decision
taken with the need visible, not a property assumed now.

Test FX-MoneyRoundTrip: adversarial decimals survive storage, and equal values
written differently store identically.
Test FX-NoDecimalOrderingInSql: a static check that no SQL in the codebase
orders, ranges over, or aggregates a decimal column.

---

### D-W30 The clock tells wall-clock time and nothing else
`active` · 2026-07-28

The injected clock returns the instant at which the process is running. A
simulated date is never obtained from it. Simulated dates arrive as parameters
and are threaded through, exactly as configuration is resolved as-of a date
rather than as-now [D-W26].

Rationale, and it is the same failure in a new place. The lab has two kinds of
time and they are unrelated: when this run is happening, and which day is being
simulated. A component that wants the second and reaches for the clock gets the
first, and the answer is plausible, non-null and wrong. That is the leakage
D-W26 forbids, arriving through a different door.

Consequence for placement. The clock is read at composition and entry points
only. Nothing below them reads a clock; they take instants as parameters, which
is the shape 0.3 deliberately gave `set_at` and the migration instant. This keeps
the change at 0.5 a wiring change, and keeps tests supplying a fixed instant
directly rather than through a fake.

Out of scope, stated. Converting an instant to a trading date needs a market
calendar and a session timezone. That is Phase 1. This clock knows nothing about
trading days.

Test FX-NoAmbientClock: no ambient clock call outside the permitted file.
Test FX-ClockIsNotADateSource: a static check that no simulated-date path
derives its date from the clock.

---

### D-W31 Synthetic chains are written by hand, and the format serves that
`active` · 2026-07-29

A synthetic chain is authored by a person, not generated. The format optimises
for being written and read by hand, and pays for that in loading cost rather than
the reverse.

Rationale. These exist so that assignment, early exercise and roll-cap cases can
be constructed deliberately rather than waited for [`SYSTEM_DESIGN.md` §7]. A
case nobody can write is a case nobody constructs. Mirroring the schema loads
trivially and turns a five-strike chain into a wall of repeated columns; a domain
shape reads as a chain and costs the loader some work. The loader is written once
and the chains are written every time a case is needed.

**Values are in the canonical stored forms** [D-W29, `DATA_AND_SCHEMA.md` Time].
A hand-written number that reads back as a different number defeats the reason
for writing it by hand, so a value beyond scale is a malformed chain rather than
one to round.

**The loader yields in `ContractIdentity`'s order** and never in file order
[D-W4]. Hand-written files get reordered by whoever edits them, and an order that
depends on the file changes when someone tidies it.

Scope. This governs what the format optimises for, not what the format is. The
format is checkpoint detail and may change without superseding this, so long as
the property holds.

Test: `WORKED_EXAMPLE.md`'s chain is expressible in the format and round-trips
through the loader.

---

### D-W32 The migration ledger is never rewritten
`active` · 2026-07-29

`schema_migrations` records which migrations have been applied. Rows are appended
when a migration runs and are never updated or deleted.

Rationale, and it is not the reason the other append-only tables have. A store's
schema version is not stated anywhere; it is derived from what the ledger says
has been applied. A ledger that can be rewritten therefore makes a store unable
to answer what it is, and a snapshot taken before a rewrite and one taken after
would restore to different schemas while both claiming the same version. That is
why the ledger sits in the vocabulary alongside the tables it has nothing else in
common with.

**Scope, stated because the boundary is easy to overrun.** This does not
establish a general rule that every table recording something past is
append-only. `watchlist_membership`, `positions` and `trials` carry
effective-dating, where closing a row is how a state change is recorded rather
than a rewrite of an observation, and whether that counts is already owed at
Phase 1. All three are named, because listing two of three reads as a boundary
rather than as an example. Each decision
states the property for its own tables and for its own reason, and no general
rule is inferred from there being several. The reasons genuinely differ, and a
single rule covering all of them would have to be vague enough to cover cases it
should not.

Test FX-NoRewriteOfAppendOnlyTables: covered by the vocabulary entry.

---

### D-W33 The source guards stay a text scan and a fixture
`active` · 2026-07-29

Checks are enforced by two mechanisms and neither is replaced by a Roslyn
analyser. A named check in `guards.ps1` scans text and runs before restore. A
fixture reads structure and a vocabulary and runs under the test suite.

Measured rather than argued, and the first measurement refuted the reason the
split was given. `guards.ps1` claimed a guard must fail even when the build does
not. An analyser probe with a violation in one file and a type error in another
reported both, so that is false. What an analyser cannot survive is a failed
restore, where none runs and only the NuGet error appears. The script runs before
restore, so its property is that it reports when restore does not succeed.
Narrower, true, and enough.

One check of four would gain. Inferred types for the floating-point guard, which
is its documented blind spot; alias resolution for the clock guard, which is
marginal; nothing for either SQL check, because an analyser returns the same
string literal a fixture already has and does not parse SQL. So "one mechanism
serving four" does not survive contact with what the four are.

Cost, weighed and declined. `Microsoft.CodeAnalysis.CSharp` restores clean
against the audit, and brings ten transitive packages that this repository's
central pinning would each require a pin for, plus a `netstandard2.0` project
overriding `Directory.Build.props`.

What would reopen this. The floating-point guard's blind spot is real and
unclosed: a `double` reached through inference is invisible to every mechanism
here. If that becomes a live defect rather than a documented gap, the comparison
changes for the check that has it and not for the other three.

---

### D-W34 A write that makes an invariant unevaluable is refused
`active` · 2026-07-29

A configuration write is refused when it touches a key belonging to a cross-key
invariant and the store would not then hold every key that invariant needs. A
write touching no such key is permitted regardless of what else is absent.

Rationale. An invariant over two keys cannot be evaluated while only one exists,
so a rule that simply skips the check passes vacuously until the last key lands,
which is the state the enforcement exists to prevent. The alternative is to make
seeding atomic and rely on that, but then the protection belongs to the seeder
rather than to the write path, and every later phase that writes configuration
would have to know to reproduce it. Make it unwritable rather than detectable.

**Consequence, and it is the point.** Neither `Gate:MaxDte` nor
`Trial:MaxTrialDays` can be written alone, since either alone leaves D-W24
unevaluable and touches its keys. The pair is therefore atomic by the write path
rather than by the seeder's discipline. The same holds for `Gate:MaxDelta` and
the policy bands under D-W23.

Scoped narrowly on purpose. A write touching no invariant key is permitted into
an empty store, so a store can be built up in any order that does not split a
pair. Refusing every write while any invariant is unevaluable would block an
unrelated key for a reason that has nothing to do with it.

Test FX-ConfigWriteRefusesInvariantBreach: extended to cover the unevaluable
case, not only the violating one.

---

### D-W35 Records are append-only; projections may be rebuilt
`active` · 2026-07-29, amended 2026-08-03 (more than one source)

A **record** is the only place a fact is held. Rewriting it destroys the fact, so
a record is append-only: a change appends a new version and nothing already
written is altered. `watchlist_membership` is a record.

A **projection** is derived from an append-only source. Rewriting it destroys
nothing, because it can be rebuilt. `trials` and `positions` are projections of
`ledger_entries` and may carry a nullable close column and be updated in place.

A projection may derive from more than one append-only source. `trials` derives
from `ledger_entries` for everything a trial did and from `decisions` for which
maker did it, because `maker_id` is a fact about a decision and no ledger entry
carries it [§4.3]. The rebuild condition is unchanged: every source it reads must
itself be append-only, and a projection deriving from anything rewritable is not
a projection.

Rationale. The lab exists so a decision can be re-scored later from what stood at
the time, which holds only if what stood at the time is still there. That argument
reaches a record and does not reach a projection, and treating both alike would
cost query complexity everywhere for a guarantee only one of them needs.

**The condition, and it is not free.** A projection may be rewritten only where a
test discards it, rebuilds it from its source, and gets the same rows. Without
that test it is not a projection, it is a rewritable table with a flattering name.
The test also proves the ledger's `kind` vocabulary carries enough to rebuild
from, which nothing else checks.

Membership on re-entry. A name that leaves and returns appends a further version,
so the key is the symbol and a version as `config_rows` has. Keying on the symbol
alone cannot express it.

Test FX-PitMembershipExcludesLaterJoiner: covered.
Test FX-ProjectionRebuildsFromLedger: registered at Phase 3, where
`ledger_entries` first has entries to rebuild from.

---

### D-W36 Adjusted contract terms are recorded, never derived
`active` · 2026-07-30

When a corporate action adjusts a contract, the successor's strike,
deliverable and multiplier are transcribed from what the adjusting
authority states. Nothing in this lab computes an adjusted term from a
ratio, at ingest, at minting, or anywhere else. The `corporate_actions` row
records the event; the successor contract row records the stated terms; the
ratio is a recorded fact about the event and never an input to arithmetic.

Rationale, from the primary record. OCC publishes the adjusted terms per
event in an Information Memo: the 2026 StoneX 3-for-2 sequence (memos 58376
and 59086) states the new 150-share deliverable and a table of adjusted
strikes. The methodology is era-dependent, not a formula: memo 26853
records the September 2007 change made precisely to eliminate strike
rounding for splits other than 2-for-1 and 4-for-1, and the SEC notice
preceding it records that the earlier rounding to eighths produced windfall
profits for one side and losses for the other. A lab that derives encodes
one era's method, is wrong for the others, and reproduces a documented
source of silent economic error. This project also derived twice during
design and was wrong both times, which is the local demonstration.

**The refusing decimal path becomes the tripwire.** A stated strike is an
exact decimal and stores through `StoreDecimal.ToStored` unchanged. A
derivation that produces a non-terminating value cannot be stored at all,
so record-not-derive is enforced by the seam that already exists rather
than by review. The obligation's dilemma, round a value inside identity or
carry the ratio, is dissolved rather than decided: neither operation ever
runs.

Scope. The Phase 3 metric question, whether committed capital uses the
multiplier or the deliverable, is unaffected and stays open: both columns
are recorded as stated, so either answer reads a transcribed value.

Test FX-CorporateActionMintsSuccessor: covered.

---

### D-W37 A constraint that cannot resolve its bound stops the evaluation
`active` · 2026-07-31

When a gate constraint cannot resolve its bound as of the simulated date, the
evaluation fails with a message naming the key and the date. It does not admit
the candidate, and it does not reject it.

Rationale. Admitting silently drops a structural risk control [D-W11] and leaves
a run that looks normal and is unconstrained. Rejecting presents a
misconfiguration as an absence of opportunity, and a run of empty feasible sets
is indistinguishable from a quiet market. Neither is recoverable from the
record, which is what the record exists for [D-W5]. This is the read-side of
[D-W34]: a write that leaves an invariant unevaluable is refused, and a read
that leaves a constraint unevaluable stops for the same reason.

Bounds are resolved once per evaluation rather than per candidate, so an
unresolvable bound produces one message rather than one per contract.

Test: a constraint evaluated at a simulated date before its bound was written
fails naming the key and the date.

---

### D-W38 Expiry resolves by exercise at one cent in the money
`active` · 2026-08-01

A short option expiring one cent or more in the money against the session's
closing price is assigned. One expiring out of the money, or in the money by
less than one cent, expires worthless.

Source. OCC Rule 805's exercise-by-exception threshold for equity options is
one cent, set by Release No. 34-57163, File No. SR-OCC-2007-18, which amended
Rule 805 to reduce from $.05 to $.01 the threshold amount used to determine
the equity options deemed in the money for that processing, published at
73 FR 4297, 24 January 2008. The same filing states the in-the-money test:
the difference between the exercise price and the closing price of the
underlying equity interest on the last trading day before expiration.
Retrieved 2026-08-02.

Retrieved 2026-08-01 from the Options Industry Council's exercise reference,
which states that OCC uses the one-cent threshold for the positions of its
clearing members as an administrative convenience and that a firm may use a
different one. So it is a procedure between OCC and its clearing members
rather than a rule binding an account, and Rule 805 Interpretation .02 says
as much: the thresholds are not intended to dictate to clearing members
which positions in customers' accounts should or must be exercised.

The lab models the common case and records that it is a model. A contrary
exercise advice, by which a holder declines an in-the-money exercise or
exercises one that is out of the money, is not modelled: it is a choice made
by the holder of a contract the lab is short, and the lab cannot observe it.

Test FX-ExpiryResolvesAtOneCent: a short put closing one cent below its
strike assigns; one closing at the strike expires worthless.

---

### D-W39 Assignment occurs at a session's close and is known the next morning
`active` · 2026-08-01

Assignment is determined against net short positions after the close of
session D and is not known to the account until the morning of the next
business day. No decision made on D may depend on an assignment that
occurred on D.

Source, the clearing layer. Rule 803 assigns exercise notices to Clearing
Members in respect of positions in a particular account, or a particular
sub-account where an account is divided, which is its Interpretation .01. The
method is deliberately outside the rule: SR-OCC-95-16 amended Rule 803 to
eliminate the reference to random selection as the means OCC uses, and states
that the assignment procedures "will be a stated policy, practice, or
interpretation of proposed OCC Rule 803 and will not be set forth in Rule 803"
(Release No. 34-36453, File No. SR-OCC-95-16, 60 FR 56625, 9 November 1995).
Neither does any rule reached here state that assignment is determined against
net positions after the close of the market each day, so that a short position
bought back before the close cannot be assigned that day; that is retrieved
2026-08-01 from the Options Industry Council's assignment reference, as is the
description of OCC allocating to clearing members in the early hours.

Source, the account layer. Rule 804 requires each Clearing Member to establish
fixed procedures for allocating assigned exercises to specific short positions,
"in accordance with the requirements set forth in Exchange Rules and any
applicable rules of any self-regulatory organization of which the Clearing
Member is a member", and its reporting provision names each writer to whom the
member allocated an exercise assigned "on the preceding business day". So the
member's own procedures govern which account is assigned, and no OCC rule fixes
when a customer is told: the next-business-day shape is what that reporting
deadline assumes rather than what any rule requires. Retrieved 2026-08-01 from
Exhibit 5B of File No. SR-OCC-2024-013.

The lab models the common case of next-session notification and records that
the timing is a broker's procedure rather than a rule, which is the disclosure
D-W38 makes about the one-cent threshold not binding an account.

Rationale, and it is [D-W8] applied to the account rather than to the market.
A maker that reacted to its own assignment on the day it happened would be
reading a fact that did not exist yet, which is the leak an as-of read exists
to prevent. The same discipline the store applies to what the market showed,
applied to what the account knew.

Consequence for the state machine. An assignment carries two dates: the
session it occurred in, and the session the account may act on it. Both are
stored, because a projection rebuilt from the ledger [D-W35] must reproduce
what was known when, and one date cannot answer both questions.

Test FX-AssignmentKnownNextSession: a decision on the day of assignment sees
the pre-assignment state, and the following session sees the shares.

---

### D-W40 Proceeds from an assignment or a call-away are usable the next session
`active` · 2026-08-02

Cash and shares from an assignment or a call-away settle on the first business
day after the session the exercise occurred in. That is the session the account
first learns of the assignment [D-W39], so a trial may commit the proceeds on
the session it learns of them and not before.

Source, the settlement cycle. Rule 15c6-1(a) as amended requires that a broker
"not effect or enter into a contract for the purchase or sale of a security ...
that provides for payment of funds and delivery of securities later than the
first business day after the date of the contract", with a compliance date of
28 May 2024. Release No. 34-96930, File No. S7-05-22, 88 FR 13872, 6 March 2023.
Retrieved 2026-08-02.

Source, the exercise leg. An exercise is a clearing event rather than a purchase
or sale, so the cycle above does not reach it and OCC's own rule does. The order
approving OCC's conforming changes records that, for transactions settling on a
broker-to-broker basis, OCC changed "the delivery date for physically-settled
options under OCC Rule 903 from the 'second' to the 'first' business day
following exercise", implemented on the Commission's compliance date. Release
No. 34-99701, File No. SR-OCC-2024-002, 89 FR 18685, 14 March 2024. Retrieved
2026-08-02.

What neither reaches. When a broker makes settled proceeds available to trade
against is house policy and margin treatment rather than a settlement cycle, and
no rule fixes it. The lab models proceeds as usable on the settlement session and
records that as a model, which is the disclosure [D-W38] makes about the one-cent
threshold and [D-W39] about notification.

Scope. "The first business day after" needs a session calendar and this lab has
none. The only session sequence in the store is `underlying_bars.session_date`,
which is per symbol and cannot distinguish a market holiday from a name that did
not trade. What that calendar is, and whether it is derived or stored, is owed at
3.3 rather than settled here.

Nothing reads this yet, and saying so is cheaper than leaving it to be found.
Committed capital is bounded against equity rather than against settled cash
[D-W11], so no maker asks whether cash has cleared. The mechanic is recorded
because the state machine needs it the first time a trial opens on the session
after a close, not because a path consumes it today.

Test FX-ProceedsUsableOnSettlement: a trial closed by assignment cannot commit
its proceeds on the session of the assignment and can on the following session.

---

### D-W41 Dividend entitlement is fixed at the record date, and a dividend is ledgered
`active` · 2026-08-02

A holder entitled to a dividend is one holding the shares before the ex-dividend
date, which under a one-day settlement cycle is the record date itself. A
dividend received while a trial holds assigned shares is recorded in
`ledger_entries`, and the buy-and-hold control receives its dividends on the same
basis [D-W13].

Two questions, and only the first has an authority.

Source, the entitlement. FINRA Rule 11140(b)(1) as amended: the date designated
as the "ex-dividend date" "would be the record date if the record date falls on a
business day, or the first business day preceding the record date if the record
date falls on a day designated by the Committee as a non-delivery date". Filed
for immediate effectiveness with an operative date of 28 May 2024. Release No.
34-99075, File No. SR-FINRA-2023-017, 88 FR 85678, 8 December 2023. Retrieved
2026-08-02.

What no rule reaches, and the lab decides. Whether a dividend enters the record
at all is this corpus's question rather than a market's. It does, in both places:
a dividend paid between assignment and call-away is cash the trial received, and
recording it against the trial while leaving the control's return untouched would
bias the exact comparison the lab exists to make, in one direction. Chosen, not
transcribed.

Three things this needs and none of them lands here. `ledger_entries.kind` needs
a `dividend` value; `CorporateActionKind` is `Split` only and names this decision
in its own remarks; and the synthetic-chain format carries no corporate actions
at all, so no hand-written scenario can express a dividend today. Naming them is
what this decision owes the checkpoint that adds them.

Test FX-DividendReachesLedger: a dividend whose ex-date falls while a trial holds
assigned shares produces a ledger entry, and one whose ex-date falls after the
shares were called away does not. The control's half is asserted where the
control is built, which is not Phase 3.

---

### D-W42 Early exercise around ex-dividend is modelled, and nothing is cited
`active` · 2026-08-02, amended 2026-08-02 (scoped to unadjusted dividends)

A short call is assigned on the session before the ex-dividend date when the
dividend exceeds the call's remaining time value **and the contract is not
adjusted for that dividend** [D-W44]. No other early assignment is modelled.

Scope, narrowed by 3.2's completeness pass. Early exercise to capture a dividend
is the behaviour OCC's own rationale describes for dividends it does not adjust
for: "If adjustments are not made in response to special dividends (i.e., by
calling for the delivery of the dividend) call holders can capture the dividends
only by exercising their options." Where the contract is adjusted the holder
receives the dividend through the deliverable and has no reason to surrender the
option's time value, so the condition below applies to unadjusted dividends and
the adjusted case has no early assignment at all.

The absence is the source. Whether the holder of a long call exercises it early
is that holder's decision, and no rule governs the making of it. OCC Rule 803
assigns an exercise notice to a Clearing Member once one is made and Rule 804
leaves the allocation to that member's own fixed procedures [D-W39]; both
describe what happens after a choice this lab cannot observe. So this decision
cites nothing, which is a different thing from citing weakly.

The condition is chosen, not transcribed. A holder who exercises early captures
the dividend and gives up the option's remaining time value, so the exchange is
worth making when the first exceeds the second. That is the standard reasoning
and it is an approximation: it assumes a holder who acts on it, ignores what
acting costs, and could not see intraday exercise in end-of-day data in any case.
`VALIDITY.md` records the error as unmeasured and this decision does not improve
on that.

Test FX-EarlyAssignmentOnDividend: a short call whose underlying goes ex-dividend
by more than the call's remaining time value is assigned on the preceding
session, and one where the time value is larger is not.

---

### D-W43 A covered call commits nothing beyond the trial's committed capital
`active` · 2026-08-02

A covered call written against shares a trial already holds commits no further
capital. The trial's committed capital was fixed when the put was sold [D-W17]
and the shares are what that capital bought, so the portfolio caps read one
figure per trial from open to close and it does not change when the leg changes.

Chosen, not transcribed. No authority states this: it is a modelling choice about
how the lab measures its own exposure, on 2.4's distinction, and the only one of
3.1's seven mechanics with no external source of any kind.

Why it is the conservative reading rather than the permissive one. The
alternative charges a call its own committed figure, which would count the same
capital twice in the same trial and make the per-name cap bind on a leg that ties
up no cash. The risk a covered call carries is not that it commits capital but
that it caps recovery below the outlay, and that is [D-W19]'s gross-basis
constraint rather than a capital cap.

What this does not decide. Whether writing a call reduces the trial's committed
capital, by the credit received or otherwise, is not addressed: the figure is
fixed at open and this decision keeps it fixed. And the simultaneous-assignment
limit reads short puts, so a trial holding shares contributes nothing to it,
which is why that cap and the total cap coincide today [`CONFIG_REFERENCE.md`,
`Risk:SimultaneousAssignmentLimitFraction`] and will diverge at the first covered
call.

Test FX-CoveredCallCommitsNothingFurther: a trial holding assigned shares gates a
call candidate against the committed capital it already carries, and the per-name
headroom is unchanged by the call.

---

### D-W44 An ordinary dividend pays cash; a non-ordinary one adjusts the contract
`active` · 2026-08-02

A dividend is two events in this domain and the record carries both. An ordinary
cash dividend pays the holder of the shares and leaves the overlying contracts
untouched. A non-ordinary one adjusts them, by calling for delivery of the
dividend, so the deliverable changes and the strike does not [D-W17].

Source. A cash dividend or distribution is ordinary regardless of size when it
"was declared pursuant to a policy or practice of paying such dividends or
distributions on a quarterly or other regular basis", and as a general rule one
"less than $12.50 per contract would not trigger the adjustment provisions of
Article VI, Section 11A". Release No. 34-54748, File No. SR-OCC-2006-01,
71 FR 67415, 21 November 2006, approved at Release No. 34-55258, 72 FR 7701,
16 February 2007. Retrieved 2026-08-02.

The rule this replaces is in the same filing and is not the rule. Its Background
records the 10% Rule, under which a dividend was ordinary if it did not exceed
ten per cent of the underlying's value on the declaration date, and the filing
exists to revise it. Size no longer decides; regularity does.

Consequence, and it is why this is not a detail of [D-W41]. A trial holding
shares through an ordinary dividend receives cash and its short call is
unchanged. Through a non-ordinary one it receives the dividend through the
deliverable of a contract whose terms have moved, which is a corporate action in
the sense `corporate_actions` already carries and not a ledger entry alone. The
corpus had one word for both and the two have opposite consequences for the leg
the trial has written.

What this does not decide. Which side of the line a particular dividend falls on
is OCC's determination per event and is transcribed, never derived [D-W36]. The
$12.50 threshold is stated as a general rule by the filing and this decision
repeats it as such rather than as a bound to compute with.

Test FX-OrdinaryDividendLeavesContractUnchanged: an ordinary dividend produces a
ledger entry and no contract adjustment, and a non-ordinary one produces the
adjustment its corporate action states.

---

### D-W45 Tax is outside the lab, and saying so is the point
`active` · 2026-08-02

No return this lab computes is after tax, and no decision path reads a tax rate,
a holding period or a wash-sale rule.

Chosen, with nothing to cite: what a laboratory chooses to measure is not
governed by anything external.

Why it is written down rather than left obvious. This is a decision-quality
laboratory, and the wheel's tax treatment differs from buy-and-hold's in ways
that would move the comparison [D-W13]: premium is short-term whatever the
holding period, assignment resets a basis, and a called-away position realises a
gain that a held one does not. An unstated exclusion is indistinguishable from an
omission, which is the subject of the pass that raised this, and the corpus had
no sentence anywhere naming tax in either direction.

Consequence for reading a result. Every comparison this lab reports is pre-tax on
both sides, which is a fair comparison of decisions and not a claim about what an
investor keeps.

---

### D-W46 The session calendar is transcribed, never derived
`active` · 2026-08-02

A session is a date the market traded, and the lab holds that calendar as a
transcribed record. It answers one question, which is what the next session after
a given date is, and it redefines no day count that already exists.

Why not derived from bars, and the weaker objection is the obvious one.
`underlying_bars.session_date` is per symbol, so a union over symbols cannot tell
a market holiday from a name that did not trade, which is what [D-W40] named when
it raised this. The stronger objection is point-in-time. A derived calendar's
answer to whether some past date was a session changes the moment another symbol
is ingested, so a question about the past would get a different answer after new
data arrived, which is the leak an as-of read exists to close [D-W8] arriving
through a derived value rather than through a stored one.

A calendar is a fact about the market rather than about the data this lab happens
to hold, which is [D-W36]'s shape: a stated term is transcribed and never
computed.

**It is a stored snapshot, so it is never rewritten and a correction appends a row
carrying its own observation stamp** [D-W8]. That is this decision's own argument
turned on itself: what makes a derived calendar unusable is that a past date's
answer could move, and a transcribed one that could be edited would move it the
same way. Stated here rather than left to follow, because the append-only list
records the decision behind each entry and a classification carried only in prose
is not one.

What it does not do, and this clause is what keeps it from spreading. Days to
expiry and a trial's day bound are calendar days and stay calendar days.
`WORKED_EXAMPLE.md` counts 46 days from 2026-03-02 to 2026-04-17 and 109 to
2026-06-19, both on the calendar, and `Gate:MaxDte` and `Trial:MaxTrialDays` are
compared against counts of that kind [D-W24]. A session calendar that silently
redefined either would move every gate verdict in that document and put its total
out of reach.

A shortened session is a session. The lab observes a closing price and cannot use
the hours a session kept, so the calendar carries dates and no hours.

One market, so no market column. Every name this lab holds trades against one US
holiday schedule. A second market would need the column, which is a change to a
built structure rather than one to add now against an eventuality.

A date the calendar does not reach stops the evaluation rather than resolving
either way [D-W37]. Guessing forward would put an unrecorded market assumption
inside a settlement date, and guessing that the date is a session would settle
proceeds on a day the market was shut.

Consequence. This is the first thing [D-W40] needs and its first schema
consequence. Phase 8's vendor ingest is the second producer of the calendar;
before it, a scenario states its own sessions, which is exact rather than
approximate, because a hand-written scenario's session dates are what its author
wrote and there is no name that did not trade.

Test FX-NextSessionSkipsAClosedDate: an assignment whose following date is absent
from the calendar settles on the next date the calendar carries, and a date the
calendar does not reach stops rather than resolving.

---

### D-W47 The state machine's events lie on three axes, not in one list
`active` · 2026-08-02

An event is what changes a trial's state. [`SYSTEM_DESIGN.md` §3.8] named six and
they cover three different kinds of thing, so separating them is what a longer
list would not have fixed.

**Contract events** arise from a contract the trial holds: expiry and assignment.
**Corporate actions** arise from the underlying on an ex-date and reach the trial
through `corporate_actions`. **Earnings is neither.**

Earnings drives no transition. It refuses a candidate whose life spans a buffered
report date [D-W25], which is a gate input, built at 2.3 and asserted there. An
event union carrying it would let a report date change a position, which nothing
in this lab does.

Exercise is assignment seen from the other side. This lab is never long an
option: the four states are cash, short put, holding shares and short call, a
roll buys a short back rather than opening a long, and the wheel writes options
rather than buying them [D-W16]. So the lab is never the party that exercises,
and naming exercise as an event of its own describes a transition no state can
make.

**The corporate-action vocabulary is complete before the transitions are, which
is the whole of what this settles.** OCC's adjustment provisions reach the
"declaration of dividends or distributions, stock splits, rights offerings,
reorganizations, or the merger or liquidation of an issuer", and
`CorporateActionKind` held `Split` alone. The enumeration carries all of them,
plus the ordinary and non-ordinary dividend that [D-W44] separates, and the
spin-off that a distribution can be. A reverse split is a split whose ratio is
less than one and is not a separate kind: the ratio is a recorded fact about the
event [D-W36], and a second name for one event is a second place to get it wrong.

**An action the lab does not model stops the trial rather than passing through
it.** What each action does to a contract is deferred, which the obligation
raising this permitted. What an unmodelled action does is not deferred, because
that is the silence it forbade. After a merger the trial's contract no longer
overlies what the trial opened against, so carrying on would price a position on
terms the lab cannot compute and dropping the event would leave a return that
reads as ordinary. The trial stops and carries the action as its reason. That is
[`CLAUDE.md` §6]'s rule that doubt about an identity excludes the security,
applied to an event that ends an identity rather than to one that questions it.

Consequence. `corporate_actions.kind` gains a CHECK, on the reason `right` and
`watchlist_membership.kind` have one: a stored form the database does not enforce
has one guard, and a vocabulary going from one value to several is when that
starts to cost. The synthetic scenario format gains corporate actions, without
which no hand-written scenario can express a dividend at all, which [D-W41] named
and could not add.

Test FX-UnmodelledActionStopsTheTrial: a merger on a held underlying stops the
trial with the action recorded as its reason, and a split does not.

---

### D-W48 The ledger records events, not only cash
`active` · 2026-08-02

`ledger_entries` records every event that moves a trial, whether or not it moves
cash. An expiry that pays nothing is an entry carrying a zero amount, because the
projection rebuilt from this table has to know the short closed and no other
table says so.

The corpus already reads this way. `WORKED_EXAMPLE.md` §6.3 carries a leg dated
2026-05-15 reading "call expires worthless" against a cash column of `0.00`, in
the same table as the five legs that move money.

The vocabulary: `premium_received`, `premium_paid`, `bought_to_close`,
`expired_worthless`, `assignment`, `call_away`, `shares_sold`, `dividend`,
`commission`, `assignment_fee` and `stopped`.

Four of those pairs exist because the same cash direction hides two different
events. A short leaves by expiring worthless, by being assigned, or by being
bought back, and only the last is a premium. Shares leave by being called away at
the strike or sold at market when the roll bound binds [D-W14], and the two
prices are not the same fact. The two premium kinds are named rather than carried
as a signed amount under one kind, because a roll is a debit and a credit on one
day and the rebuild has to read which leg opened a position rather than infer it
from a sign.

A short is bought back either to roll into a new leg or to end the trial, and
only the first is followed by a `premium_received` on the same day. The rebuild
cannot infer the difference from the sequence, because a trial closed at its last
permitted roll and a trial closed by choice look identical after the fact. So the
paying leg of a roll is `premium_paid` and a buy-back that ends a trial is
`bought_to_close`: a roll pays a premium and opens a position, a close pays a
premium and ends one, and this decision's own rule is that a kind missing from
here is a fact the rebuild cannot recover.

`commission` and `assignment_fee` are in the vocabulary although nothing writes
them yet. Whether the fill model records them as entries of their own or nets
them into the premium is 3.4's, and [D-W12] requires them explicit without saying
where. A vocabulary admitting a value nothing writes costs nothing, and a
migration adding one costs a table rebuild, which is the argument [D-W47]'s
enumeration rests on.

`kind` carries a CHECK, for the reason every other stored vocabulary in this
schema has one.

Consequence for the projections. This is the vocabulary [D-W35]'s rebuild test
exercises, and that test is what makes `trials` and `positions` projections
rather than rewritable tables with a flattering name. A kind missing from here is
a fact the rebuild cannot recover, and nothing else would find it. `trials`'
`close_kind` is the column that shows it: four of its five values read straight
off a kind here, and the fifth is what `bought_to_close` exists for.

Test FX-ProjectionRebuildsFromLedger: covered, registered at 3.3.

---

### D-W49 A trial that stops is valued, and a forced close pays the ask
`active` · 2026-08-03

A trial stopped by an unmodelled action [D-W47] is valued at the session's close:
shares at the close, a short at its quoted price, and the cash recorded so the
trial's entries sum to what the account actually held. A trial closed at a bound
[D-W14] buys its short back at the ask, not at intrinsic value.

Why stopping must value rather than zero. D-W47 says the trial stops and carries
the action as its reason; it does not say the position is liquidated at nothing.
Zeroing it makes every name that has a corporate action a total loss, which is a
bias with a sign, in a lab whose whole criterion is comparing decision quality
across makers. A maker that happened to hold the name with the merger would be
scored worse for an event no maker chose. The value is a model rather than a
measurement, and it is recorded as one: the position is marked at the close, and
the trial is scored on that mark.

Why a forced close pays the ask. An option costs at least its intrinsic value to
buy back and normally more, so pricing a forced close at intrinsic closes below
the bid and manufactures an edge from the accounting, which is what [D-W12] fixes
fills at the bid to prevent. It flatters precisely the trials the bound exists to
terminate, which are the losing ones, so the error has a sign and points the same
way as the first.

The asymmetry is deliberate and is [D-W12]'s. A sale fills at the bid and a
purchase pays the ask, because both are the side of the spread the account does
not choose. `SessionFacts` already carries the quote.

Test FX-StoppedTrialIsValuedAtTheClose: a trial holding shares that meets an
unmodelled action reports entries summing to the marked value, not to the outlay.
Test FX-BoundClosePaysTheAsk: a forced close debits the ask, and a case where
intrinsic and ask differ shows which was used.

---

### D-W50 What a fill costs, and where each cost is recorded
`active` · 2026-08-03

A fill's cash for one contract is the price times the multiplier [D-W17], taken
at the bid for a sale and the ask for a purchase [D-W12, D-W49]. **The commission
is its own ledger entry beside the premium, per contract and per leg, and the
assignment fee is zero.**

The commission is separate because [D-W12]'s word is explicit and a netted cost is
not. `ledger_entries.kind` has carried `commission` since [D-W48] for this, and
the two questions a ledger should answer without arithmetic are what a trial paid
in commission and what it received in premium. Netting makes each answerable only
by recomputing it from the other.

Consequence for the projection, and it is not a rounding difference. A rebuild
folds `commission` entries into the premium banked, because net basis is what the
account paid per share and the account paid the commission. A projection ignoring
the entry would be wrong rather than coarse: `WORKED_EXAMPLE.md` §6.3 states a net
basis of 49.0565, which is 50.00 less the credit after commission, and ignoring it
gives 49.05.

The third arrangement is rejected and named, because it is the one a later reader
reaches for. Writing the gross premium, the commission, and a net figure on the
premium entry states one fact twice, which this corpus has removed counts,
ordinals and markers for. A query wanting the net sums two rows.

Source for the fee. Charles Schwab's Pricing Guide for Individual Investors,
April 2026: "There are no commissions or per-contract fees assessed on
transactions resulting from options exercises and assignments." The same page
gives an online option commission of "$0 base commission, plus $0.65
per-contract fee", which is the figure `Costs:CommissionPerContract` has carried
from §1 of the worked example since 0.8 with no external source. Retrieved
2026-08-03.

What that source does not reach. It is one broker's published schedule and not a
market rule, so it establishes that zero is the common case rather than that it is
universal. Another broker may charge, and the lab models the common case and
records that it is a model, which is the disclosure [D-W38] makes about the
one-cent threshold. **A fee of zero still earns a configuration key**, because the
key is what makes a different broker a change to a stored value rather than to
code, and because a zero inferred from an absent ledger line is invisible when
wrong where a stated one is not.

`Costs:FillPoint` is readable and is not a tunable [D-W12]. The fill point is
fixed in advance, and the key exists so a fixed value can still be resolved as of
a simulated date rather than assumed by whatever reads it.

Test FX-TrialCompleteIncludesAssignment: the assigned trial totals 498.05, each of
§6.3's cash cells equals the sum of that date's ledger entries, and its net basis
reads 49.0565. The cell correspondence is a reconciliation and not a row for row
match, because the document nets what this decision separates and the ledger
therefore carries more rows than the table.

---

### D-W51 A run's randomness comes from a seeded generator, never the store
`active` · 2026-08-04

No SQL this lab issues calls `random()`, `randomblob()` or any function whose
value varies between two runs over the same data. Randomness the lab needs is
produced in code from a seeded generator, so a run reproduces.

The rule is narrower than barring randomness, which the lab requires: one of the
three makers is a random-within-band control [D-W4], and its seed is a config row
whose value is arbitrary while its fixity is not [`Policy:Random:Seed`]. What is
barred is randomness whose source is the store, because a seeded run must
reproduce and `random()` cannot be seeded.

**Three classes are not covered, and naming them is half the rule.**

*Row order without `ORDER BY`.* A `SELECT` has no guaranteed order, so this is
nondeterminism in SQL that is not a function at all. A scanner cannot tell a
scalar read from a sequence read without understanding the query, and most reads
here are single-row. What holds the property instead is the byte-identical run
itself, which reads through the real paths, and any read whose result is kept as
a sequence orders explicitly. This is [D-W28]'s argument one level up: row order
is a fact about the storage engine until a caller keeps the rows as a sequence.

*Connection-state functions.* `last_insert_rowid`, `changes` and `total_changes`
are deterministic given an identical insertion history, so barring them would
fail a run that already reproduces.

*Version functions.* `sqlite_version` and `sqlite_source_id` vary by binary
rather than by run, which is build determinism and a different property.

Test FX-NoNondeterministicSql: no SQL under `src/` calls a barred function, and
every name on the list is asserted to exist in the bundled binary.

---

### D-W52 The feasible set is keyed on what the generator reads
`active` · 2026-08-04, amended 2026-08-09 (the split governs the evaluation too)

A feasible set is stored once per symbol, session date and option right, and is
referenced by every decision made against it. The six contract-level gate verdicts
are stored with it. The four portfolio-level verdicts are stored per decision,
because they are computed from a book no two makers share once they diverge.

**The key comes from the generator's inputs and not from the obligation's
wording.** `EnumerateFor` takes a symbol, a simulated date and a position state,
and `GateFor` takes those plus a book. Position state reaches enumeration through
one function, which maps cash to puts, holding shares to calls, and both short
states to nothing at all. So the enumerated set varies with the right and not with
the state, and

> at most two non-empty enumerations exist per symbol and session, however many
> makers there are.

That mapping was measured at 4.1, when both short states enumerated nothing. From
4.4 each short state enumerates the right it is short [D-W54], which leaves this
key unchanged rather than widened: enumeration filters on the right alone, so two
states sharing a right share a set, and `ContractConstraints.Evaluate` takes
neither a state nor a book, so they share its verdicts too. Four states still map
to two rights and the bound above still holds.

The obligation this closes was raised at v1.17.0 and asked for one set per name and
date. That wording describes a generator taking neither parameter, which is the
generator that existed when it was written.

**The split between shared and per-maker follows a boundary the code already
draws.** `ContractConstraints` raises the spread cap, the premium floor, the
crossed market, the delta ceiling, the expiry window and earnings clearance, from
the candidate, the bounds and the report dates. `PortfolioConstraints` raises the
per-name cap, the total cap, assignment stress and gross basis, every one against
a book. A verdict computed from a book belongs to the maker whose book it is.

**The split governs the evaluation and not only the storage, and they are one
boundary rather than two.** The contract-level verdicts are computed once per
symbol, session and right and every maker acting against that key is handed the
same evaluation; the portfolio-level verdicts are computed per maker, against that
maker's own book. Storing them apart while computing them together would make the
shared half a thing three evaluations agree about rather than a thing there is one
of, which is what [D-W4] asks for and what a comparison cannot deliver: the
refusal that guards a shared set compares contract identities and not verdicts, so
three separate evaluations pass it while the property fails. **A property enforced
by a comparison that cannot see the difference is not enforced.**

This was stated for storage alone until 4.5, where the composition root needed the
other half and found the surface contradicting it: one entry point took a book
alongside the key, so a single call per key had to pick one maker's book and there
is none to pick.

**What this does for storage, measured rather than argued.** The obligation framed
the saving as division by three, the number of makers. The shared part is bounded
at two rows per symbol and session by the count of sellable rights, which is a
constant rather than a divisor and does not grow when a maker is added. The
per-maker verdicts do not share at all, and they are the small part: they are
written per candidate only where a cap binds.

**What [D-W4] requires, and it is not one row.** That decision says the three
makers act on the same feasible set, and its rationale is that a control running a
different schedule makes an improving curve indistinguishable from an easier
environment. It is a requirement about confounds. Makers whose positions have
diverged face different opportunities because of their own prior choices, which is
the experiment rather than a defect in it. This key delivers [D-W4] by
construction: makers whose states coincide share a row, and makers whose states
differ are outside what that decision reaches.

**A gate reason is a row, not a delimited list.** The reasons for one candidate are
a set in declared order, and a delimited list makes a single reason unqueryable.
They are stored one per row against the candidate they refuse, which leaves the
candidate at one row per contract rather than changing its grain, and that is the
third option the obligation did not name.

**No ordinal column beside the reason.** The declared order is the domain type's
own, so a stored position would be a second statement of a fact the vocabulary
already carries. That is the arrangement [D-W50] rejected on the premium entry.

**`gate_status` is not stored, because it is derivable.** A candidate is feasible
exactly when no reason refused it, so a status column beside the reasons could
disagree with them, and a schema admitting a rejected candidate with no reason
admits a state [D-W22] forbids. Rejected candidates are still recorded, which is
what [D-W10] asks; what goes is the second statement of whether they passed.

**Whether a candidate's features are shareable is not settled here.** Nothing
computes a feature yet, so what one contains is unknown, and a feature that is
portfolio-relative belongs on the per-maker side rather than the shared one. The
checkpoint that computes them answers it rather than inheriting the assumption.

Test FX-DecisionsShareOneFeasibleSet: two decisions made against the same symbol,
session and right reference one stored set rather than two copies, and their
portfolio verdicts differ where their books do.

---

### D-W53 A trial's bounds are fixed at its open
`active` · 2026-08-04, amended 2026-08-09 (where the machine is constructed)

`Trial:MaxRolls` and `Trial:MaxTrialDays` are resolved once, as of the session a
trial opens, and hold for that trial's life. A later version of either binds the
trials opened after it and no trial already running.

Rationale. Configuration resolves as of the simulated date [D-W26], and a trial
spans many sessions, so the rule leaves open which of those dates is meant.
Resolving per session would let a bound move under a position already taken: a
trial opened when three rolls were permitted could find itself over its cap
without having rolled again, and the roll that breached it would be a roll that
was permitted when it happened. A bound is a constraint on a decision, and a
decision is made once.

This ratifies a shape already built rather than requiring a change. 3.3 gave the
state machine its bounds at construction, on the resolve-once-per-evaluation shape
`GateBounds` uses, and settled nothing about which date that construction reads.
The machine needs no change; what needed stating is which date the component
constructing it resolves as of.

**What this does not answer, stated so it is not answered twice.** It fixes the
date within a run, not the configuration a run reads. Whether a run over history
resolves configuration as of each simulated date or as of the instant its
pre-registration was committed [D-W15] is a separate question, owed at Phase 9,
and reached there by a seed stamped from the wall clock rather than by anything
here. This decision holds under either answer: if Phase 9 pins a run's
configuration, every trial in that run opens under the pinned values and the rule
below is satisfied trivially; if it does not, the rule is what keeps a mid-run
revision off the trials already open.

Consequence for the run, added at 4.5. The state machine carries its bounds from
construction, so the component that constructs it decides which trial's bounds it
holds. It is constructed where a trial opens and not where a run starts. A run
holding one machine across the trials it drives would apply the bounds of whichever
trial happened to open first to every trial after it, which is this decision's own
defect one level up, and the rebuild's test would not see it: that test asks
whether a projection agrees with a run, and both halves would be wrong together.

Consequence for the rebuild. A projection asking whether a bound had been reached
must read the values that trial opened under, not current ones and not the ones in
force on the session being rebuilt. A rebuild reading anything else disagrees with
the run it is rebuilding and presents the disagreement as a ledger defect, which
is the wrong place to look.

Test FX-TrialBoundsFixedAtOpen: a trial spanning a configuration change is bound
by the values in force when it opened, and a trial opened after the change is
bound by the new ones.

---

### D-W54 When a maker rolls, and when it closes
`active` · 2026-08-06, amended 2026-08-06 (moneyness, and the debit condition)

A maker with an open short at or inside seven days to expiry acts on it only if
the position is in the money by the exercise-by-exception threshold [D-W38]. A
position that would expire worthless is left to expire, because the wheel's
ordinary outcome is a short expiring and a maker that bought back every position
would never be assigned and never hold shares.

Acting, the maker closes the trial if a bound has been reached [D-W14], and
otherwise rolls to the candidate its own policy selects from that session's
feasible set [D-W52], by the same highest-credit-in-band rule it uses to open. It
closes rather than rolls if its band admits nothing, or if the roll would pay a
net debit: a roll exists to defer assignment while collecting premium, and one
that costs more than it collects has stopped doing that, so the position is closed
and the trial ends.

Acting requires a feasible set for that session [D-W52]. A session with no chain
has none, so a maker cannot roll and does not close: the position is left as it
stands and runs to expiry if nothing intervenes. That is why `WORKED_EXAMPLE`
§6.3 reproduces under this rule despite its short being deep in the money at the
threshold. Its chain is a single snapshot at 2026-03-02, so no session between
opening and expiry offers anything to roll into, and the trial reaches the
assignment the document records.

This is a property of the rule and not of that fixture. A forward run with a chain
every session would act at the threshold on the same position, so §6.3
demonstrates the rule's conditionality rather than its trigger, and a reader
taking it as evidence the trigger fires would have it backwards.

**Chosen, not transcribed.** No authority states any of this. The glossary's
sentence about rolling is standard terminology and that file says so; the corpus
states what a roll costs [D-W48], what bounds it [D-W14] and what it commits
[D-W43], and nothing states what triggers one. D-W43 is the precedent for a
decision with no external source, and this is the second.

Why a threshold rather than every session. A maker that reconsidered daily would
roll on the first session a higher credit appeared, making the trial a sequence of
one-day positions and the roll bound meaningless. Seven days is chosen inside
`Gate:MinDte`'s own floor: the gate will not open a position closer than seven
days out, so a position inside that window has passed beyond what this lab would
newly enter.

Why one algorithm. The three makers differ in their bands and not their rules
[D-W4, §4], and a roll that selected differently from an open would make the
learner's channel unable to change how it rolls, since the channel writes rows and
not code [D-W6].

What this does not settle. Whether a maker should close a profitable position
early rather than roll it is a strategy question this lab does not answer, and it
is deliberately outside the rule: the makers differ in selection, and adding an
early-close condition would make them differ in kind.

The threshold alone would break the wheel. A maker acting on every position at
seven days would never hold one to expiry, so it would never be assigned, never
write a covered call, and never reach the states this lab exists to measure.
`WORKED_EXAMPLE` §6.3's trial is the case: its last recorded close before expiry
is 45.80 against a 50.00 strike, and a rule without the moneyness condition buys
that position back rather than taking the assignment the document records.

Test FX-RollAtTheThreshold: a maker acts at seven days and not at eight, rolls
when a bound has not been reached, and closes when one has.

---

### D-W55 A run holds a sequence of trials, one open at a time
`active` · 2026-08-09, amended 2026-08-09 (what counts as opening one)

A maker driving a run opens a trial, carries it to cash, and opens another. A run
is therefore a sequence of trials per maker and per symbol, and a second open
while one is open is refused by name.

Rationale. A trial runs from first open through to return to cash [D-W14], and
the wheel is that cycle repeated. A run that held one trial would stop at the
first close and measure a single cycle, and the improvement curve this lab exists
to compute is over many. The refusal that made a run one trial was correct for a
loop applying a supplied sequence, where a second open is a sequence written
wrongly; it is wrong for a loop asking a maker, where a second open is what a
maker in cash decides.

**The refusal moves rather than goes**, and this is the operative half. One trial
is open per maker per symbol at a time, so an open arriving while one is open is
still a run described wrongly and still stops the walk naming the session and the
state [D-W48's argument, one level up]. What changes is which condition is the
error: it was any second open and it is now a second concurrent one.

**What counts as opening a trial, since the refusal keys on trials and never on
decision kinds.** A maker holding shares writes a covered call, which is an
opening decision by its kind and a leg inside a trial already open. Refusing it as
a second open would stop the wheel at its own middle. A trial opens when a maker
in cash sells a put [D-W16] and at no other time, so what the refusal asks is
whether this maker already holds an open trial in this name, not what kind of
decision arrived.

That distinction is why a maker is told about the short it holds rather than about
the trial it holds [`OpenShort`]. A maker holding shares holds a trial and has no
short, and a maker in cash has neither, and both open; passing the trial would
have made the parameter's absence mean two things and its presence mean two more.

Consequence for what a run returns. A run's result is a sequence of trials, each
with its own entries, and not a state beside one list. The ledger is written per
trial [D-W35], so a flat list would have to be partitioned by inferring where one
trial ended, which is reconstructing what the loop already knew and is the shape
that decision exists to prevent.

Test FX-MakersDriveTheRun: three makers driving one chain produce three trials,
three ledgers and one decision record, with no contract supplied by the test.

---

### D-W56 A trial is opened before the decision that opened it is recorded
`active` · 2026-08-09

The composition root mints the trial identifier first and records the decision
naming it. An opening decision carries the trial it opened.

Rationale. `decisions` is append-only and its trigger refuses an update [D-W3], so
a null written at the moment of the open is permanent in the strong sense: there
is no later write that could fill it. A record unable to answer which trial a
decision opened is a record missing the link between the two things this lab
measures, and D-W3 names that loss as the one unrecoverable one in the design.
The ordering is the whole fix and it costs nothing: the strike and the session an
open needs are both in the decision before it is recorded.

**A decision that names no trial is not a decision missing one.** A maker taking
nothing has no trial to name, and the column stays nullable for that case rather
than for the open's. The two are told apart by the decision's own kind, which the
record already carries.

Test: covered by FX-MakersDriveTheRun, whose decision record is read back and
whose opening decisions name their trials.
