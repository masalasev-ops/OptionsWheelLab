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

**Purpose and measurement**: D-W2, D-W3, D-W5, D-W17, D-W18, D-W20, D-W21
**Isolation and controls**: D-W1, D-W4, D-W6, D-W13
**Data and identity**: D-W7, D-W8, D-W9, D-W15, D-W26, D-W27, D-W28, D-W29, D-W30, D-W31
**Risk**: D-W10, D-W11, D-W14, D-W19, D-W23, D-W25
**Gate constraints**: D-W10, D-W22, D-W23, D-W24, D-W25
**Scope**: D-W12, D-W16

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

Test FX-RecordCarriesFeasibleSet: a recorded decision can be re-scored from the
record alone, with no access to live state.

---

### D-W4 Three decision-makers run in parallel on identical data
`active` · 2026-07-26

A frozen baseline, a random-within-band control, and the learner act every day on
the same feasible set with the same fill rules, keeping separate ledgers.

Rationale: without a frozen arm running the same schedule, an improving curve
cannot be distinguished from an environment that became easier.

Test FX-ThreeMakersSameFeasibleSet: on a given day all three makers are offered
byte-identical candidate sets.

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

Test: CI greps for `DELETE FROM` and `UPDATE` against snapshot tables and fails
the build on a match.

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
`active` · 2026-07-26

The outcome of a decision is return on capital committed at the strike, being
strike times the contract multiplier times contracts, measured from trial open
through to return to cash, with assigned shares marked into the same number.

Consequence: assignment is inside the metric rather than outside it, so the
strategy's downside cannot leave the measurement.

One hundred is the standard multiplier and not a constant of the metric. An
adjusted contract carries its own deliverable in `contracts.multiplier`, so a
metric hardcoding one hundred would misprice every position in a name that has
split.

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
`active` · 2026-07-26

After assignment, cost basis is recorded both gross, with premium tracked
separately, and net, with premium reducing basis. The "above basis" constraint on
covered call strikes is evaluated against gross basis. Reports state which
convention they display.

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
`active` · 2026-07-27

The gate rejects a candidate whose quoted spread exceeds
`Gate:MaxSpreadFractionOfMid` of the mid, or whose bid falls below
`Gate:MinPremium`. Proposed defaults are 0.12, being twelve percent of mid, and
0.30, both Phase 0.8 config.

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

**Open, and to be settled at Phase 0.8.** The proposed 0.35 exactly equals the
upper bound of the random control's band. If both are held at 0.35, the random
control becomes uniform over the entire feasible delta range rather than uniform
within a band inside it. That may be the better control, but it should be chosen
rather than inherited from a coincidence of defaults.

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
`active` · 2026-07-27, amended 2026-07-27 (enforcement point)

The gate rejects a candidate whose days to expiry fall outside
`Gate:MinDte` to `Gate:MaxDte`. Proposed defaults 7 and 70, Phase 0.8 config.

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
`active` · 2026-07-27

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
