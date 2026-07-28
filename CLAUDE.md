# CLAUDE.md

Rules for agents working in this repository.

Most of what follows is transplanted from AlphaLab, where each rule was paid for
by a defect. Where a rule exists because something went wrong, the reason is
stated, because a rule whose cost is invisible gets negotiated away.

Read `README.md` and `SYSTEM_DESIGN.md` before making changes. Read
`DECISIONS.md` when a change touches a numbered decision.

---

## 1. Verify before asserting

**Confirm any claim about this code or corpus with a clean direct read of the
actual lines. Never from a single grep, and never from memory of an earlier
read.** A grep tells you a string exists somewhere; it does not tell you what
the code does with it. In the sibling project this rule was written after a
series of confident claims that a direct read contradicted.

**Print the HEAD sha and the recent commit list before analysing a branch.**
Analysis against a stale checkout produces findings that were fixed days ago.
This has happened, and four of five raised items were already done.

**A chat artifact is not a corpus artifact.** Something proposed, agreed, or
drafted in conversation does not exist until it is committed to a file in this
repository. Before citing a rule, a clause, or a prior decision, confirm it is
actually in the corpus.

**A code comment is not a corpus record either.** This one is harder to spot
because it is committed, permanent, and in the repository. What makes a record
is where it will be read, not whether it survives. An obligation noted beside
the code that has it, and nowhere the planning for that work will look, is not
recorded. State it where it will be found, then read it back off disk.

**When a claim is challenged with cited sources, treat the citation as probably
right and re-read.** In the sibling project the challenger with a line number
was right nearly every time.

**State uncertainty as uncertainty.** A measured number and an assumed number
are different things, and an argument is not evidence. If something has been
asserted rather than measured, say so in the same sentence.

---

## 2. Non-negotiable invariants

1. **Worker is the sole writer.** The Api opens the store read-only.
2. **Snapshots are append-only.** Never emit `DELETE FROM` or `UPDATE` against
   snapshot tables, or against `decisions` and `candidates` [D-W3, D-W8]. CI
   greps for both.
3. **Money is decimal in TEXT.** No `double` or `float` in any monetary path.
   CI greps for it.
4. **No ambient clock.** Inject `IClock`. No `DateTime.Now` or `DateTime.UtcNow`
   outside the clock implementation.
5. **Reads are as-of.** Every read path serving a simulated date filters on
   `observed_at <= as_of`. There is no "current data" read path for a simulated
   date.
6. **Config is resolved as-of, never as-now** [D-W26]. See §3.
7. **Membership is state, not a filter.** Resolve watchlist membership as of the
   query date [D-W9].
8. **No learner output in the judging path** [D-W6]. Not the scorer, the feature
   grader, the improvement instrument, the risk drift check, or the risk caps.
   Including indirectly: a candidate feature derived from a previous learner
   decision routes learner output into the thing that prices learner output, and
   that is the case people trip over.
9. **Risk caps are not tunable by the system.** They are operator configuration
   [D-W11].
10. **Gate constraints record every failing reason**, not the first [D-W22].

---

## 3. Configuration

Config rows are append-only and versioned; current is `MAX(version)` for a key.
Never update a row in place. A change inserts version + 1, which is what lets a
later behaviour change be explained after the fact.

**Anything reading config for a simulated date must resolve it as of that date,
not read current** [D-W26]. This is structural rather than defensive. Any tool
that evaluates a proposed parameter change inserts a new config version, so by
the time it runs, current config differs from what the original session ran
under. A tool that reads current config will fail its own parity check and the
failure will be misdiagnosed as impure inputs rather than as a config-resolution
bug. The sibling project found this the expensive way.

**No magic numbers at call sites.** A value that could plausibly be tuned is a
config key with an entry in `CONFIG_REFERENCE.md`, not a literal in a
constructor call. In the sibling project two threshold literals sat at a call
site and were invisible to every audit that read the config file.

