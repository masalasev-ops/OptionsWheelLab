# SYSTEM_DESIGN

Version 1.0.0. Supersedes v0.1 (2026-07-12), which was written before the lab
was reframed as a decision laboratory and is not recoverable.

This is the narrative design, read start to finish. The numbered register lives
in `DECISIONS.md` and is not duplicated here. Prose states each rule; the bracket
is a pointer.

Build state: stated per section, not for the document. A section carries
a marker once part of what it describes is built, and the sections
without one describe designs nothing has yet implemented. This document
said "not built" as a whole and promised per-section markers as phases
landed; none was ever added, so the claim went stale at 1.1 and stayed
stale for five checkpoints.

---

## 1. The claim

OptionsWheelLab studies whether a decision-maker improves at deciding. Its success
criterion is that decisions made later are measurably better than decisions the
same maker produced at the start, judged against a control that does not learn
[D-W2].

The wheel is the task environment, not the subject. Whether the wheel is
profitable is a question the lab can answer and was not built to answer.

This decoupling is deliberate. A lab whose success depends on finding a
profitable strategy has bound its own fate to market efficiency, and can run for
years producing nothing falsifiable. A lab that measures improvement produces a
verdict either way.

## 2. Why the wheel is the right environment

Three properties, in descending order of importance.

**The counterfactual is dense and recoverable.** Every contract not chosen was
priced and its outcome is computable after the fact, so each decision carries a
rank and a regret inside the opportunity set that genuinely existed that day
[D-W5]. Nothing else in the lab's reach offers this.

**Feedback closes in weeks.** A trial resolves at expiry or shortly after, so
the improvement signal accumulates on a horizon of months rather than the
multi-year cohort horizon that binds an equities research lab.

**Decisions are discrete and frequent.** Sell this contract, or that one, or
nothing. Twenty watchlist names on a monthly cadence produce on the order of
two hundred and forty trial outcomes a year.

Against those, one caveat that must never be dropped. Every contract on one name
on one day resolves against a single underlying path, so the effective sample is
far smaller than the raw count of scored candidates. Statistics computed as if
each candidate were an independent observation will be wildly overconfident.
`VALIDITY.md` §4 states how this is handled.

## 3. Components

### 3.1 Watchlist

Build state: membership as state is **built at 1.3**, resolved as of both
axes. The universe itself is the operator's curation and has no build content.

The universe is restricted to names the lab would accept holding, because
assignment is a designed leg rather than an exit to be avoided [D-W16].
General premium selling on whatever is richest is out of scope.

Membership is state, not a filter. The store records when a name entered and
left the watchlist, and any query about a past date resolves membership as of
that date [D-W9]. Applying today's watchlist retrospectively would select names
that survived and did well, which removes precisely the cases the risk machinery
exists to catch, and would make any historical run incapable of failing.

### 3.2 Chain store

Build state: **built across 1.1 to 1.5**, on synthetic chains: the schema,
point-in-time reads, chain ingest, and corporate actions as stated successors
with resolvable lineage. The earnings calendar has its table and no writer yet.

A daily end-of-day snapshot of the option chains for watchlist names, plus
underlying bars, dividends, and the earnings calendar. Snapshots are append-only
and stamped with what was observable that day; a stored snapshot is never
rewritten [D-W8]. A delete or an update against a snapshot table fails the build,
the same guard AlphaLab uses for bar history.

### 3.3 Candidate generator

Two stages in one component. First it enumerates every contract that could be
sold today given the current position state, with the features that describe it.
Then the risk gate removes the ones that would breach a cap, and the survivors
are the feasible set [D-W10].

The gate lives inside the generator rather than downstream of the choice so that
all three decision-makers receive an identical feasible set, and any difference
between them is selection rather than permission.

The gate needs current portfolio state to evaluate concentration and committed
capital, so the ledger feeds back into it. This is the only backward edge in the
daily path.

### 3.4 Risk gate

Two families of constraint, and they answer different questions.

**Portfolio constraints** ask whether the book can carry this position. Per-name
committed capital as a fraction of account equity, total committed capital as a
fraction of account equity, and a simultaneous-assignment stress figure asking
what would be owed and what would be held if every open short put assigned at
once. The third is the one that matters, because the wheel's real cash-loss event
is not one bad position, it is every short put assigning together in a correlated
selloff while the account lacks the cash to fund it.

**Contract constraints** ask whether this contract belongs in the opportunity set
at all. A liquidity filter on spread and premium [D-W22], a delta ceiling
[D-W23], an expiry window [D-W24], and an earnings clearance [D-W25].

