# A primer on the wheel

About the strategy itself, not about OptionsWheelLab. Read this if the mechanics are
new. It assumes nothing.

---

## 1. The loop

The wheel is a repeating cycle with four steps.

1. You have cash. You sell someone the right to sell you 100 shares of a stock at
   a fixed price. That is a **put**, the fixed price is the **strike**, and you
   are paid cash for it up front, called the **premium**. You set aside enough
   cash to actually buy the shares, which is what makes it **cash-secured**.

2. Either nothing happens, in which case the contract expires, you keep the
   premium, and you go back to step 1. Or the stock falls below the strike and
   you are **assigned**, meaning you must buy 100 shares at the strike.

3. Now you hold shares. You sell someone the right to buy them from you at a
   fixed price. That is a **call**, and it is **covered** because you actually
   own the shares to deliver.

4. Either nothing happens, in which case you keep that premium too and sell
   another call, or the stock rises above the strike, your shares are sold at
   that price, and you are back to cash at step 1.

That is the whole thing. Sell puts until you own the stock, sell calls until you
do not, collect premium throughout.

## 2. One pass through it, with numbers

Stock trades at 52. You sell one put at a 50 strike expiring in 45 days and
receive 95 dollars. You set aside 5,000 dollars, which is 50 times 100 shares.

**If the stock is above 50 at expiry**, the contract expires and you keep the 95
dollars. That is 1.9 percent on the 5,000 you tied up, over 45 days. You go
again.

**If the stock is at 46 at expiry**, you are assigned. You pay 5,000 dollars for
100 shares now worth 4,600. You are down 400 on the shares and up 95 on the
premium, so down 305 overall.

You then sell a call at a 50 strike and collect, say, 80 dollars. If the stock
recovers above 50, your shares are sold for 5,000 and you end the cycle up 175
dollars in total premium. If it keeps falling, you keep collecting premium while
the shares keep losing value, and the premium is much smaller than the losses.

## 3. Where the money comes from

There is a real reason this can pay, and it is worth knowing because most
explanations skip it.

Options are priced off expected future movement, called **implied volatility**.
On average, the movement that actually happens is slightly smaller than what was
priced in. The gap is called the **variance risk premium**, and it exists because
option buyers are often buying insurance and are willing to overpay for it, the
same way insurance customers pay more than their expected claims.

Selling options harvests that gap. It is a documented effect rather than folklore.
It is also small, and it is compensation for taking a specific risk rather than
free money.

## 4. The shape of the payoff, which is the part that matters

**Your maximum gain is the premium.** No matter how well the stock does, you
cannot make more than what you were paid. Once you are holding shares and have
sold a call against them, a stock that doubles still gets taken from you at the
strike.

**Your downside is nearly the whole position.** If the stock goes to zero you lose
the strike times 100, less the premium collected. On the example above that is
4,905 dollars of risk against 95 dollars of reward.

So the wheel wins a little, often, and loses a lot, rarely. That asymmetry is not
a flaw in how you run it. It is the strategy.

There is a tidier way to say the same thing. Selling a cash-secured put has
essentially the same payoff as buying the stock and selling a call against it.
They are the same position wearing different clothes. So the wheel is not really
an alternative to owning stocks. It is a way of owning stocks with the upside
sold off in exchange for cash today.

## 5. Four ways it goes wrong

**A strong bull market.** You capped your upside. Buy-and-hold beats you, and it
beats you by more the better the market does. This is the most common outcome for
the wheel over long horizons, and it is quiet rather than dramatic.

**A stock that keeps falling.** You get assigned, then you are holding a losing
position while collecting premiums far too small to offset it. The usual mistake
here is selling calls below what you paid, which locks in the loss when they get
exercised.

**A correlated selloff.** The real danger. Every put you sold assigns at roughly
the same time, because the whole market fell together. You now owe cash on all of
them simultaneously. One position going wrong is manageable, all of them going
wrong at once is what actually causes damage.

**A single-name shock.** A fraud, a failed trial, a guidance collapse. The stock
gaps down overnight and there was never a moment to react.

## 6. Why it fools people

Something like three quarters of individual wheel trades are winners. That feels
like skill. A run of thirty small wins in a calm market produces a track record
that looks excellent and a confidence level that is not warranted, because the
losing trades have simply not arrived yet.

Two more traps worth naming.

Premium gets counted as income, which is psychologically satisfying and
misleading. It is not income until the position closes, because the obligation
attached to it is still live.

And "I do not mind being assigned, I wanted to own it anyway" is the reasoning
people use to avoid noticing a loss. It is sound only if your reason for wanting
the stock was true before you collected the premium. If the premium is what makes
the ownership acceptable, you have talked yourself into a position you did not
want.

## 7. What it is not

It is not market-neutral. It is long the stock market and short volatility at the
same time, which means it loses on both counts in exactly the conditions where
you would want something to be working.

It is not a substitute for holding shares. It underperforms them when things go
well and roughly matches them when things go badly.

It is not passive. Every expiry is a decision, which is precisely why it makes an
interesting environment for studying decisions.

---

*This describes how the strategy works mechanically. It is not a recommendation
to run it, and I am not a financial advisor. The wheel's long-run record against
simply holding the same shares is genuinely mixed, and reasonable people disagree
about whether the variance risk premium is large enough to survive costs on
single names.*
