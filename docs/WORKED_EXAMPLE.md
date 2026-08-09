# WORKED_EXAMPLE

Build state: **partly reproduced**, and this marker means something different
from every other one in the corpus. This is a specification expressed as
arithmetic, so what gets built is never this document but the machinery that
reproduces it, and the question the marker answers is how much of the arithmetic
something now computes. It read **not built** until v1.34.1, which was true of
the document and said nothing about that.

Reproduced: §2's chain and §5's bars, which load and persist to the cent; §3
whole, every strike enumerated and every verdict and reason reached; and §1's
caps, which are configuration now and whose 5,100.00 and 22,000.00 headrooms are
asserted. §1's policy bands are configuration too and nothing consumes
them yet, and its fill rule is consumed from 3.4. Of §6, 6.3's two bases are read
by the constraint that binds a call strike, and 3.3 added its shape: the state
machine reaches the call-away on 2026-06-19 from the assignment on 2026-04-17, so
the trial's 109 days are computed rather than stated, and its four positions
rebuild from the ledger. **3.4 added its arithmetic**: §4's fill table, all three
rows of it, and §6.3's 498.05 from a ledger the machine wrote. **3.5 changed what
produces it rather than what it is**: §6.3's trial is now walked by the run from
its three choices and its six sessions, where the figures were previously reached
by a test stepping the machine itself, so the document is reproduced end to end by
the thing the lab will run rather than by the thing that tests it.

**4.4 left §6.3 reproduced under a rule that could have changed it.** A maker acts
at seven days to expiry on a short in the money [D-W54], and §6.3's last recorded
close before its first expiry is 45.80 against a 50.00 strike. It reproduces
because acting requires a feasible set and this chain is a single snapshot at
2026-03-02, so no session between opening and expiry offers anything to roll into.
That is an argument rather than a measurement: nothing drives a maker, so no
fixture walks this trial through one.

Not built: §6.1 and §6.2, which are the totals of the two trials no maker has
opened, and §7 and §8's scores and regret. **§4's three decisions are reproduced
from 4.3**, which is the first checkpoint with makers and the first to read that
section at all: it has been this corpus's statement of what a decision-maker does
since v1.0.0 with nothing checking it. Nine of the twelve fixtures naming this
document are implemented and the other three belong to Phase 5, which is where
the scores arrive.

3.3 added none of its own: its fifteen rows are all `authored`, taking their
expectations from the decisions rather than from here, and what they borrow is the
shape of §5's sessions.

**§6.3's trial never observably holds bare shares, which 3.3 found by rebuilding
it.** The covered call is written on the session the assignment becomes known and
again on the session the first call expires, so `holding_shares` begins and ends
within one session both times. The shares are carried on the `short_call`
positions, which is what the account held at each close, and a lab that observes
closes [D-W12] cannot claim to have seen a state that began and ended between two
of them.

Nothing here is decorative. A figure this document states that the code
contradicts fails a fixture rather than going unnoticed, which is what made §3's
revision at 2.1 a code change and not a documentation one.

One decision traced from chain snapshot to regret score, with every number
computed. Prose specifications are ambiguous exactly where it matters; this
document removes the ambiguity. Anyone who can reproduce these figures has
understood the system, and any implementation that reproduces them is correct.

The data is synthetic. `WDGT` is not a real ticker, deliberately, so that nobody
mistakes this for a backtest.

---

## 1. Setup

Account equity: `100,000.00`
Per-name committed capital cap: 25% of equity, so `25,000.00`
Total committed capital cap: 60% of equity, so `60,000.00`

Existing state on 2026-03-02:

- `WDGT` already has `19,900.00` committed from earlier positions.
- Total committed across all names is `38,000.00`.

Therefore the per-name headroom for `WDGT` is `25,000.00 - 19,900.00 = 5,100.00`,
and the total headroom is `60,000.00 - 38,000.00 = 22,000.00`. The per-name cap
binds; the total cap does not.

Fill rule: sell at the bid, commission `0.65` per contract [D-W12].

Policy bands. Frozen baseline targets delta 0.20 to 0.30 and 30 to 60 days to
expiry, and prefers the highest credit inside that band. The random maker draws
uniformly among candidates with delta 0.10 to 0.35 in the same expiry window.

---

## 2. The chain snapshot

Snapshot date 2026-03-02. `WDGT` last close `52.40`.

Puts expiring 2026-04-17, which is 46 days out:

| Strike | Delta | Bid | Ask | Committed if 1 contract |
|---|---|---|---|---|
| 40.00 | -0.05 | 0.15 | 0.16 | 4,000.00 |
| 42.50 | -0.07 | 0.30 | 0.44 | 4,250.00 |
| 45.00 | -0.10 | 0.30 | 0.32 | 4,500.00 |
| 47.50 | -0.16 | 0.55 | 0.59 | 4,750.00 |
| 50.00 | -0.24 | 0.95 | 1.01 | 5,000.00 |
| 52.50 | -0.44 | 2.05 | 2.20 | 5,250.00 |
| 55.00 | -0.62 | 3.60 | 3.85 | 5,500.00 |

