# VALIDITY

What the lab claims, what would prove it wrong, and what it structurally cannot
test. This document carries more weight here than its AlphaLab equivalent,
because the risk conclusions are the part that cannot be established from the
data available.

Build state: **not built**. All criteria below are pre-registered specifications.

## 1. The claim, stated so it can fail

> Over a defined window, the learner's regret per decision falls relative to a
> frozen baseline running the same schedule on the same feasible sets, by more
> than the measurement error, without a concurrent widening of its risk profile.

Three clauses, each of which can fail independently.

**Relative to a frozen baseline.** An absolute fall in regret is not evidence,
because a calmer market lowers regret for everyone.

**By more than the measurement error.** Stated in §4.

**Without a concurrent widening of risk.** Stated in §3. A learner that lowers
regret by taking more tail exposure has not improved.

## 2. The improvement instrument

For each closed trial, regret is the difference between the outcome of the
best-ranked candidate in that day's feasible set and the outcome of the candidate
actually chosen, both under the trial-complete metric [D-W17].

The instrument is mean regret per decision over a rolling window, plotted for the
learner and for the frozen baseline on the same axis.

The verdict is the difference between the two slopes over the evaluation window,
tested against the measurement error from §4.

Reported alongside, never in place of: the random-within-band maker's curve, which
establishes the level a no-skill selector achieves inside the same bands.

## 3. The risk drift check

Three series, learner against frozen baseline, over the same window.

**Adverse excursion distribution.** The distribution of maximum adverse excursion
per trial [D-W21]. Compared on median and on the upper decile, since a widening
tail is the failure shape and a mean will hide it.

**Assignment rate.** Fraction of short-put trials that reach assignment.

**Committed capital.** Mean fraction of account equity committed, and peak.

The check raises when regret is falling while any of the three deteriorates
relative to the baseline. That combination is the specific way this lab would
deceive its owner, and it is a standing test rather than a periodic review.

The check is inside the judging layer and receives nothing the learner produced
[D-W6].

## 4. Measurement error and effective sample size

The trap: a year of operation might produce two hundred and forty trials across
twenty names, each scored against roughly two hundred candidates, which looks
like tens of thousands of observations. It is not.

Every candidate on one name on one day resolves against a single underlying path.
Every trial in one month across twenty names resolves against one market. The
effective sample is closer to the number of independent underlying paths than to
the number of scored candidates.

Consequences, all mandatory:

- Regret is aggregated to one observation per trial before any statistic is
  computed. Candidate-level counts are never used as a sample size.
- Standard errors are computed with clustering by date, since trials sharing an
  expiry window share their market draw.
- Any comparison over a window shorter than the window's autocorrelation length
  is reported as descriptive, not inferential.

## 5. What the lab cannot test

Stated plainly so that no future reader mistakes silence for coverage.

**Tail risk calibration.** One to two years of stored chains plus a forward run
starting now will almost certainly contain no crash. The lab cannot tell you the
risk caps are correctly set. It can only tell you they were respected. This is
why the caps are structural and outside the learner's reach [D-W11].

**Fill realism.** The fill rule is an assumption, not a measurement [D-W12]. A
walk-forward run inherits whatever assumption was made and cannot validate it.
Only live execution could, and this lab does not execute.

**Early exercise.** End-of-day data does not show intraday exercise. Assignment is
modelled by rule, principally at expiry and around ex-dividend for short calls
[D-W38, D-W42], and the model is an approximation whose error is unmeasured.

**Regime generality.** With a handful of walk-forward folds in one volatility
regime, any conclusion is conditional on that regime. The lab cannot claim a
learner that improved in 2025 and 2026 would improve in a different environment.

**Whether a maker can learn to decide.** The learner's entire mutable policy is
four configuration rows, a delta band and an expiry window [D-W58], because the
learning channel writes rows and nothing else [D-W6]. So a verdict here is a
verdict about band placement, which is a well-posed experiment and a narrow one.
The lab cannot say whether a maker given a wider space would improve, and a
result reported as decision-making in general would overstate what was
measured.

## 6. Pre-registration

Before the forward run begins, a pre-registration file is committed containing
the evaluation window, the instrument specification, the drift thresholds, and
the predicted held-out result from the historical split [D-W15]. The forward run
refuses to start without it.

The purpose is that the historical split becomes a prediction the forward run
either confirms or refutes, which is worth more than treating it as evidence in
its own right.

## 7. Known ways this lab could fool its owner

Written as a list because it is a checklist, reviewed at every phase sign-off.

1. **Learning-boundary leakage.** The learner learns from trials that opened
   before the boundary rather than closed before it, and reads the future.
   Guarded by FX-LearningBoundaryLagRespected, which asserts the learner sees
   only trials closed before the boundary. FX-PreRegRequired guards a different
   risk: it refuses to start a forward run without a committed pre-registration
   [D-W15].
2. **Survivorship in the watchlist.** Today's watchlist applied to past dates
   removes the names the risk machinery exists to catch [D-W9].
3. **Annualization in the objective.** Short trades win on arithmetic [D-W18].
4. **Netting premium into basis.** Call strikes drift below the cash outlay while
   the profit and loss still looks positive [D-W19].
5. **Fast-score optimization.** The learner optimizes the mark-at-expiry score,
   which can rank candidates in the opposite order to the trial-complete score
   [D-W20]. See `WORKED_EXAMPLE.md` §7 for a case where they invert exactly.
6. **Candidate-level sample inflation.** Statistics computed as if each scored
   candidate were independent. See §4.
7. **Risk borrowed from the tail.** Regret falls, excursion widens. See §3.
