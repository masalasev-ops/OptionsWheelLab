# UI_MOCKUPS

Build state: **not built**. The UI is Phase 11, after the data purchase boundary.
This document is a visual specification, not an implementation guide.

## How to use this document

Paste §2 through §9 into Claude Design as the brief. What comes back is a
prototype, not Blazor. Treat the output as a specification that this document
then absorbs: replace the sketches in §10 with the screens produced, and the
result is the reference the Phase 11 build works from.

The mockups are worth doing now, before any code, for a reason that is not about
the UI. Drawing a panel is a schema review in disguise. If a screen cannot be
drawn from the tables in `DATA_AND_SCHEMA.md`, the schema is wrong, and finding
that on a canvas is cheaper than finding it in Phase 6. Each screen below names
the tables it reads so this check is mechanical.

---

## 1. Brief starts here

Everything from §2 onward is written to be read by whoever or whatever produces
the mockups.

## 2. What this application is

OptionsWheelLab is a research laboratory. It runs a paper-trading options strategy in
order to study a question about decision-making, and its screens exist to show
whether a decision-maker is improving.

Three simulated decision-makers act on the same data every day: a frozen
baseline that never changes, a random control, and a learner. The application
shows what each one decided, what it would have been better to decide, and
whether the learner is closing that gap over time.

## 3. Two constraints that override normal conventions

**This is read-only. Nothing on any screen executes anything.** There is no order
entry, no confirm button, no live blotter, no connection to a broker. Every
figure is the output of a simulation over stored data. Controls are limited to
date pickers, filters, and navigation. A screen that looks like it could place a
trade is wrong.

**Do not lead with profit and loss.** The conventional pattern puts large green
and red currency figures at the top. That contradicts the premise of the lab,
where profitability is a secondary question that is reported but never treated as
the verdict. The primary number on the main screen is a regret curve. Currency
totals appear, but subordinate and unemphasised.

## 4. Vocabulary

Two vocabularies stack in this product and both are unfamiliar to the reader.

**Options terminology**: strike, expiry, premium, put, call, cash-secured put,
covered call, assignment, called away, in the money, delta, DTE, implied
volatility, IV rank, bid, ask, spread, cost basis, roll.

**This lab's own terms**: trial, maker, frozen baseline, learner, candidate,
feasible set, regret, committed capital, fast score, slow score, rank inversion,
adverse excursion, risk drift, simultaneous assignment, pre-registration.

`GLOSSARY.md` defines every one of them and is the source for the wording. Use
those definitions rather than inventing new phrasings, so the product and the
corpus teach the same thing.

**The product must explain both layers in place.** Assume the reader knows
neither. Every term above, on its first appearance on a screen, carries a
definition reachable without leaving the screen, as a hover or a click. This is a
requirement, not a nicety: the first mockup pass was internally consistent and
unreadable to its own owner, because the terms were used correctly everywhere and
explained nowhere.

**Every screen opens with one plain sentence** naming the question it answers, in
ordinary words, using none of the terms above. The dense instrument layout sits
below that sentence. See each screen in §6 for its sentence.

## 5. Tone

Dense, quiet, and instrument-like. Closer to a scientific readout than to a
consumer finance product. Data-heavy tables are appropriate and welcome. Avoid
celebratory styling, progress gamification, and large decorative numbers.

Density applies to the data, not to the explanation. A dense readout that its own
owner cannot read has failed, so the plain opening sentence and the in-place
definitions from §4 are not exceptions to this instruction; they are what make it
survivable.

Colour carries meaning and nothing else. Reserve a single accent for the learner,
a neutral for the frozen baseline, and a muted third for the random control, and
use those three consistently on every chart in the application. Do not use red
and green as the primary encoding, because the primary comparison is between
makers rather than between profit and loss.

## 6. Screens

Six screens. The first two are the product; the rest support them.

### 6.1 Verdict

Opening sentence: *Is the decision-maker getting better, and is it taking bigger
risks to do it?*

The landing screen and the lab's answer.

The regret curve over time for all three makers on one axis, learner against
frozen baseline against random. A falling learner curve relative to the frozen
baseline is the result the lab exists to produce.

**The risk drift panel lives on this same screen, not behind a tab.** Three
series, learner against frozen baseline: adverse excursion distribution shown at
median and upper decile, assignment rate, and committed capital. This placement
is a requirement rather than a layout preference. The specific way this lab would
deceive its owner is regret falling while risk widens, and a reader who has to
navigate to see the second half will eventually stop navigating. The two must be
visible together without scrolling on a desktop viewport.

Also present, small and subordinate: trials closed, the evaluation window, and a
marker showing whether the pre-registered prediction has been committed.

A headline figure for the learner-versus-frozen gap may appear, but only over the
full evaluation window, or else explicitly marked descriptive. A trailing
short-window gap presented as a headline number is the reading `VALIDITY.md` §4
rules out.

Reads: `decision_scores`, `outcomes`, `trials`, `preregistrations`.

### 6.2 Decision detail

Opening sentence: *On this day, for this stock, what could it have sold, what did
it sell, and how good was that?*

One day, one symbol, one maker's decision, opened up completely. This is the
explainability surface, and it is the screen that answers a reader who doubts a
result.

Show the full candidate list for that day including the ones the gate rejected,
with the rejection reason against each. Show each maker's choice marked in the
list. After resolution, show each candidate's fast and slow score, its rank under
each, and the regret assigned to the chosen one.