Committed capital is `strike x 100 x contracts` [D-W17].

---

## 3. Enumeration and the risk gate

All seven strikes are enumerated. The gate then evaluates each against every
constraint this snapshot gives it something to read: the spread cap of twelve
percent of mid and the premium floor of `0.30` [D-W22], the delta ceiling of
`0.35` [D-W23], and the `5,100.00` per-name headroom [D-W11]. Every failing
reason is recorded, not the first. The ceiling compares absolute delta
[D-W23], which is why this table carries magnitudes where §2 carries the sign
the chain stated.

| Strike | Spread, % of mid | Bid | Delta | Committed | Gate |
|---|---|---|---|---|---|
| 40.00 | 6.45 | 0.15 | 0.05 | 4,000.00 | rejected: premium floor |
| 42.50 | 37.84 | 0.30 | 0.07 | 4,250.00 | rejected: spread cap |
| 45.00 | 6.45 | 0.30 | 0.10 | 4,500.00 | feasible |
| 47.50 | 7.02 | 0.55 | 0.16 | 4,750.00 | feasible |
| 50.00 | 6.12 | 0.95 | 0.24 | 5,000.00 | feasible |
| 52.50 | 7.06 | 2.05 | 0.44 | 5,250.00 | rejected: delta ceiling, per-name cap |
| 55.00 | 6.71 | 3.60 | 0.62 | 5,500.00 | rejected: delta ceiling, per-name cap |

**The feasible set is {45.00, 47.50, 50.00}.**

Note what the gate did. It removed the two highest-premium candidates, which
are also the two with the largest downside exposure, and each carries both of
its failing reasons rather than the first found [D-W22]. It removed one
candidate quoted too wide to transact and one too cheap for its commission,
neither of which this example demonstrated before. Rejected candidates are
stored with their reasons so the gate's effect is auditable.

The 42.50 and 45.00 rows carry the same bid and opposite verdicts, which is
deliberate. A bid alone does not say whether a quote is transactable; the
spread does, and the 42.50's wide market puts its mid above the 45.00's,
which is what a stale quote looks like and why an unfiltered mid-derived
figure would corrupt the counterfactual [D-W22]. Both bids sit exactly at the
`0.30` floor and pass it, because the floor rejects a bid below it, not at
it.

Two constraints have nothing to read on this snapshot, and their absence from
the table is that rather than a gap. The expiry window [D-W24] cannot be
shown by one snapshot with one expiry, every candidate here being 46 days out
inside the 7 to 70 window; earnings clearance [D-W25] has no report date to
read, because this example states none. Both belong to checkpoint 2.3's
fixtures rather than to this example.

All three makers now receive this identical set [D-W4].

---

## 4. The three decisions

| Maker | Choice | Reason |
|---|---|---|
| Frozen baseline | 50.00 put | delta 0.24 is inside 0.20-0.30; highest credit in band |
| Random within band | 45.00 put | uniform draw among {45.00, 47.50, 50.00} |
| Learner | 47.50 put | delta 0.16 is inside 0.10-0.20; highest credit in band |

Fills, at the bid less commission:

| Strike | Bid | Gross credit | Commission | Net credit |
|---|---|---|---|---|
| 45.00 | 0.30 | 30.00 | 0.65 | 29.35 |
| 47.50 | 0.55 | 55.00 | 0.65 | 54.35 |
| 50.00 | 0.95 | 95.00 | 0.65 | 94.35 |

---

## 5. What the underlying did

| Date | Close | Note |
|---|---|---|
| 2026-03-02 | 52.40 | trial opens |
| 2026-04-08 | 45.80 | low of the trial window |
| 2026-04-17 | 48.90 | first expiry |
| 2026-04-20 | 48.95 | covered call sold |
| 2026-05-15 | 51.20 | second expiry |
| 2026-05-18 | 51.30 | second covered call sold |
| 2026-06-19 | 53.40 | third expiry |

---

## 6. Resolving each candidate

### 6.1 The 45.00 put

At 2026-04-17 the underlying is `48.90`, above `45.00`, so the put expires
worthless. The trial returns to cash.

- Total: `+29.35`
- Committed: `4,500.00`
- Return on committed: `29.35 / 4,500.00 = 0.652%`
- Duration: 2026-03-02 to 2026-04-17 = **46 days**
- Maximum adverse excursion: the underlying low `45.80` never went below the
  strike, so **0.00%**

### 6.2 The 47.50 put

At 2026-04-17 the underlying is `48.90`, above `47.50`, so the put expires
worthless. The trial returns to cash.

- Total: `+54.35`
- Committed: `4,750.00`
- Return on committed: `54.35 / 4,750.00 = 1.144%`
- Duration: **46 days**
- Maximum adverse excursion: `(47.50 - 45.80) / 47.50 = 3.58%`

The excursion is non-zero even though the outcome was a clean win. The position
was in the money during the window and the endpoint hides that [D-W21].

### 6.3 The 50.00 put, which goes the distance

At 2026-04-17 the underlying is `48.90`, below `50.00`, so the put is assigned.
100 shares are bought at `50.00`.