The liquidity filter carries a purpose the others do not. Because the scorer
prices every candidate in the feasible set and regret is measured against the
best of them, an untransactable quote corrupts the regret figure for every
decision that day rather than only for a decision that selects it. Liquidity is a
precondition of the counterfactual meaning anything.

A candidate may fail several constraints, and the gate records every failing
reason rather than the first, so the screens and the audit trail show the whole
picture.

Cap values are structural and outside what the learner may propose [D-W11].

### 3.5 Decision-makers

Three, running in parallel on identical data with identical fill rules and
separate ledgers [D-W4].

The **frozen baseline** implements a fixed policy that never changes for the life
of the experiment. It is the yardstick.

The **random-within-band** maker selects uniformly among candidates in the frozen
baseline's expiry window, over a delta band of its own that is wider than the
baseline's and spans what the gate admits. It separates selection skill from the
return to simply being short volatility, which it can only do if it is not itself
constrained by a band someone chose.

`Policy:Random:` therefore carries no DTE keys: the random maker reads the
baseline's window. A reader of `CONFIG_REFERENCE.md` alone would take their
absence for an omission.

The **learner** acts from its current policy rows and is the subject of the
experiment.

A policy is a configuration row rather than compiled code, so a variant is a new
row. Configuration rows are append-only and versioned, with current defined as
`MAX(version)`.

### 3.6 Decision record

The primary artefact of the system, and more important than the position ledger.
Every decision is journaled with the feasible set exactly as it stood, the
features of every candidate in it, the choice, and which maker made it [D-W3].

If the input context is not recorded, the decision cannot be re-scored later and
the improvement measurement becomes impossible. This is the one place where
losing data is unrecoverable rather than merely inconvenient.

### 3.7 Fill model

Sells at the bid, never the mid, with explicit per-contract commission and
assignment fees [D-W12]. End-of-day granularity means the lab never observes the
price it would actually have received, so the assumption must be conservative and
fixed in advance. A wheel result filled at the mid shows an edge manufactured
entirely by that one choice.

### 3.8 Wheel state machine and ledger

Four states modelled as a discriminated union: cash, short put, holding shares,
short call. Daily events drive transitions: expiry, assignment, exercise,
dividend, split, earnings.

Rolling is permitted but bounded, and a rolled chain terminates at a configured
maximum number of rolls or maximum trial days, whichever binds first, at which
point the position closes at market [D-W14]. Bounding it keeps trials finite,
which the scorer and the walk-forward learning boundary both require, and forces
an explicit rule about when to stop defending a losing position.

A roll is itself a recorded decision, scored against the counterfactual of
accepting assignment.

Cost basis after assignment is recorded both gross, with premium tracked
separately, and net, with premium reducing basis. The covered call "above basis"
constraint is evaluated against gross basis [D-W19].

### 3.9 Counterfactual scorer

After a trial closes, the scorer computes the outcome of every candidate that was
in the feasible set that day, not only the one chosen, and assigns the chosen one
a rank and a regret within that set.

It produces two figures. A **fast** mark-at-expiry score, comparable across every
candidate in a chain on a common horizon, and a **slow** trial-complete score.
The learner's objective is the slow score; the fast score serves cross-candidate
ranking within a day and is monitored for systematic divergence [D-W20].

The two can invert. `WORKED_EXAMPLE.md` contains a case where they rank three
candidates in exactly opposite order, which is the situation the divergence
monitor exists to surface.

### 3.10 Outcome metric

The outcome of a decision is return on capital committed at the strike, being
strike times one hundred times contracts, measured from trial open through to
return to cash, with assigned shares marked into the same number [D-W17].

It is not annualized. Duration in days is carried as a separate field and
annualization happens only at report time [D-W18]. Annualization inside the
objective creates a preference for short trades for arithmetic reasons rather
than real ones, and is the most common way wheel results get flattered.

Maximum adverse excursion over the trial window, computed from daily underlying
bars, is recorded alongside every outcome and feeds the risk drift check. It is
not part of the learner's objective [D-W21], because an objective that includes
it invites the learner to game it rather than respect it.

### 3.11 Feature grader

Grades properties of contracts rather than policies: delta, implied volatility
rank, spread width, days to expiry, distance to earnings, term structure slope.
It answers which properties predicted good outcomes, one layer below the policy.

Descriptive only. It never becomes an input to the scorer, the gate, or the
instrument.

### 3.12 Improvement instrument

Regret per decision over time, for the learner against the frozen baseline. If
the learner's curve falls faster than the baseline's by more than the measurement
error, decisions have improved [D-W2]. Specified in `VALIDITY.md` §2.

### 3.13 Risk drift check