The fast and slow rankings can invert completely. When they do, say so on the
screen rather than leaving the reader to notice, because that inversion is a
designed teaching case and a monitored condition.

`WORKED_EXAMPLE.md` is this screen rendered as arithmetic. Reproducing that
example's figures on this layout is the test of whether it works.

**Both scores are post-resolution.** Neither the fast nor the slow rank exists at
the moment of choosing, so no maker ever acts on a rank. A maker acts on its
policy rows; the ranks are assigned afterwards. Captions must not describe a maker
as having chosen on a rank, because that describes leakage rather than a decision
[D-W6, D-W20].

Reads: `decisions`, `candidates`, `candidate_outcomes`, `decision_scores`.

### 6.3 Trial board

Opening sentence: *What positions are open right now, and how are they doing?*

Open trials grouped by state: short put, holding shares, short call. Per trial:
symbol, days elapsed, rolls used against the configured bound, gross basis, net
basis, and current adverse excursion.

Show both basis figures side by side and label which is which. **Gross basis
governs the covered call strike constraint; net basis governs nothing and is
shown for reporting only** [D-W19]. State that on the screen, in that direction.
An earlier version of this brief said only that one of them governs a constraint
without saying which, and the first mockup pass asserted the inverse.

Reads: `trials`, `positions`, `ledger_entries`.

### 6.4 Expiry calendar

Opening sentence: *What comes due, and when?*

What resolves when, by date, across all makers. The lab's entire rhythm is driven
by expiries, so a calendar is a first-class view here rather than a convenience.

Reads: `trials`, `contracts`.

### 6.5 Capacity

Opening sentence: *How much cash is tied up, and what would we owe if everything
went wrong at once?*

Committed capital by name against per-name headroom, total committed against the
total cap, and the simultaneous-assignment stress figure, which asks what would
be owed and held if every open short put assigned at once.

The stress figure is the one that matters and should be prominent. The wheel's
real cash-loss event is not one bad position; it is every short put assigning
together in a correlated selloff.

Show the stress figure against its own limit, not against the total committed
capital cap. They are separate configuration keys and separate constraints.

Reads: `trials`, `positions`, `config_rows`.

### 6.6 Feature grades

Opening sentence: *What kinds of contracts have tended to work?*

Which contract properties predicted good outcomes: delta, IV rank, spread width,
days to expiry, distance to earnings.

Lowest priority of the six. Sketch it last or not at all, since it is the screen
most likely to be redesigned once real grades exist.

Reads: `candidate_outcomes`, `candidates`.

## 7. Data realism

Use plausible synthetic figures. Regret in percentage points, typically single
digits. Returns on committed capital in the low single digits per trial, with
occasional larger figures on trials that went through assignment. Adverse
excursion between zero and roughly fifteen percent. Twenty or so watchlist names.
A few hundred closed trials over a year.

Do not show a learner curve that falls smoothly to zero. A realistic curve is
noisy, and a suspiciously clean one would misrepresent what the instrument
actually produces.

## 8. What not to include

No order entry or execution controls of any kind. No broker connection status. No
account funding, deposits, or withdrawals. No real ticker symbols, since these are
simulated results and should not read as claims about real securities. No
leaderboard or scoring gamification. No advice, recommendations, or suggested
trades anywhere in the interface.

## 9. Brief ends here

---

## 10. Produced mockups

### Pass 1 — 2026-07-26

Six screens produced as a single bundled HTML prototype, React over a design
system, one section per screen behind a nav. Source kept as `WheelLab.html`, which predates the rename.

**Held.** All six screens present. Read-only framing intact, with no execution
control anywhere: the only interactive elements in the whole prototype are a date
picker and three radio filters. Regret leads the verdict screen and no profit and
loss figure appears above it. The risk drift panel sits beside the regret curve
in the same viewport, as required. Maker colours are consistent across every
chart. Tickers are synthetic. Every screen prints the tables it reads, and all six
mappings match §6.

**Schema check: clean.** No screen required a field the schema does not hold. Two
findings worth carrying: the pre-registration state is surfaced with its commit
date and hash, which `preregistrations` supports as specified; and the feature
grades screen wants a bucket count and a monotonicity flag per feature, which are
derived at report time rather than stored, so no schema change follows.

**Defects found.**

*UI-1, corrected in this document.* The trial board asserted that net basis
governs the constraint and gross basis governs nothing, which inverts [D-W19].
Cause was a gap in this brief rather than in the mockup: §6.3 said only that one
convention governs a constraint, without saying which. The line now states the
direction. This is the exact drift D-W19 exists to prevent, arriving through the
documentation rather than through the code.

*UI-2, corrected in this document.* A decision detail caption described the
learner as having acted on the fast rank. Both scores are post-resolution, so no
maker can act on either; a maker that did would be reading the future. §6.2 now
states this.

*UI-3, corrected in this document.* The verdict screen carried a trailing
eight-week learner-versus-frozen gap as a headline figure, which is the
short-window reading `VALIDITY.md` §4 rules out. §6.1 now constrains it.

*UI-4, corrected in this document.* The simultaneous-assignment stress figure was
shown as a percentage of the total committed capital cap. It has its own limit and
its own configuration key. §6.5 now says so.

**Next pass.** Re-run against the amended brief. The verdict, decision detail, and
capacity screens are the ones that change; the expiry calendar and feature grades
can carry forward unchanged.