Gross basis is `50.00` per share. Net basis is `50.00 - 0.9435 = 49.0565`. The
covered call constraint is evaluated against **gross** basis, so only strikes at
or above `50.00` are eligible [D-W19]. Under net basis a `49.50` call would have
looked admissible, which is the drift this rule prevents.

Leg by leg:

| Date | Event | Cash |
|---|---|---|
| 2026-03-02 | Sell 50.00 put, bid 0.95 less commission | +94.35 |
| 2026-04-17 | Assigned, buy 100 shares at 50.00 | -5,000.00 |
| 2026-04-20 | Sell 52.50 call exp 2026-05-15, bid 0.70 less commission | +69.35 |
| 2026-05-15 | Underlying 51.20, call expires worthless | 0.00 |
| 2026-05-18 | Sell 52.50 call exp 2026-06-19, bid 0.85 less commission | +84.35 |
| 2026-06-19 | Underlying 53.40, shares called away at 52.50 | +5,250.00 |

Total: `94.35 - 5,000.00 + 69.35 + 84.35 + 5,250.00 = 498.05`

- Committed: `5,000.00`
- Return on committed: `498.05 / 5,000.00 = 9.961%`
- Duration: 2026-03-02 to 2026-06-19 = **109 days**
- Maximum adverse excursion: `(50.00 - 45.80) / 50.00 = 8.40%`

The trial is measured from open through to return to cash, with the assigned
shares inside the number rather than treated as an exit [D-W17]. This is the
clause that keeps the strategy's downside inside the measurement.

---

## 7. The two scores, which invert

### 7.1 Fast score, marked at the first expiry

Every candidate is marked at 2026-04-17 on a common horizon, so they are
comparable across the chain.

| Strike | Mark | Net | On committed | Fast rank |
|---|---|---|---|---|
| 45.00 | worthless | +29.35 | 0.652% | 2 |
| 47.50 | worthless | +54.35 | 1.144% | **1** |
| 50.00 | intrinsic 50.00 - 48.90 = 1.10, so -110.00 | 94.35 - 110.00 = -15.65 | -0.313% | 3 |

### 7.2 Slow score, trial complete

| Strike | Return on committed | Duration | Slow rank |
|---|---|---|---|
| 45.00 | 0.652% | 46 | 3 |
| 47.50 | 1.144% | 46 | 2 |
| 50.00 | 9.961% | 109 | **1** |

**The two rankings are exactly inverted.** This is not a contrived edge case; it
is what assignment does to a mark-at-expiry view. The learner's objective is the
slow score [D-W20], and the divergence monitor fires here.

---

## 8. Ranks and regret

Regret is computed under the governing slow score, as the best available outcome
minus the outcome actually achieved.

| Maker | Chose | Slow rank | Regret |
|---|---|---|---|
| Frozen baseline | 50.00 | 1 of 3 | 0.000 pp |
| Random within band | 45.00 | 3 of 3 | 9.961 - 0.652 = **9.309 pp** |
| Learner | 47.50 | 2 of 3 | 9.961 - 1.144 = **8.817 pp** |

---

## 9. What this example is here to teach

Four things, and the fourth is the important one.

**Annualization would not have rescued the ranking.** The 47.50 put returns
1.144% over 46 days, which annualizes to about 9.1%. The 50.00 put returns 9.961%
over 109 days, or about 33.4%. The 50.00 strike wins either way here. The reason
the objective is not annualized is not that it changes this case; it is that
across many cases it creates a systematic preference for short trades on
arithmetic alone [D-W18].

**The excursion field is doing work the outcome cannot.** The winning decision was
exposed to an 8.40% adverse excursion against a 5,000.00 commitment. The losing
decisions were exposed to 3.58% and 0.00%. Ranked on outcome alone, the riskiest
choice looks best.

**One sample teaches the wrong lesson.** If a learner updated from this trial, it
would learn to sell higher delta, because the higher delta won. It won because
the underlying recovered. A different path over the same 109 days produces the
opposite result from the same decision, and nothing in the outcome distinguishes
the two.

**Which is why the risk drift check exists.** A learner that repeatedly draws this
lesson will show falling regret and widening excursion at the same time. That
pattern is not improvement, and the standing comparison against the frozen
baseline is what surfaces it [D-W21, `VALIDITY.md` §3].

---

## 10. Fixtures derived from this example

These are registered in `FIXTURES.md` against their checkpoints.

| Fixture | Asserts |
|---|---|
| FX-GateRejectsAboveHeadroom | 52.50 and 55.00 are rejected with reason recorded |
| FX-ThreeMakersSameFeasibleSet | all three receive {45.00, 47.50, 50.00} |
| FX-GrossBasisBindsCallStrike | a 49.50 call is rejected though net basis admits it |
| FX-TrialCompleteIncludesAssignment | the 50.00 trial totals 498.05 |
| FX-ExcursionRecordedOnWin | the 47.50 trial carries 3.58% on a positive outcome |
| FX-FastSlowDivergenceFires | the inverted rankings in §7 raise the monitor |
| FX-RegretUsesSlowScore | learner regret is 8.817 pp, not a fast-score figure |