Compares the learner against the frozen baseline on adverse excursion
distribution, assignment rate, and committed capital over time. A learner whose
regret falls while these widen has not improved, it has borrowed from the tail.
Specified in `VALIDITY.md` §3.

### 3.14 Controls

Beyond the two control makers, the lab runs buy-and-hold on the same underlyings
with the same capital over the same window, and a hold-cash floor [D-W13]. These
answer the secondary question of whether the wheel is worth running at all, and
they are reported separately from the improvement measurement so the two are
never conflated.

## 4. The firewall

No output of the learning channel reaches any component that judges the learner.
Not the scorer, not the feature grader, not the improvement instrument, not the
risk drift check, and not the risk caps [D-W6].

The case people trip over is indirect: feeding a prior learner decision back in
as a feature routes learner output straight into the thing that prices learner
output. Any feature derived from a previous decision by the learner is excluded.

## 5. The two clocks

The **fast loop** runs daily and is mechanical: ingest, mark positions, process
expiries and assignments, produce the feasible set, take three decisions, record.
It never adapts itself.

The **slow loop** runs per expiry cycle and is evaluative: score the closed
trials, update grades, recompute the regret curve and the drift check, and only
then permit the learning channel to emit new policy rows.

Keeping them separate in the code and in the operator's head is what prevents the
daily path from becoming self-modifying.

## 6. Use of stored history

Stored chains serve first as a machinery harness, proving the loop correct, the
scorer leak-free, and the metric well behaved before the forward clock starts.
A learn-and-validate split may also be run, but its held-out result is committed
as a pre-registered prediction before the forward run begins and is not counted
as evidence of improvement on its own [D-W15].

Two or three folds in one volatility regime cannot separate improvement from
fitting. The value of the split is that it becomes a prediction the forward run
either confirms or refutes, which is worth more than either use alone.

**The leakage trap specific to this design.** The scorer needs an outcome, and an
outcome needs an expiry. So at a learning boundary date T, the learner may only
learn from decisions whose trials closed before T, not from decisions that opened
before T. That puts a lag of roughly one expiry cycle on the boundary, longer
where rolls are involved, since a rolled chain does not resolve until the position
returns to cash. Getting this wrong lets the learner read the future, and the
regret curve will look excellent.

## 7. Phase map

Complete and stable. Checkpoint detail is written one phase ahead and lives in
`BUILD_PLAN.md`.

| Phase | Delivers | Needs data purchase |
|---|---|---|
| 0 | Foundations: repo, config, store, migrations, fixture harness, CI, deterministic clock | No |
| 1 | Chain store and point-in-time invariants, on fixtures | No |
| 2 | Candidate generator and risk gate, producing a feasible set | No |
| 3 | **Thin slice**: one full wheel turn end to end, cash back to cash | No |
| 4 | Decision record and three makers in parallel | No |
| 5 | Counterfactual scorer, outcome metric, adverse excursion | No |
| 6 | Feature grader, improvement instrument, risk drift check | No |
| 7 | Learning channel and walk-forward harness, on synthetic chains | No |
| 8 | **Data boundary**: EODHD options add-on, live ingest, historical backfill | **Yes** |
| 9 | Walk-forward on real history, pre-registration committed | Yes |
| 10 | Forward run begins | Yes |
| 11 | Blazor surface | Yes |

Two things about this ordering are deliberate.

The thin slice at Phase 3 is one complete wheel turn rather than a broad but
shallow layer, because the state machine is where correctness is hardest and
cheapest to establish early.

The purchase boundary is at Phase 8, and everything before it runs on synthetic
chains. Synthetic is better than real for the machinery, because assignment,
early exercise, and roll-cap cases can be constructed deliberately rather than
waited for. The subscription clock and the evidence clock are the same clock, so
paying before Phase 8 buys nothing.

## 8. Open parameters, closed at 0.8

Build state: **closed at 0.8**. The values are config rows in the store; the
four keys still unset are owed to the phases that first consume them.

The roll bounds [D-W14], the divergence threshold and window [D-W20] and the six
gate constraints [D-W22 to D-W25] were left unset in this document deliberately,
because setting a policy value in a design disguises a parameter choice as a
design decision. They are config rows now. `CONFIG_REFERENCE.md` states what is
in force and the reason for each, and `CHANGELOG.md` at v1.15.0 records them
landing. Provenance is three kinds: transcribed from a corpus statement, taken
from a proposed value in a decision, or judged.

Four rows-classed keys remain unset, and they are owed rather than open. The
three risk fractions are the operator's [D-W11] and the assignment fee has no
statement anywhere. Both are carried obligations against the phase that first
consumes them.
