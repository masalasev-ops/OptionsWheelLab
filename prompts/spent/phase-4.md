# Phase 4 Decision record and three makers in parallel: spent prompts

Current state below is the whole state of the repository and the only description
of the present. Phase 0's, Phase 1's, Phase 2's and Phase 3's files hold the state
as it stood when the next file opened and are not corrected further.

One prompt per checkpoint, being the prompt that produces the checkpoint as it now
stands. Corrections found while building are folded back into the checkpoint's
prompt rather than appended as further entries, so replaying the prompts in order
against the corpus reproduces the current state without replaying the mistakes.

One file per phase. This file opened at 4.1's sign-off, which closed
`phase-3.md`, on the practice that file records: a phase's archive stays open
past its own sign-off and closes when the next one opens, because closing at
sign-off leaves nothing describing the present in between.

---

# Current state

Corpus v1.47.0.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| Phase 1 | complete, 1.1 to 1.5 built and signed off |
| Phase 2 | complete, 2.1 to 2.5 built and signed off |
| Phase 3 | complete, 3.1 to 3.5 built and signed off |
| Phase 4 | 4.1 to 4.4 built and signed off, 4.5 not started |
| CI | green, 745 tests, guards then restore then build then test, on push to `main` and every pull request |

**The block this one inherits was stale for two checkpoints, which is why
re-measuring it is an act rather than a habit.** In `phase-3.md` it read v1.39.0
through two sign-offs, saying checkpoints were not started while they were built
and merged, and that file's own opening calls the block the only description of
the present. Every section below is re-measured against `main` at this version
rather than edited where it looked wrong.

4.1 changed one `.cs` file, a test pinning a pragma. 4.2 built the decision
record: migration 9's five tables, a writer, a reader, two registered fixtures and
a third whose coverage it repaired. The suite went from 672 to 691 and the guards
from 191 files to 201. 4.3 built the three makers and took the suite to 727 and
the guards to 212. 4.4 gave a maker something to do with a trial it already holds:
the roll and close rule, a fourth choice the state machine honours, and the first
call site `TrialBounds.ResolveFor` has ever had. 727 to 745, and 212 files to
215.

Which branch the work sits on and which pull requests have merged are not recorded
here. Git holds both exactly, and a fact kept in two places drifts.

## Build

.NET 10. Four projects: `Core`, `Worker`, `Api`, `Tests`. Warnings are errors,
nullable is on, `InvariantGlobalization` is on, code style is enforced in the build.
Central package management with transitive pinning.

`Core` has nine folders: `Configuration`, `Storage`, `Identity`, `Time`,
`Synthetic`, `MarketData`, `Membership`, `Positions` and `Generation`.

Repository root holds `README.md`, `CLAUDE.md`, `migrate.ps1`, `seed.ps1` and
`guards.ps1`. Every document is in `docs/`, spent prompts in `prompts/spent/`,
hand-written synthetic chains in `synthetic/`.

## Store

SQLite, WAL, one writer. The Worker is the sole writer [D-W1] and the Api opens
read-only, which is a property of the connection string rather than a convention.
Pooling is off on every connection, which is load-bearing on Windows: with pooling
on the native handle outlives `Dispose` and the snapshot copy fails on a locked file.

Foreign keys are enforced. Microsoft.Data.Sqlite turns them on by default, which a
bare `sqlite3` prompt does not, so a reference cannot dangle through the code path
and can through a hand-written one.

Snapshot-first migrations. The runner takes a `VACUUM INTO` snapshot before applying
[D-W28] and `migrate.ps1` is the operator entry point, so a hand-run cannot skip it.
Schema version comes from `schema_migrations` rather than `PRAGMA user_version`
[D-W32].

**Schema 8.** Migration 1 is `config_rows`, 2 its monotonic `set_at` trigger, 3 the
six market-data tables of §4.1 [1.1], 4 the membership record [1.3], 5 the bars
nullability rebuild [1.4], 6 the `corporate_actions` rebuild for its `kind` CHECK,
7 `market_sessions`, and 8 `trials`, `positions` and `ledger_entries` [3.3].

Thirteen tables, and they fall in two vocabularies rather than one. Eleven are
append-only, being the seven snapshot tables, membership, `ledger_entries`,
`config_rows` and `schema_migrations`. Two are projections of the ledger and may
be rewritten, conditional on the test that discards and rebuilds them [D-W35].
`ProjectionTables` names the second set, and the two lists are asserted disjoint
over the declarations rather than over the tables that happen to exist.

Migration 6 is the second rebuild and the first with rows to lose: migration 5
rebuilt a table no writer had touched, where `corporate_actions` has had one since
1.5, so the copy is asserted through that writer rather than trusted. Migration 8
carries no foreign keys, which §4.3 already said by carrying no arrows: a record
referencing a projection would refuse the discard the rebuild needs.

## Market data

Seven tables: `market_sessions`, `underlying_bars`, `corporate_actions`,
`earnings_calendar`, `chain_snapshots`, `contracts`, `contract_quotes`. Two carry no
key of their own and have an index instead; four carry `observed_at` in the key,
because a correction appends rather than replaces [D-W8].

`market_sessions` landed at 3.3 and carries no symbol, which is the point of it. A
session is a fact about the market and not about a name, and the only other session
sequence is `underlying_bars.session_date`, which is per symbol and cannot tell a
market holiday from a name that did not trade. It is transcribed rather than
derived [D-W46], because a derived calendar's answer about a past date would change
when another symbol was ingested.

`contracts` carries no `observed_at`, since a corporate action mints a new identity
rather than restating a row. It carries two quantities that read as one until 1.1
split them: `multiplier`, what a quoted premium multiplies by, which an adjustment
does not change, and `deliverable_shares`, what one contract conveys, which it does.

Nullability follows what a chain can express rather than what the schema document
leaves unmarked. Bid and ask are required; last, both counts and the five greeks are
absent rather than zero, matching `ContractQuote`. Only a bar's close is required as
of migration 5, matching `UnderlyingBar`, and a standing test compares the table's
pragma nullability against the record's optional properties, so a record change
names the migration owed.

Twelve triggers refuse `UPDATE` and `DELETE`, two per table, generated from a list
frozen at migration 3 rather than from `AppendOnlyTables`, because an applied
migration's SQL cannot change when that vocabulary grows. They hold against a writer
the source detector cannot see; the detector reads `src/`.

**Identity is five components, settled at 1.5** [§2, D-W36]. A contract's
identity is underlying, expiry, right and strike together with the deliverable,
which is what separates an adjusted series from the standard contract listing
beside it at the same strike. `ContractIdentity` carries all five in equality,
hashing and the total order; the uniqueness constraint has since 1.1. Adjusted
terms are transcribed from what the adjusting authority states, never derived
from a ratio [D-W36], and the refusing decimal path is the tripwire: a
derivation producing a non-terminating value cannot be stored at all.

**A corporate action mints a stated successor, atomic with its event row**
[1.5]. `CorporateActionWriter` records the `corporate_actions` row and inserts
the successor with its predecessor link in one transaction; an adjustment whose
stated terms change nothing is refused, and the predecessor reads back
byte-identical. `ContractLineage` walks the link as the recursive CTE the alias
convention was proven to permit, timeless because contracts carry no
observation axis. The corporate-action kind vocabulary widened at 3.3 from `split`
alone to OCC's own enumeration of what adjusts a contract, complete before the
transitions that read it exist [D-W47], and `corporate_actions.kind` gained the
`CHECK` it went without since 1.1.

