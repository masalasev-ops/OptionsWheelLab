# GLOSSARY

Two vocabularies stack in this lab and they are different in kind.

**Layer one is options terminology.** Standard, and true outside this project.
Anyone who trades options already knows these.

**Layer two is this lab's own terms.** Invented here, and meaningless outside the
project. No amount of options experience teaches them.

Both layers appear on every screen and in every document. A reader missing either
one cannot follow the corpus, so both are defined here and both must be
explained in the product [`UI_MOCKUPS.md` §6].

---

## Layer one: options

**Option contract.** A right, bought or sold, concerning 100 shares of a stock.
The lab only ever sells, never buys.

**Strike.** The price at which the contract's holder may transact. A 50 strike
means 50 dollars per share.

**Expiry.** The date the contract ends. After it, the contract no longer exists.

**Premium.** The cash received for selling a contract. Received up front and kept
regardless of what happens afterwards.

**Put.** A contract whose buyer may sell shares to you at the strike. Selling one
means agreeing to buy 100 shares at the strike if the buyer chooses.

**Call.** A contract whose buyer may buy shares from you at the strike. Selling
one means agreeing to sell 100 shares at the strike if the buyer chooses.

**Cash-secured put.** A sold put with enough cash set aside to buy the shares if
required. The lab never sells a put it could not fund.

**Covered call.** A sold call against shares already held. The lab never sells a
call it could not deliver.

**Assignment.** The buyer exercises. On a short put, you buy 100 shares at the
strike. On a short call, you sell the 100 shares you hold at the strike.

**Expires worthless.** The buyer does not exercise, the contract ends, and you
keep the premium with no further obligation. The common outcome.

**Called away.** Assignment on a short call, meaning your shares are sold at the
strike.

**In the money.** For a put, the share price is below the strike, so assignment
is likely. For a call, above.

**Delta.** Roughly the probability the contract ends in the money, expressed
between 0 and 1. A 0.20 delta put has about a one in five chance of assignment.
Higher delta means more premium and more risk, and the two move together always.

**DTE.** Days to expiry.

**Implied volatility, IV.** How much movement the option's price implies the
market expects. Higher IV means higher premium.

**IV rank.** Where today's implied volatility sits against its own past year, from
0 to 100. A way of asking whether premium is rich or cheap for this name right
now.

**Bid and ask.** What buyers offer and what sellers want. Selling gets you the
bid. The gap between them is the spread, and it is a real cost.

**Cost basis.** What you paid per share.

**Roll.** Closing a contract and opening a later one on the same position, usually
to avoid assignment.

**The variance risk premium.** The tendency of implied volatility to exceed the
volatility that actually occurs. If the wheel has any edge, this is where it
comes from, and it is why selling options can pay over time.

---

## Layer two: this lab

**The wheel.** The strategy the lab runs. Sell a cash-secured put on a stock you
would accept owning. If it expires worthless, keep the premium and repeat. If you
are assigned, you now hold shares, so sell a covered call against them. If that
expires worthless, keep the premium and sell another. If the shares are called
away, you are back to cash and the loop restarts.

**Trial.** One pass through that loop, from first opening a position to returning
to cash. May span several contracts if rolled. The unit everything is measured
on [D-W14].

**Maker.** One of the three simulated decision-makers running side by side on the
same data: the frozen baseline, the random control, and the learner [D-W4].

**Frozen baseline.** A maker whose rules never change. The yardstick, since any
improvement has to be improvement against something that did not improve.

**Learner.** The maker under study, whose rules change as evidence accumulates.

**Candidate.** One contract that could have been sold on a given day.

**Feasible set.** The candidates that survived the risk rules that day. All three
makers choose from the identical set, so a difference between them is judgement
and not permission [D-W10].

**Regret.** How much better the best available choice turned out to be than the
choice actually made, in percentage points. Lower is better and zero means the
best available choice was made. This is the lab's primary measure [D-W2].

**Committed capital.** Cash tied up as collateral, being strike times 100 times
contracts. Returns are measured against this rather than against premium,
because that cash could not do anything else [D-W17].

**Fast score.** A candidate's result marked at the first expiry. Comparable across
every candidate in a chain, and available sooner.

**Slow score.** A candidate's result over the complete trial, including everything
that happened after assignment. The figure the learner is judged on [D-W20].

Both are computed after the fact. Neither exists at the moment of choosing, so no
maker ever acts on either.

**Rank inversion.** When the fast and slow scores order the same candidates
differently. Assignment is the usual cause, and it is monitored rather than
smoothed over.

**Adverse excursion.** The worst a position got at any point during its life,
regardless of how it ended. A trade can end well and still have been badly
exposed, and the endpoint hides that [D-W21].

**Risk drift.** Regret falling while adverse excursion, assignment rate, or
committed capital widen. Not improvement, but risk borrowed from the tail, and
the specific way this lab would deceive its owner [`VALIDITY.md` §3].

**Simultaneous assignment.** The stress case where every open short put assigns at
once. The wheel's real cash-loss event is not one bad position, it is all of them
landing together in a correlated selloff.

**Pre-registration.** Writing down what result is expected before running the
test, committed with a hash so it cannot be revised afterwards [D-W15].