**`CONFIG_REFERENCE.md` records the verified consumer**, meaning someone read the
composition code and confirmed the binding. Not the assumed consumer. That
document was wrong about a consumer in the sibling project, and two configuration
blocks turned out never to bind at all, so editing them did nothing.

---

## 4. Git and commits

- **Never a whole-file rewrite** when an edit will do. A large diff hides the
  change inside churn and makes review impossible.
- **Separate pure churn from content.** Line-ending normalisation, formatting,
  and renames go in their own commit, verified as whitespace-only, before or
  after the commit that changes meaning. Never in the same one.
- **Branch rather than commit to main** for anything not trivially reversible.
- **Inspect the `.gitignore` diff before committing it.** An accidental
  exclusion is easy to miss and hard to notice later.
- **CI green before merge.**

---

## 4a. Dependencies

**Never suppress a vulnerability advisory to keep a dependency.** Drop the
package, or upgrade to a version that clears the audit. A suppression is
repository-wide in effect even when it reads as local, so keeping one unused
package costs detection everywhere.

When a package is dropped for this reason, record why at the reference site and
add the re-adding to carried obligations in `BUILD_PLAN.md`, so the phase that
needs it inherits the reason rather than rediscovering the advisory.

---

## 4b. Test seams

**Prefer testing through the public surface.** Much of this codebase is
deliberately internal, and internal usually means a bypass someone should not
have: `ConfigRowQuery` is internal so neither configuration surface can be
reached around.

`InternalsVisibleTo` is permitted where the alternative is widening the public
API purely for tests, which is worse. A CLI verb is the clear case: it is an
entry point, and making it public to test it would put it in the API surface
permanently.

The bar is that the public alternative would be worse, not that it would be less
convenient. The failure mode is quiet: once the seam exists, testing internals
becomes the easier path, the public surface stops being what is under test, and
refactoring internals starts breaking tests that should not care.

Record each use at the reference site, so a reader finds the reason before the
precedent.

---

## 5. Documentation

- **Prose states the rule; the decision number is a trailing bracket.** Test by
  deleting the bracket: the sentence must still read and still state the rule. A
  sentence that becomes meaningless without its bracket has made the narrative
  dependent on the ledger, which is how the sibling project's design document
  became unreadable.
- **Every section describing a component carries a build-state marker.** Without
  one, shipped and aspirational prose read identically, and every reader
  re-derives which is which.
- **`SYSTEM_DESIGN.md` is narrative, `DECISIONS.md` is the register.** Different
  audiences, different lifecycles. Do not merge them and do not copy register
  content into the narrative.
- **Never renumber a decision.** A superseded entry keeps its number and gains a
  status pointing at its replacement. The same applies to the `D-W` prefix.
- **Supersede visibly.** A corrected figure is struck and replaced with the
  correction stated, not quietly overwritten. A reader must be able to see that
  a number changed and why.
- **Fixtures live in `FIXTURES.md` and are referenced from there.** Do not
  enumerate fixture names inline in a prompt or checkpoint. A prompt that lists
  fixtures goes silently incomplete the moment one is added elsewhere, which has
  happened.

---

## 6. Decisions and evidence

- **Record the decision before the number that would justify it exists.**
  Deciding to change a rule after seeing that the change makes a result pass is
  result-shopping, whatever the reasoning says.
- **Never loosen a bound because a measurement missed it.** If breaches
  concentrate in one regime, that is a non-stationarity finding. If a rate is
  inconsistent with how the thing was constructed, fix the construction. If a
  bound was inherited from another metric rather than derived for this one,
  re-derive it. Raising it is none of those.
- **A check cannot leave the gating set without a recorded decision.** If a
  mechanism exists to mark a check non-gating, every use of it carries a D-W
  reference. Otherwise demoting an inconvenient check becomes one line of work,
  which is the obvious failure mode of having the mechanism at all.