**One read surface, `AsOfMarketData`, and no current-value counterpart exists at
all** [1.2]. Market data has no operational current-read consumer anywhere in the
design, so the strongest form of "cannot read current" is that no current-reading
type exists to cast to. Every value-returning member takes the as-of date by name
and type, asserted by a shape suite, because a read here filters on two independent
axes, which session the row describes and when it was observed, and a member taking
only the session date would leak the latest observation. A correction recorded
after a simulated date is invisible to a read at that date and visible after it.

The chain read reaches identity through `contracts` on `contract_id`, the latest
observation per contract is a CTE with declared column names, and the uniqueness
constraint's own index serves the symbol lookup, measured with `EXPLAIN QUERY
PLAN`. Identity order is imposed in C# on the parsed identities, because the
stored decimal form does not sort. `corporate_actions` has no as-of read;
`earnings_calendar` gained one at 2.3, `ReportDatesFor`, which takes the
buffered window and returns dates in order, so the buffer's arithmetic stays
with the constraint that owns it.

**`ChainWriter` persists one chain, whole, at one instant** [1.4]. The chain
carries no instant and the writer takes one, stamped on every row. Same-instant
re-ingest is refused with the correction path named, the primary keys remaining
the enforcer; a new instant appends alongside, each observation visible to its
own as-of. Contracts are found or created on the unique tuple, an upsert being
impossible by construction under the append-only trigger, and a multi-match on
the four-tuple refuses rather than guesses, naming §2's unsettled identity
question. FX-WorkedExampleChainPersists holds the round trip against the
document's own tables, to the cent.

Every rendered value binds through `AddStored`, the write-side seam [D-W29]:
decimals through the refusing entry point, dates, instants and rights through
their stored forms, nulls as DBNull. Exclusivity is review's to hold, a
type-level check having been declined [D-W33].

## The worked example

`WORKED_EXAMPLE.md` is the oracle for three fixtures and, since 2.1, teaches
the gate as well as the wheel. Its chain is seven strikes on one expiry: three
feasible, one failing the spread cap alone at 37.84 percent of mid, one failing
the premium floor alone at half of it, and two failing the delta ceiling and the
per-name cap together, which is the case for recording every reason rather than
the first [D-W22]. The expiry window and earnings clearance are named in §3 as
constraints one snapshot cannot demonstrate, and belong to 2.3's fixtures.

The oracle pins the symbol, the snapshot date, the expiry and the right, and
parses everything else out of the document's tables, so the document and
`synthetic/worked-example.json` move together and no test code changes when they
do. §2's and §5's table headers are the parse keys, matched ordinally against the
first table carrying them, which makes a reworded header a silent miss caught
only by the fixtures' own emptiness assertions.

**§3 became an oracle at 2.2, not only §2.** The third fixture enumerates the
chain and compares against both tables, so the document's two halves are
checked against each other for the first time: a revision touching one and not
the other was previously invisible. §3's opening claim is prose and nothing
parses prose, so the claim is read as the rows of its own table.

Every figure from §4 onward derives from a bid or a close. That is what made the
reconciliation small, and it is the property to check before revising the chain
again.

**§10 lists seven derived fixtures where the registry cites this document in
nine rows, and that is not a defect.** Its sentence claims that everything in
its table is registered, which is true of all seven; it makes no claim in the
other direction, and `FIXTURES.md` rule 4 says adding a fixture is not a doc
change requiring propagation. The two absent rows are the chain loader's, out
since Phase 0, and the enumerator's.

## Membership

`watchlist_membership` records transitions, `joined` or `left` effective on a
date, keyed on symbol and version [D-W35]; an interval per version cannot answer
the membership question, which §4.2 demonstrates on re-entry. Three triggers
hold it: two append-only refusals and a monotonic `observed_at` per symbol,
which is `config_rows`' geometry, because version order crosses stamp order here
and does not in the snapshot tables.

`MembershipWriter` is `ConfigWriter`'s shape: `MAX(version) + 1` computed inside
the insert, `RETURNING`, both instants as parameters. The kind renders through
`StoreMembershipKind`, declared not derived, and the read's filter is rendered
through the same declaration rather than restating it as a literal.

`AsOfMembership.MembersOn(date, asOf)` resolves the sequence: among rows visible
at the as-of instant and effective at or before the date, the greatest
(`effective_on`, `version`) governs. Latest-version resolution fails when a
correction carries an earlier effective date than a later genuine transition,
measured by a test that fails under it. A correction supersedes a transition
only by tying its date; a wrong date is a compensating pair. Its own type
rather than a member of the market-data surface, because the one-surface
guarantee there rests on a premise never argued for membership, and its shape
suite and no-current tripwire are mirrored copies.

**Two members since 2.2, resolving through one ranking** [2.2].
`WasMemberOn(symbol, date, asOf)` is the per-symbol read Phase 1 withheld on
the grounds that a member nothing calls is speculation; the candidate
generator asks per name, so it has a caller. The ranking is one const behind
one private resolver, so the narrower question narrows the input and never
restates the rule: two copies would drift the way two copies of one fact do.
Narrowing is sound because the window partitions by symbol, so restricting the
input to one symbol cannot change that symbol's ranking.

The symbol predicate is substituted rather than bound, and that was measured.
`($symbol IS NULL OR symbol = $symbol)` would hold the text literally
constant and costs the seek: `EXPLAIN QUERY PLAN` reports `SCAN
watchlist_membership` where the direct predicate reports `SEARCH ...
(symbol=?)`, because SQLite will not seek an index through an `OR` on a
parameter's nullness. A scan per call is most of what a per-symbol read
exists to avoid, so the ranking is stated once and only the predicate varies,
through a SQL-comment placeholder that leaves the template valid SQL. The
every-symbol read scans, which is what reading every symbol has to do. The
agreement sweep between the two members is evidence rather than the property,
and is the tripwire if the one-text choice is undone.

## Candidate generation

**Enumeration and both constraint families** [2.2, 2.3, 2.4].
`CandidateGenerator.EnumerateFor(symbol, simulatedDate, state)` asks
membership, then the chain, and keeps the quotes whose right the state makes
sellable. `GateFor(symbol, simulatedDate, state, book)` runs both families over
that set and returns every candidate with the reasons it failed, rejected ones
included, because the gate's effect is auditable only if what it refused travels
with why [D-W5, D-W10]. 2.5 assembles and orders the feasible set.

`GateBounds` resolves the six contract bounds once per evaluation rather than
once per candidate, `PortfolioBounds` the four `Risk:` values the same way, and
an unresolvable bound stops the evaluation naming the key and the date [D-W37]
rather than admitting or rejecting. Both call one internal helper, so D-W37's
message exists once. That path is reachable in ordinary use, not only in tests:
the seed stamps `set_at` from the wall clock, so every bound resolves null for
any simulated date before the seed ran, which is the Phase 9 obligation.

The message does not say which record failed, and the key implies it only by
convention: the six `Gate:` keys and the four `Risk:` keys partition cleanly,
nothing states that a record's keys share a section, and nothing checks it.

Two bound records rather than one, because the two families are evaluated apart
and widening would make every contract-constraint site supply numbers that
family cannot read.

`PortfolioConstraints` is pure and handed its values too, and asks the other
question of SYSTEM_DESIGN §3.4: whether the book can carry the position rather
than whether the contract belongs in the set. Three caps against equity, and
D-W19's gross-basis rule binding a call strike, which admits a strike exactly at
basis. A call arriving with no basis stops rather than resolving either way,
which is D-W37's argument through book state rather than configuration.

`PortfolioConstraints` exposes its three headrooms and that is deliberate. A cap
whose bound is never reached passes whether or not it is wired, so WORKED_EXAMPLE
§3 alone cannot tell a working total cap from one reading the wrong exposure;
asserting the headrooms through the functions the constraint compares against is
what does.

**The total cap and the assignment limit are indistinguishable by any
rejection.** Both fractions are 0.60 and assignment exposure never exceeds
committed capital on a book this lab can hold, so a candidate breaching one
breaches the other, and the two tests separating them work at a configuration the
store does not hold and could.

`BookState` carries committed capital in the name, committed capital in total,
and a nullable gross basis. It is a parameter because `positions` is Phase 3's.
Assignment exposure is not a field: it is committed capital today, derived where
it is compared and stated there, so Phase 3 must touch the site that states the
equality rather than notice a field that has quietly been a copy. `GateFor` takes
the book required rather than defaulted, because a cap against an empty book
admits everything.

`CommittedCapital` is one site, being strike times deliverable times contracts.
Which quantity D-W17 means is Phase 3's open obligation; 2.4 reads the
deliverable because it is the only one in reach and says so rather than deciding.
What a covered call commits is a further open question, D-W17 fixing a trial's
committed capital at open, and 2.4 charges the candidate's own figure whatever
the right, which is the tighter reading of a cap.

`ContractConstraints` is pure and handed its numbers, so it reads no
configuration and no clock. Its comparisons are deliberately not uniform and
each comes from the decision that states it: exceeds for the spread cap and the
delta ceiling, strictly below for the premium floor, an inclusive range for the
expiry window, an inclusive edge for the earnings buffer. The crossed check
precedes the spread ratio, because a crossed quote's mid is not a midpoint and
the ratio below it would be arithmetic on a quantity that means nothing.

`GateReason` is ten grounds in declared order, which is the order they are
recorded in, because three makers receive byte-identical sets [D-W4]. Contract
grounds run one to six and portfolio grounds seven to ten, so a reader can see
which question a candidate failed. Every entry names a decision stating its
ground, and a standing test reads the enum's own summaries and requires it.
`StoreGateReason` declares all ten stored forms rather than deriving them, eight
being unreachable from the member name by any casing rule. Nine of the ten can
land on one candidate, the crossed reason being unreachable beside the spread
cap, and FX-GateRecordsAllReasons asserts that maximum against the enum's own
length.

`GatedCandidate` compares its reasons by sequence rather than by the reference
a record's default comparer would use, order included. Two candidates with one
contract and one set of reasons in one order are the same verdict, which is what
D-W4 asks and what the synthesised equality answered wrong until 2.5.

The order a feasible set arrives in is the chain read's, imposed once at
`AsOfMarketData.QuotesFor` and inherited everywhere after. Filtering preserves
order, so the gate adds nothing; the read owns the sort and the test that holds
it, and the gate asserts the property. Identity orders on expiry before strike,
so the ordering suite supplies two expiries scrambled, every other chain here
being one expiry.

Enumeration filters on nothing but position state and membership. A deep
in-the-money put is enumerated and will be rejected by every constraint, which
is what makes the gate's effect auditable [D-W5, D-W10]; a generator that
pre-filtered would produce a smaller record of what the gate did. Nothing about
basis or moneyness happens here, which is why the gross-basis constraint on a
call strike belongs to 2.4.

One simulated date reaches four parameters across two reads, written out with
named arguments at both call sites. The two axes exist because they can
differ: collapsing them is correct for a simulated run and wrong for a
backfill. Order is inherited from the chain read rather than imposed twice,
and asserted at the generator's own output instead.

`EnumeratedCandidate` carries the quote and nothing else. It is not the
`candidates` row, which is Phase 4's, and it declines `contracts_qty`,
`committed_capital`, `credit` and `feature_json`: none of 2.3's constraints
needs them, and the quantity computing committed capital is the open Phase 3
obligation, so building the economics here means choosing between the
multiplier and the deliverable at the checkpoint with no reason to. 2.4 needed
committed capital and still did not put it here, since it is a function of the
identity the record already carries and the obligation is better served by one
computing site than by one field.

`PositionState` is the concept without its table, four tags rendered through a
declared `StorePositionState`. It starts at one, because a `default` reading
as `cash` would enumerate puts against an account holding shares. Cash sells
puts and shares sell calls [D-W16, D-W19]; both short legs enumerate nothing,
because no document states what a roll enumerates, the bounds are Phase 3's
[D-W14], and enumerating a guess would put an unrecorded rule into the
decision path. The test asserting that is written to fail the day Phase 3
writes the rule.

## Trials, the ledger and the fill

**Built across 3.3 to 3.5, and a loop drives it but nothing chooses.** The machine
is a function from a state and a session's facts to a state and its entries; the
bounds, the calendar and the costs arrive resolved, so it reads no configuration,
no clock and no table.

`TrialRun` steps a session range and applies the choices each session needs,
supplied rather than chosen, producing a ledger. Order within a session is choice
then advance. Four choices from 4.4: open, write a covered call, roll, and close
the trial by buying its short back. The fourth is `Bound`'s arithmetic under a
different close kind, because a maker that closes and a bound that binds put the
account in the same place and only the trigger differs; its price arrives as a
fill, which is what a bound cannot do, having no choice to carry one. It refuses rather than skips: a choice outside the range, a bar the
calendar does not carry, and a choice the state cannot honour each stop the walk
naming the session and the state, on [D-W48]'s argument one level up, since a
mis-described run that produced a plausible ledger would be worse than one that
stopped. Two invocations produce byte-identical output, compared as the ledger and
both projections read back out of two independently migrated stores rather than as
a database file [D-W28].

Four states as a discriminated union, and the events that move between them lie
on two axes rather than in one list [D-W47]. Contract events are expiry and
assignment; corporate actions reach a trial from its underlying and carry OCC's
enumeration rather than the lab's. Earnings is on neither, being a gate input that
moves no position, and exercise is assignment seen from the side this lab is never
on. An action the lab does not model stops the trial and is valued at the close
rather than zeroed [D-W49], because zeroing made every name with a corporate
action a total loss.

The order within a session is stated at the type and is not arbitrary: an
unmodelled action stops the trial before anything prices it, early assignment is
checked before expiry because it acts on the session before an ex-date, and the
bound is last because a trial that expired to cash has already ended. The bound
also waits for a state the account knows about, since an expiry resolving to
assignment leaves a state effective on the next session [D-W39].

`ledger_entries` is the record and carries eleven kinds, four of them pairs
because one cash direction hides two events [D-W48]. It records events and not
only cash, so an expiry that pays nothing is a row with a zero amount. `trials`
and `positions` are projections of it, rewritable only because a test discards and
rebuilds them, which is also the only thing proving the vocabulary carries enough
to rebuild from.

The rebuild resolves the trial's bounds itself from 4.4, as of the session the
trial opened [D-W53], taking that session from the ledger's first entry rather than
from `trials.opened_on`, which is a column it is about to discard. That is what
separates `closed_at_bound` from `closed_by_choice`, since both write the same
`bought_to_close` with nothing following. **The branch doing the separating had
never executed before 4.4**: the files writing such an entry and the files calling
the rebuild were disjoint sets.

`FillModel` prices a quote: the price times the multiplier [D-W17], at the bid for
a sale and the ask for a purchase [D-W12, D-W49]. A leg writes two entries, the
premium and the commission, per contract and per leg [D-W50], and the projection
folds the commission back because net basis is what the account paid per share.

**Two quantities that agree for every standard contract, and money follows only
one.** An adjustment moves the deliverable and leaves the strike and the aggregate
exercise price alone, so cash multiplies by the multiplier and the deliverable
says how many shares change hands. `ContractTerms` is the one site, and
FX-NoShareCountInOptionCash is what keeps it one.

## The decision record and the makers

**Built across 4.2 to 4.4, and nothing drives it.** Five append-only tables, a
writer, a reader that rebuilds a decision from the record alone, and three makers
that choose from a set they are handed and act on a trial they already hold. What
asks a maker for a decision today is a test.

The feasible set is stored once per symbol, session and right and referenced by
every decision made against it [D-W52]. The key comes from what the generator
reads: position state reaches enumeration only through the right it makes
sellable, so at most two non-empty sets exist per name and session however many
makers there are. That is what makes [D-W4]'s byte-identical property true by
construction rather than by three writes agreeing, and a maker arriving with a
different set is refused rather than merged.

The gate reasons are two tables because their inputs differ. Six are raised from
the candidate, the bounds and the report dates and are the same for every maker
sharing the set; four are raised against a book and belong to the maker whose
book produced them. Each `CHECK` carries its own family rather than the whole
vocabulary, so the schema refuses a portfolio verdict written to the shared table.

A reason is a row with no ordinal, and there is no `gate_status`: a candidate is
feasible exactly when nothing refused it. `bid` and `ask` are columns rather than
fields inside `feature_json`, because money is decimal in TEXT and a value inside
a blob is one the canonical form does not reach.

`policy_version` is derived as the maximum over the keys each policy factory read,
not over the keys under one prefix, because the random maker borrows the
baseline's expiry window and a prefix-scoped maximum would miss it.

Three arms behind one interface. The baseline and the learner are one algorithm on
two policies, since the channel writes rows and nothing else, so a rule in code is
one no channel could change. The random control differs in rule rather than in
rows and builds its generator inside the call from a seed derived per session and
name. A band admits its own bounds and admits no candidate whose delta is absent.

A maker acts on an open trial at or inside seven days to expiry and only if the
short is in the money [D-W54]. It closes if a bound has been reached, if its band
admits nothing, or if the roll would pay a net debit, and otherwise rolls. A
session with no chain has no feasible set, so a maker can neither roll nor close
and the position runs to expiry, which is why `WORKED_EXAMPLE` §6.3 still reaches
its assignment under a rule that would otherwise have acted on it.

The trial arrives as a parameter and carries no `PremiumBanked`, `GrossBasis` or
`NetBasis`, so a rule that rolled to defer realising a loss cannot be written
rather than being discouraged. The selection function is injected, so each arm
rolls by the rule it opens with; D-W54's sentence naming the highest-credit rule
reads two ways at the random control, and taking it literally would make that
control roll by a rule it does not open with.

## Configuration

Two sections bound, `Eodhd` and `Storage`, both verified. Six sections deliberately
unbound because `CONFIG_REFERENCE.md` classes them `rows` and a registered options
type is itself a current-value accessor.

All 24 `rows`-classed keys hold a value at version 1, written by the `seed` verb.
`Costs:AssignmentFee` was the last one owed and was set at 3.4, transcribed from a
named broker's published schedule with a retrieval date [D-W50], which is a kind
of provenance the seeder did not previously have: every other entry is transcribed
from this corpus, taken from a decision's proposed value, or judged. The store is
the authority on what is in force, not the document.

The gate's six contract bounds were seeded at 0.8: a spread cap of 0.12 of mid
and a premium floor of 0.30 [D-W22], a delta ceiling of 0.35 [D-W23], an expiry
window of 7 to 70 [D-W24], and an earnings buffer of 7 [D-W25]. All six are read
as of the simulated date since 2.3, so their Consumer column is verified rather
than assumed, and all six are named in `ConfigKeys` where two were: a key the
code reads is not a literal at a call site.

The four `Risk:` keys were seeded at 2.4, which is where the caps first read
them: equity of 100000.00, a per-name cap of 0.25, a total cap of 0.60 and a
simultaneous-assignment limit of 0.60. Three are transcribed from
`WORKED_EXAMPLE.md` §1 and only the fourth is chosen, and each row's Notes say
which. Equity is a key rather than a derived figure because a denominator
computed from the run's own state would loosen every cap during a drawdown
[D-W11]. None belongs to a cross-key invariant, so `InvariantKeys` is unchanged
and nothing ties the total cap to the assignment limit, deliberately: the two
coincide only while every position is a cash-secured put.

Against those values the caps admit 25,000.00 in one name, 60,000.00 in total
and 60,000.00 of assignment exposure. On §1's book of 19,900.00 in `WDGT` and
38,000.00 overall, that is the 5,100.00 headroom §3 rests on and 22,000.00 of
the other two. Two names at the full per-name cap commit 50,000.00, so the total
binds part-way through a third rather than at a whole number of them.

The three `Costs:` keys were seeded at 0.8 and 3.4 and are read as of the
simulated date by `CostBounds`, which `FillModel` resolves. Seventeen rows carry a
verified consumer and eleven do not, of which nine are specified-only because
their checkpoints are Phase 4's and Phase 5's. **The two `Trial:` rows are the
only unverified rows whose checkpoint has landed, which is a defect rather than a
gap.** `TrialBounds` exists and resolves both as of the simulated date, and no
file under `src/` calls it: the machine is handed resolved bounds and the
component that would resolve them is the run loop.

What verifying takes was measured at 3.4 rather than assumed. A type in `src/`
resolves the key and a component in `src/` calls that type; it is not about who
constructs the component, since `CandidateGenerator` is built only by tests and
its ten keys have been verified since 2.4.

Both directions of the key contract are standing checks for `app` keys. For `rows`
keys only the types-to-document direction holds, because most are deliberately
unbound until their phase.

Configuration is readable two ways, as separate types so neither can be reached from
the other. `ConfigWriter` appends `MAX(version) + 1` computed inside the insert's own
transaction and reports the version through `RETURNING`, so the number returned is
the one that statement wrote. Both cross-key invariants run on every write, and a
write leaving one unevaluable is refused [D-W34].

## Stored forms

Dates are `yyyy-MM-dd`, timestamps `yyyy-MM-ddTHH:mm:ss.fffZ`, filenames
`yyyyMMddTHHmmssfffZ`. Decimals are fixed-scale at 8 places [D-W29], with a refusing
entry point and a rounding one, lenient on padding and strict on precision.

The form is not order-preserving, so no SQL orders, ranges over or aggregates a
decimal column. `DecimalColumns` holds twenty-one names as of 3.3.

Decimals reach `TEXT` columns through `AddStored`, rendering through the refusing
entry point. `AddStoredRounded` is the rounding path, added at 3.4 and named
rather than defaulted, and the only values taking it are the two cost bases, which
are divisions: a premium carrying the eight places the scale admits gives a basis
needing ten. `ConfigWriter` still takes strings, `config_rows.value` being
polymorphic by design.

Six vocabularies have a declared stored form and a `CHECK` that must agree with
it, asserted in both directions and including that no `CHECK` admits a value the
code cannot produce. A seventh, `StoreFillPoint`, has no `CHECK` to compare
against, because `config_rows.value` carries every section's values and a
constraint there would have to know which key a row belongs to. That exclusion is
stated at the type, at the fixture and in the registry row, and a case fails if
that column ever gains one.

## Guards and detectors

`guards.ps1` runs before restore, so it reports on a tree where nothing else can.
Three named checks, no exemption mechanism, self-testing on their own samples. The
third arrived at 3.3's review: no file under `src/` prices an option from a share
count, which holds the claim that the quantity sits in one place. That claim had
been made in a comment, was true when written, was false three commits later, and
was unchecked throughout. A check may now state which tree its rule governs, and a
scope matching no files throws.

Three SQL detectors, all reading `src/` only: no decimal ordering, no rewrite of an
append-only table, and no alias of a table or a column. The third is the convention
that discharges the alias obligation, and it is what makes the other two sound
without either resolving aliases. Its source arm admits a parenthesised expression
as of 1.2, so an aggregate acquiring a name is reported; a CTE header stays clean
because the alias group requires an identifier and the token after `AS` there is
`(`, which no identifier can match.

**A declared vocabulary is checked standing in the direction in which absence causes
the bad outcome.** `DecimalColumns` and `AppendOnlyTables` run list to document, a
name with no table being the error. `PolicyBandCeilings` runs document to list, a
band with no entry being the error, because the ceiling is compared only against the
bands the list names.

## Tests

745: 461 across sixty-five fixtures, and 284 across forty unregistered
suites. The one 4.1 added pins that foreign keys are enforced on a connection this
store opens, read through the real factory, because a probe written for the same
question set the pragma before reading it and reported the value it had written. The three guards are checks rather than tests and are counted in neither.
2.1 registered none and changed none, which is what a document-only checkpoint
pinned by existing fixtures looks like; 2.2 registered two, 2.3 seven, 2.4
four and 2.5 one.

**A suite observed to pass is not a suite shown able to fail.** 2.2's two
mutation checks are the method: a generator returning puts unconditionally
fails five of ten cases in the generator suite, and a membership read that
always answers yes fails four of four in FX-OffWatchlistRejected. Neither
number is visible from a green run.

| Fixture | Tests |
|---|---|
| FX-ClockIsNotADateSource | 34 |
| FX-SnapshotNeverRewritten | 27 |
| FX-NoRewriteOfAppendOnlyTables | 20 |
| FX-NoDecimalOrderingInSql | 19 |
| FX-MalformedChainFailsWhole | 18 |
| FX-NoSqlAliases | 17 |
| FX-MoneyRoundTrip | 17 |
| FX-ConfigWriteRefusesInvariantBreach | 17 |
| FX-StoredVocabulariesMatchTheirChecks | 15 |
| FX-NoNondeterministicSql | 13 |
| FX-UnmodelledActionStopsTheTrial | 12 |
| FX-TickerDashForm | 12 |
| FX-ConfigStoreClassHonoured | 12 |
| FX-RollAtTheThreshold | 11 |
| FX-RecordCarriesFeasibleSet | 8 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-TrialBoundsFixedAtOpen | 6 |
| FX-RunRefusesAChoiceTheStateCannotHonour | 6 |
| FX-MigrateFromEmpty | 6 |
| FX-EveryConfigSectionBinds | 6 |
| FX-ConfigResolvesAsOf | 6 |
| FX-WorkedExampleDecisions | 5 |
| FX-TrialCompleteIncludesAssignment | 5 |
| FX-TotalCapRejectsAboveHeadroom | 5 |
| FX-StoppedTrialIsValuedAtTheClose | 5 |
| FX-RegistryMatchesDisk | 5 |
| FX-OrdinaryDividendLeavesContractUnchanged | 5 |
| FX-EveryPolicyBandIsChecked | 5 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-EarningsClearanceRejects | 5 |
| FX-EarlyAssignmentOnDividend | 5 |
| FX-BoundClosePaysTheAsk | 5 |
| FX-AssignmentStressRejects | 5 |
| FX-WorkedExampleGateVerdicts | 4 |
| FX-WorkedExampleEnumerates | 4 |
| FX-WorkedExampleChainLoads | 4 |
| FX-RollCapCloses | 4 |
| FX-ProjectionRebuildsFromLedger | 4 |
| FX-ProceedsUsableOnSettlement | 4 |
| FX-OffWatchlistRejected | 4 |
| FX-NextSessionSkipsAClosedDate | 4 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-GrossBasisBindsCallStrike | 4 |
| FX-GateRejectsAboveHeadroom | 4 |
| FX-GateRecordsAllReasons | 4 |
| FX-ExpiryResolvesAtOneCent | 4 |
| FX-DividendReachesLedger | 4 |
| FX-DeltaCeilingRejects | 4 |
| FX-DecisionsShareOneFeasibleSet | 4 |
| FX-CrossedQuoteRejected | 4 |
| FX-ChainLoadsInIdentityOrder | 4 |
| FX-AssignmentKnownNextSession | 4 |
| FX-WorkedExampleChainPersists | 3 |
| FX-ThreeMakersSameFeasibleSet | 3 |
| FX-SnapshotRestoresIdentically | 3 |
| FX-RunIsByteIdentical | 3 |
| FX-PitMembershipExcludesLaterJoiner | 3 |
| FX-NoCurrentConfigReadOnSimulatedPath | 3 |
| FX-EveryAppKeyBinds | 3 |
| FX-CoveredCallCommitsNothingFurther | 3 |
| FX-CorporateActionMintsSuccessor | 3 |
| FX-ApiCannotWrite | 3 |
| FX-SpreadCapRejects | 2 |
| FX-PremiumFloorRejects | 2 |
| FX-DteWindowRejects | 2 |

The suite parses `CONFIG_REFERENCE.md`, `FIXTURES.md`, `DATA_AND_SCHEMA.md`,
`WORKED_EXAMPLE.md` and `guards.ps1`, so all five are load-bearing rather than
descriptive. **Nothing parses §4.1 as a schema**, which is why the schema document
and the migration diverged in six places at 1.1 and no check caught it.

Every store test creates its own database in a temp directory, because the
append-only triggers make the tables impossible to clean between cases.

## Not built

**Nothing drives a maker.** Three makers choose from 4.3, they roll and close
from 4.4, and a loop steps a calendar from 3.5, and the two are not joined:
`TrialRun` still takes a supplied sequence, so what asks a maker for a decision
today is a test and nothing turns a maker's roll into the choice the machine
applies. The scorer that would re-score a decision is Phase
5's. `TrialRun` is also handed a machine
already constructed, so no composition root resolves bounds and builds one. The
two `Trial:` configuration rows are verified regardless, by the rebuild rather
than by a run.

`decisions` and `candidates` are Phase 4's, so nothing persists a candidate or its
reasons and a roll's decision row has nowhere to go. The trial's `maker_id` is the
one projection column the ledger cannot supply, which is why a rebuild preserves it
rather than reconstructing it [D-W35, as amended].

No `FeasibleSet` type, and that is a choice rather than a gap. `GateFor`
returning every candidate with its reasons in identity order is assembly,
ordering and the refusal record; a type justified only by a later consumer is
speculation, and no maker exists until Phase 4. The set's grain is (symbol,
date) and Phase 4's obligation carries it.

`BookState` is still a parameter and nothing computes one. `positions` exists from
3.3, so the backward edge SYSTEM_DESIGN §3.3 names as the only one in the daily
path could now be a read, and is not: no caller assembles a book from the table.

Rolling has a rule from 4.4 [D-W54] and no run exercises it. A maker decides to
roll and the machine applies one, and nothing between them turns the first into
the second, so every case that rolls is a test handing the machine a choice a
maker was not asked for.

`corporate_actions` has a writer and no as-of read. 1.5 reaches a predecessor
through `ContractLineage`, which is timeless, and the state machine takes the
actions on a session as a parameter rather than reading them, so nothing yet asks
what was in force at a date.

No operator entry point ingests a chain, and none runs a trial. `ChainWriter`,
`TrialStore`, `FillModel` and `TrialRun` have tests as their only callers.

**Determinism is asserted over a run's output from 3.5**, which is the form 0.5
stated and restated as stored rows for want of a run to make. What is still not
asserted is determinism over a run a maker drove, since the choices are supplied.

## Owed

Work deferred out of a checkpoint is registered in `BUILD_PLAN.md` carried
obligations, which is where planning for the phase that owns it will look. It is not
copied here: two registers of one list is how an obligation comes to exist in the one
nobody reads.

Entries stand against Phases 5, 6, 8, 9 and 11, and against no checkpoint, 4.4
having closed both of the rows that named one.
The count is not restated here. **The column names a checkpoint once the owning
phase's detail exists and a phase otherwise**, stated at the table because two
readings of it disagreed: a count over phase names alone misses the rows that have
moved on. **Authoring a phase's detail promotes rows in two registers**, this one
and `FIXTURES.md`'s Checkpoint column, and v1.43.0 moved one and not the other,
which leaves every checkpoint's entry-to-artefact definition of done resolving to
nothing. Corrected at 4.1 and the practice recorded where the trigger is.

**3.1 to 3.5 and 4.1 to 4.4 owe nothing.** 3.1 closed four rows while raising three;
3.2 closed its own and raised three; 3.3 closed four and raised five; 3.4 closed
three and raised one; 3.5 closed two and raised none; 4.1 closed three and raised
none; 4.2 closed none and raised two and 4.3 closed none and raised one, both having
carried nothing to close because 4.1 took all three of the grains this phase
owed. 4.4 closed two and raised one, and one of the two it closed was 4.5's: both
rows named one missing call, which is a shape to watch for, since a reading that
checks only the rows naming the checkpoint in hand would have left it standing
against work already done. **Phase 2 owes nothing.** 2.1 discharged the reconciliation row raised at
v1.6.0, the table's oldest and open for twenty-three corpus versions, 2.3
discharged the crossed-quote row while opening two of its own, and 2.4
discharged the risk row while opening one of its own. All three discharged rows
were preconditions rather than work items, which is what put them at the front
of the phase.

2.4's row asked what a covered call commits and 3.1 answered it: nothing beyond
the trial's committed capital [D-W43], which the caps read as one figure from open
to close. 2.4's own reasoning held, that the choice sat in one place so Phase 3
would change one site. **The claim that it stayed in one place did not**, and 3.3's
review found the state machine pricing an assignment, a call-away and a forced
close from the deliverable where D-W17 says the multiplier governs the cash. A
guard holds it now, because a claim about the codebase asserted nowhere is what
this phase demonstrated twice.

The risk row named three fractions and took four keys, because every cap is a
fraction of an account value that no key, column or table held. That is the
shape to expect from an obligation written before the thing that consumes it:
it names what was missing from the checkpoint that deferred it, not what the
checkpoint that claims it will find.

The two 2.3 raised are both consequences of building rather than of planning.
How a set of gate reasons reaches one nullable column is Phase 4's, and how
configuration resolves for a simulated date preceding the value being written
is Phase 9's, which the seed's wall-clock `set_at` makes reachable on the first
walk-forward.

## Working rules in force

- Commit subjects are prefixed with the phase and stage, as
  `Phase 2 / 2.2 - <type>: <subject>`.
- The pull request description describes the change as it stands rather than
  accumulating a section per review round.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.
- A checkpoint's pull request is merged as a merge commit, never squashed.
- Fixture names are not enumerated in checkpoint detail [`CLAUDE.md` §5, as
  narrowed at v1.31.0]. A prompt may name them, being spent and archived.

### Sign-off is three acts, and each produces an artefact someone can read

**A rule recorded is indistinguishable from a rule performed until someone reads
the artefact it should have produced.** That is why these are here rather than in
`Lessons that transfer`: a lesson is read when someone goes looking, and these
have to happen whether or not anyone does. All three failed at least once, and in
each case the work they govern was done carefully while the act itself was
skipped.

- **The marker sweep is run, not recalled.** Every `Build state:` line is read
  off disk and checked against what shipped. It produces the markers. 3.1
  recorded the practice two commits before signing off and did not perform it;
  3.3's second run at merge found four figures the first could not have seen,
  which is why it runs again if the branch moved. **A sweep that greps for the
  marker string reaches only what carries one**, and `PROGRESS.md`'s Current state
  block does not: it was stale through 4.4's sign-off and through the merge sweep
  after it, and neither missed it, because neither could see it. The sweep reads
  that block by name.
- **Both state blocks are re-measured, not carried.** There are two by design,
  this file's and `PROGRESS.md`'s, and this act named one of them until 4.4, which
  is the whole reason the other went stale. Between them they are the only
  description of the present, so every count in either comes from a measurement
  taken at sign-off rather than from the previous one adjusted. It produces the
  description. **A checkpoint that changed no code still changes what is built**,
  which is the sentence that made this fail: "neither changed any code, so every
  section below stands" was true at 3.2 and false from the next commit, and
  nothing re-read it for two checkpoints.
- **[`CLAUDE.md` §11]'s question is asked against every unspent prompt** whenever
  a decision was added, amended or superseded, **and the answer is recorded even
  when it is none.** It produces the answer. Recording a "none" is what
  distinguishes a question asked from one skipped, which is the whole of why the
  omission across thirteen decisions and three amendments left no trace.

## Lessons that transfer

Carried forward because each cost something to learn and none is visible from
the artefact it produced.

- **A passing mutation has three causes, and the technique only works if you can
  tell them apart.** One checkpoint found each, and the third is what completes
  it.

  **A weak test** [2.2]. A suite observed to pass is not a suite shown able to
  fail: reintroduce the defect the test was written for and watch it fail, as
  five of ten and four of four did there.

  **An insufficient mutation** [2.3]. A mutation confined to one site is not a
  mutation of the behaviour. Defaulting either half of the bound resolution
  passed every test, because each half left the other still raising, and only
  defeating both showed the two tests that assert D-W37. Told apart from the
  first by defeating every site the behaviour has rather than one of them.

  **An unfalsifiable suite** [2.4]. Two paths can agree at the seeded data, so a
  mutation swapping one for the other is invisible however sound the test and
  however complete the mutation. The assignment limit reading the total cap's
  fraction passed all 490 tests because both fractions are 0.60. The fix is
  neither a better test nor a better mutation: it is an assertion at data the
  store does not hold and could.

  **The tell for the third is specific**: it appears wherever the corpus seeds
  two keys to one value, and this corpus does that deliberately more than once,
  `Gate:MaxDelta` and `Policy:Random:DeltaMax` being the other pair. Where two
  values coincide by choice, the choice is also hiding a binding, and the
  coincidence is the thing to go looking at.
- **A definition of done that describes a state rather than an act usually names
  a later checkpoint's subject.** Three instances now: 0.6's, 2.1's, and 2.4's
  requiring a cap to be evaluated against committed capital "as the store
  records it" when nothing persists until Phase 4. The tell is grammatical
  rather than technical, and the repair is to state the property the clause was
  protecting, which in each case survives the checkpoint boundary the state
  does not.
- **A check can be vacuous in two directions and neither implies the other.** A
  cap tested against an empty portfolio passes whether or not it works, which is
  1.1's empty-table shape. A cap whose bound is never reached also passes
  whether or not it is wired, and that one hides inside a document that looks
  like thorough coverage: WORKED_EXAMPLE §1 derives two headrooms and only one
  reaches §3, so a total cap reading the wrong exposure reproduces §3 exactly.
  Both directions need their own case.
- **Enumerate by class, not by the shape of the instance in front of you, then
  let a compiler or an assertion confirm the count.** Four forms in one
  checkpoint. A grep for `§3` found three of four unqualified section
  references and missed `[§5]`, which is a different shape and the same class.
  A grep for `new SyntheticChain(` found three of four construction sites and
  the compiler found the fourth, a target-typed `new(`. A consumer-column edit
  matched nine rows where six were meant, and an assertion on the count caught
  it before the write. A single-site mutation was masked by a sibling path. The
  cheap protection is not a better grep; it is arranging for something that
  counts to fail.
- **A test can pass for want of data rather than for the reason it names.** A
  membership fixture over a symbol with no chain, or a right filter over a
  chain of one right, passes without exercising anything. Every case needs the
  data its subject would have used had the subject been wrong.
- **Measure the query before choosing the form.** Holding one SQL text
  literally constant looked strictly better than composing it and cost the
  index seek. `EXPLAIN QUERY PLAN` decided it in one run; reasoning had
  reached the wrong answer.
- **A structural property and evidence for it are different claims.** "The two
  reads cannot disagree" is a property of one definition serving both; "they
  agree across these cases" is a sweep. Keep the sweep, but do not let it
  stand in for the structure.
- **A rule ignored in half its scope is usually half wrong.** §5 forbade
  naming fixtures in prompts and in checkpoint detail; every prompt in this
  project named them anyway, including the supplied ones. The reliable reading
  was that the prompt half was wrong, not that every prompt was.
- **A market rule governs the clearing layer and the account layer is
  convention** [3.1]. A decision spanning both cites two authorities or states
  that it has one, and saying which is the disclosure rather than a caveat. Four
  of 3.1's seven mechanics needed it: the one-cent exercise threshold runs
  between OCC and its clearing members and does not bind an account; OCC assigns
  an exercise notice to a member and the member's own fixed procedures choose the
  writer; a settlement cycle is a rule and when a broker releases proceeds to
  trade against is house policy; and entitlement to a dividend is a rule while
  whether the lab records one is nobody's. One had no authority at any layer,
  because the act it models is a holder's choice, and that is the only case with
  no limit to disclose against. And one rests on a rule that deliberately omits
  its own method, SR-OCC-95-16 having removed random selection from Rule 803 in
  1995 and put the procedures outside it, so the gap was designed rather than
  missed. The failure this prevents is not a wrong citation but a right one
  carrying more weight than it can hold.
- **A quotation is not evidence until its position in the document is
  established** [3.1]. The footnote that appeared to settle which quantity
  committed capital uses sits in the Background of the filing that retired the
  method it describes, and reading it alone gives the opposite of the answer.
  That is this corpus's citation rule one level deeper than it had been applied:
  not only does the cited source have to state the property, the cited sentence
  has to be the document speaking rather than the document quoting what it is
  about to replace.
- **A checkpoint that writes no code can still change the schema** [3.1].
  `ledger_entries` gained `known_on` because [D-W35] makes the trial tables
  projections and a projection cannot carry what its source lacks. The column
  was deduced from a decision rather than from a rule, and landing it while the
  table did not yet exist cost one line where landing it after the transitions
  would have cost the transitions. The check that finds these is to ask, of every
  decision, whether a rebuild from the ledger reproduces what it requires.

---

# Prompts

## 4.1 The record's grain, settled before the tables

Branch `phase-4/checkpoint-4.1` off `main`. Not code. The grains that determine
what this phase's tables are, settled before any of them exists, on the ordering
Phases 1, 2 and 3 all used: a precondition answered late is a schema built twice.

### The obligation describes a generator that no longer runs

The row asking for one feasible set per name and date was raised at v1.17.0.
`EnumerateFor` did not take a position state then and `GateFor` did not take a
book. **Settle the key from what the generator reads now, not from what the
obligation says**, and expect the two to differ whenever a precondition has been
carried across phases.

### Three obligations, and the rows say they are two questions

Each of the first two ends by naming the other as the one that settles its grain,
and the third ends by naming a fourth row as the same question from the other
side. Read the rows before deciding how many decisions a checkpoint owes.

### Constraints

- **A citation is verified by reading what it says, and a decision is read to
  find out what it requires rather than to confirm what you expect it to.**
  Reading [D-W4] to settle a storage key is what found its test line asserting
  unconditionally what its fixture has always tested conditionally.
- **An amended line is swept for wherever it is repeated**, and the copies do not
  resolve alike. Five places carried the same unconditional wording: one was
  unaffected on its own argument, one was a live gap in an unspent prompt, one
  was a registry cell, one was a design sentence that stood and needed its terms
  named, and one was the line itself.
- **A probe that sets what it then reads measures the probe.** One written to
  find whether foreign keys are enforced set the pragma first and reported the
  value it had written, and three claims rested on it before a reading through
  the real factory contradicted all three. Read through the composition the
  application uses, not through one a test wrote.
- **Say which part of a schema a decision determines and which part is
  invention.** A decision settles a grain and a split; table and column names are
  unavoidable to state a schema at all and are nobody's decision. Flagging which
  is which puts the judgement where someone can disagree with it.
- **A constraint the schema holds and one a test checks are different things**,
  and the keys the first binds against are stated where the schema is stated
  rather than left for the checkpoint that builds it to discover.

## 4.2 The decision record

Branch `phase-4/checkpoint-4.2` off `main`. The primary artefact of the system,
and the tables Phase 3 left unbuilt. Five of them after 4.1 settled the grains,
with a writer, a reader, and the definition of done resting on what the reader
does not touch.

### The definition of done is about the barred set

A record rebuilding is nearly vacuous on its own: a reader free to consult live
quotes and current configuration would rebuild something on any schema at all.
**So the read path is its own type in its own file**, because a scan over one
holding the writer too would see the inserts and prove nothing, and the check
names what is barred with the reason for each rather than only listing what is
permitted.

### An append-only reference is inside a record and a projection is not

`contracts` is read by the re-scoring path and `trials` is not, and the
distinction is rewritability rather than convenience. A corporate action mints a
new identity rather than editing a row [D-W36], so that table returns what stood
then; a projection returns whatever it was last rebuilt to [D-W35].

### Constraints

- **A detector reads whatever it is handed as the language it scans, and this
  store hands it English.** Third instance: a regex would truncate a statement
  whose message holds a dash pair, 3.3 found migration comments parsing as table
  aliases, and 4.2 found trigger messages doing it from inside string literals.
  Strip what is data before scanning, and restore the case that motivated the fix
  to show it passes, because a fix that cannot be demonstrated is a rewording
  with extra steps.
- **A count in a rule is a defect waiting for unrelated work.** The vocabularies
  registry cell said "the one with no CHECK" and there were three. The fix is not
  a better number: the rule says which and why, and a case enumerating the
  declarations says how many.
- **A hand-maintained list needs a case that holds its own coverage.** Adding the
  missing entry repairs today and leaves the mechanism, so the sweep enumerates by
  reflection and a vocabulary in neither list fails. Measure the class before
  writing it: keying on the two method names alone would have reported three gaps
  that are not gaps.
- **A figure borrowed from a document is not a figure this store holds.** The
  per-name headroom is 25,000.00 from the seeded equity, not the 5,100.00
  `WORKED_EXAMPLE.md` §1 derives for its own account, and a fixture reaching for
  the document's number broke on a book that breached nothing.
- **Split a vocabulary's `CHECK` where its column splits.** Each of the two reason
  tables carries its family rather than the whole list, so the schema refuses a
  portfolio verdict written to the shared table instead of leaving the split to
  the writer.

## 4.3 The three makers, on one set

Branch `phase-4/checkpoint-4.3` off `main`. The checkpoint where something
finally chooses. Every checkpoint since 3.5 built a thing a test drives; this
builds the thing that decides, and the worked example specifies all three arms.

### A learner with a rule in code cannot learn

The channel writes to the learner and nothing else, and what a channel can write
is a row. **So a policy living in code is one no channel can change**, and the
learner becomes a second frozen arm with a different band. That is why the
baseline and the learner are one algorithm on two policies rather than two
algorithms, and it is a stronger argument than tidiness.

### An adversarial sweep before the build, not a review after it

Seven agents read the corpus and recomputed the document independently before any
maker was written. **They found two defects in code this branch had already
committed and neither was failing anything.** A checkpoint that reproduces an
authored document is exactly where a green suite is least informative, because
the document is the thing being checked.

### What a document cannot discriminate is a property of the document

§4's baseline band admits one candidate, so its rule never compares two values.
Its seeded draw is index zero for every set size up to fifteen and it enumerates
seven strikes, so **no set it could hold** distinguishes a uniform draw from
taking the first candidate. That is a stronger claim than "this data does not
discriminate" and it is the one worth measuring.

### Constraints

- **A count in a rule is a defect waiting for unrelated work**, and this
  checkpoint removed the seventh and eighth. The tell was the same as the six
  before: the number moved when something unrelated landed.
- **A pin that agrees with an unmeasured placeholder proves nothing.** A draw
  pinned across three outcomes passed its placeholder on the first run, which is
  a one-in-three coincidence. Pin across a range wide enough that agreement is
  evidence.
- **Two lists that must move together will not**, unless something says so where
  the second one is. One decided which bands a ceiling is checked against and the
  other whether the check runs; moving one silently covered nothing.
- **Assert the reason column, not only the answer.** A choice can be right while
  the policy that produced it is wrong, and the strike alone cannot tell. What
  made that possible was correcting a document cell that stated a preference
  where the others state a band.
- **A negative example naming something the roadmap intends to build has an
  expiry date on it.** A case used the learner's key as its example of a band in
  no vocabulary, and closed itself when the learner arrived.

## 4.4 What a maker does with an open trial

Branch `phase-4/checkpoint-4.4` off `main`. Step 1 and step 2 built, not signed
off, so the `Current state` block above still describes the repository as it stood
at v1.46.1 and is re-measured when this checkpoint signs off. The decisions Phase
3 took as a caller's parameter: a maker's session produces one of open, roll,
close or nothing, and only the first of those had ever been specified.

### A rule with no authority is written down before it is built, and amended there

Nothing in the corpus states what triggers a roll. The glossary's sentence is
standard terminology and that file says so; what a roll costs [D-W48], what bounds
it [D-W14] and what it commits [D-W43] are all stated and none of them selects.
D-W54 is the second decision in this corpus to say of itself that no authority
states it, after D-W43. **It was amended before any code rested on it**, gaining
the moneyness condition and the debit condition, which is the order that makes a
rule an authority rather than a description of what got built. A maker acting on
every position at the threshold would never hold one to expiry, so it would never
be assigned and never write a covered call, and the amendment is what keeps the
wheel turning.

### One algorithm, and the sentence that reads two ways at the random control

D-W54 says a maker rolls to what its own policy selects "by the same
highest-credit-in-band rule it uses to open". The two halves agree for the
baseline and the learner and part company at the random control, which opens by
uniform draw and has no highest-credit rule to reuse. **The selection function is
injected**, so each arm rolls by the rule it opens with, and the site says this is
a reading of the decision rather than a quotation of it. Taken the other way the
random control would roll by highest credit while opening by draw, and its rolls
would stop being a control. Recorded because 4.5 composes the makers and would
otherwise inherit the choice without knowing one was made.

### The branch a rebuild exists for had never executed

`CloseKindFor` is the one place a projection needs a value from outside its source,
and telling a bound close from a chosen one is the only reason `TrialBounds`
reaches the rebuild at all. Measured at the parent commit, the three test files
writing a `bought_to_close` or `shares_sold` entry and the three calling `Rebuild`
or `Replay` were disjoint sets. **A green suite was evidence of nothing about the
path**, and the fixture that finally runs it is the one that also severs the
literal every other caller shares with the run.

### Constraints

- **A fixture whose sessions are its subject observes each snapshot on its own
  session.** One ingest stamped after the last session puts every quote beyond the
  as-of cutoff of every earlier one, and a case asserting a maker declined to act
  then passes because its session saw no chain. Assert the feasible set before
  reading a verdict whose expected value is "nothing", because those two are the
  same answer for opposite reasons.
- **Three configuration versions, not two.** Reading current configuration and
  reading what was in force on the session being rebuilt are both excluded by
  name [D-W53], and with one change either wrong answer can coincide with the
  other. One in force at the open, one landing between the open and the close, one
  landing after both.
- **Vary the key that sits in no cross-key invariant.** `Trial:MaxRolls` moves
  alone; `Trial:MaxTrialDays` drags `Gate:MaxDte` into the same write and changes
  what the gate admits alongside what the bound does.
- **A rebuild resolves from the ledger, never from the projection it is about to
  rewrite.** The open date comes from the first entry rather than
  `trials.opened_on`, or the rebuild depends on its own previous output.
- **A caller supplying the constant the run was built from proves nothing.** Six
  call sites still pass the seeded literal to both the machine and the rebuild and
  agree with themselves whatever the resolution does. Severing that is what makes
  agreement evidence.
- **A number a decision states is a constant, not a config key.** Seven days is in
  D-W54 rather than named by it, and a `Policy:` row would hand the learner the
  trigger for trial duration. `WheelStateMachine.ExerciseByExceptionThreshold` is
  the precedent: a value that moved would be a rule change, recorded by amending
  the decision.
- **Two closes of one position must cost the same.** They do not: a forced close
  writes no commission entry and a chosen one does. Raised rather than fixed,
  because the amounts are recorded and changing them is a decision.