- **Pre-registration is the only thing preventing retry-until-green**, because
  re-running an analysis over stored data is cheap and unlimited. Predictions,
  selection rules, and thresholds are committed before the numbers exist
  [D-W15].
- **Exclude at the granularity of the doubt.** Doubt about an identity excludes
  the security; doubt about one quoted price excludes that quote.

---

## 7. Long runs

Once a walk-forward or forward run is in flight:

- **Mid-run work is documentation only.** A code change cannot take effect in a
  running process, a rebuild will fail on Windows file locks held by the Worker,
  and a resumed run assembled from two builds destroys the provenance that
  byte-identical reproducibility depends on.
- **Do not stop a run to apply a fix.** File it.
- **Snapshot before any irreversible write.**
- **Know what a change costs before proposing it.** A change confined to
  scoring, grading, or reporting costs a re-run of that stage. A change to the
  decision path, being the gate, the candidate generator, the fill model, or the
  state machine, invalidates recorded decisions and costs a fresh run. Bundle
  decision-path changes.

---

## 8. Working style

- **Do not return every discovery as a task.** A stream of new issues is
  fatiguing and buries the important ones. While a long run is in flight, raise
  only what is dangerous or what blocks the deliverable. File the rest.
- **Distinguish a blocker from a finding** explicitly when reporting.
- **Amendment format**: a short prose verdict first, then labelled clauses in a
  fenced block, scoped to their checkpoint, written in the plan's own style, each
  naming its test and definition of done, so they paste directly into a build
  prompt.
- **Own errors plainly.** State what was wrong, what the correction is, and
  whether anything downstream inherited it.

---

## 9. Style

- Standard keyboard punctuation only. No em dashes.
- Do not write "honest" or "honesty" about the system. State the mechanism.
- When editing text with existing formatting conventions, preserve them rather
  than normalising to your own.
- Around wording that exists for a reason you do not know, append rather than
  rewrite.

---

## 10. Who authors what

The corpus and the repository have different authors, and mixing them makes two
sources of truth for one document.

**Authored content is not yours to write.** Decisions, checkpoint scope,
fixture registrations, and any prose stating a rule are authored deliberately
and arrive as a corpus sync. If a build reveals that one of them is wrong,
missing, or contradicted, report it. Do not close it.

**Verified content is yours to write.** Facts the build establishes and the
corpus can only assert belong to whoever ran the build: the Consumer column in
`CONFIG_REFERENCE.md`, HEAD shas, test counts, and anything in `PROGRESS.md`
recording what was actually built. Correct these directly.

The test is whether the statement is a decision or an observation. A decision is
authored; an observation is measured.

**Two documents have both authors, so neither is ever delivered as a whole
file.** `CONFIG_REFERENCE.md` carries an authored table and a Consumer column
written by the build. `PROGRESS.md` carries authored corpus entries and build
entries recording what shipped. A corpus sync for either arrives as a described
edit to apply, never as a replacement, because a replacement silently reverts
whichever author did not produce it. Every other document has one author and
syncs as a file.

Authored prose that a landed decision has already superseded is corrected, not
reported. The test is whether the decision determines what the text should say.
If it does, correct it and cite the decision. If the correction still needs a
judgement about the wording, it is a finding. This is not licence to overrule a
document by building something else: the authority is the decision, never the
code.

**Before reporting a corpus entry as absent or contradictory, confirm the
working copy is current.** Sound reasoning against a stale tree produces
confident, wrong findings, and this has happened repeatedly. Check the corpus
version in `PROGRESS.md` against the sync you were given.

---

## 11. The reconciliation question

Whenever a decision is added, amended, or superseded, ask:

> **Does this decision change a prompt that has not been spent yet?**

That single question distinguishes a live gap, which must be fixed, from
historical drift in an already-executed prompt, which must be left alone. Spent
prompts are records of what was asked and are never updated.
