# Phase 2 Candidate generator and risk gate: spent prompts

Current state below is the whole state of the repository and the only description
of the present. Phase 0's and Phase 1's files hold the state as it stood at each
phase's close and are not corrected further.

One prompt per checkpoint, being the prompt that produces the checkpoint as it now
stands. Corrections found while building are folded back into the checkpoint's
prompt rather than appended as further entries, so replaying the prompts in order
against the corpus reproduces the current state without replaying the mistakes.

One file per phase. **This file stays open past Phase 2's sign-off and closes
when `phase-3.md` opens**, which is the practice `phase-1.md` already records:
that file is frozen at v1.30.0, "when `phase-2.md` opened", which was 2.1's
sign-off rather than Phase 1's close. Closing at sign-off would leave nothing
describing the present between here and 3.1, which is the defect v1.30.0 fixed in
this pair of files. This sentence said sign-off until 2.5 and is corrected to what
the archive does.

---

# Current state

Corpus v1.34.0.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| Phase 1 | complete, 1.1 to 1.5 built and signed off |
| Phase 2 | complete, 2.1 to 2.5 built and signed off |
| CI | green, 503 tests, guards then restore then build then test, on push to `main` and every pull request |

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

**Schema 5.** Migration 1 is `config_rows`, 2 its monotonic `set_at` trigger, 3 the
six market-data tables of §4.1 [1.1], 4 the membership record [1.3], 5 the bars
nullability rebuild [1.4].

## Market data

Six tables: `underlying_bars`, `corporate_actions`, `earnings_calendar`,
`chain_snapshots`, `contracts`, `contract_quotes`. Two of the six carry no key of
their own and have an index instead; three carry `observed_at` in the key, because a
correction appends rather than replaces [D-W8].

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
observation axis. The corporate-action kind vocabulary is `split` only, with
the fuller set and the CHECK question recorded at Phase 3's dividend
obligation.

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

## Configuration

Two sections bound, `Eodhd` and `Storage`, both verified. Six sections deliberately
unbound because `CONFIG_REFERENCE.md` classes them `rows` and a registered options
type is itself a current-value accessor.

Twenty-three of the 24 `rows`-classed keys hold a value at version 1, written by
the `seed` verb. One carries an `Unset` marker and names the phase that owes it,
being `Costs:AssignmentFee` at Phase 3. The store is the authority on what is in
force, not the document.

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
decimal column. `DecimalColumns` holds seventeen names as of 1.1.

Decimals reach `TEXT` columns through `AddStored`, rendering through the refusing
entry point; rounding is a deliberate call visible at a site, never a default
inside the seam. `ConfigWriter` still takes strings, `config_rows.value` being
polymorphic by design.

## Guards and detectors

`guards.ps1` runs before restore, so it reports on a tree where nothing else can. Two
named checks, no exemption mechanism, self-testing on their own samples.

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

503: 307 across forty-one fixtures, and 196 across thirty-one unregistered
suites. The two guards are checks rather than tests and are counted in neither.
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
| FX-SnapshotNeverRewritten | 23 |
| FX-NoRewriteOfAppendOnlyTables | 20 |
| FX-MalformedChainFailsWhole | 18 |
| FX-MoneyRoundTrip | 17 |
| FX-ConfigWriteRefusesInvariantBreach | 16 |
| FX-NoSqlAliases | 15 |
| FX-NoDecimalOrderingInSql | 14 |
| FX-ConfigStoreClassHonoured | 12 |
| FX-TickerDashForm | 12 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-ConfigResolvesAsOf | 6 |
| FX-EveryConfigSectionBinds | 6 |
| FX-MigrateFromEmpty | 6 |
| FX-EarningsClearanceRejects | 5 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-EveryPolicyBandIsChecked | 5 |
| FX-RegistryMatchesDisk | 5 |
| FX-TotalCapRejectsAboveHeadroom | 5 |
| FX-AssignmentStressRejects | 5 |
| FX-ChainLoadsInIdentityOrder | 4 |
| FX-CrossedQuoteRejected | 4 |
| FX-DeltaCeilingRejects | 4 |
| FX-GateRecordsAllReasons | 4 |
| FX-GateRejectsAboveHeadroom | 4 |
| FX-GrossBasisBindsCallStrike | 4 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-OffWatchlistRejected | 4 |
| FX-WorkedExampleChainLoads | 4 |
| FX-WorkedExampleEnumerates | 4 |
| FX-WorkedExampleGateVerdicts | 4 |
| FX-ApiCannotWrite | 3 |
| FX-CorporateActionMintsSuccessor | 3 |
| FX-EveryAppKeyBinds | 3 |
| FX-NoCurrentConfigReadOnSimulatedPath | 3 |
| FX-PitMembershipExcludesLaterJoiner | 3 |
| FX-SnapshotRestoresIdentically | 3 |
| FX-WorkedExampleChainPersists | 3 |
| FX-DteWindowRejects | 2 |
| FX-PremiumFloorRejects | 2 |
| FX-SpreadCapRejects | 2 |

The suite parses `CONFIG_REFERENCE.md`, `FIXTURES.md`, `DATA_AND_SCHEMA.md`,
`WORKED_EXAMPLE.md` and `guards.ps1`, so all five are load-bearing rather than
descriptive. **Nothing parses §4.1 as a schema**, which is why the schema document
and the migration diverged in six places at 1.1 and no check caught it.

Every store test creates its own database in a temp directory, because the
append-only triggers make the tables impossible to clean between cases.

## Not built

Every table beyond the nine that exist. `decisions` and `candidates` are
Phase 4's.

No `FeasibleSet` type, and that is a choice rather than a gap. `GateFor`
returning every candidate with its reasons in identity order is assembly,
ordering and the refusal record; a type justified only by a later consumer is
speculation, and no maker exists until Phase 4. The set's grain is (symbol,
date) and Phase 4's obligation carries it, so that phase models the set with
`candidates` in front of it rather than inheriting a shape guessed early.

No book to gate against. `BookState` is a parameter and nothing computes one:
`positions` is Phase 3's, so every caller states its own exposure and basis, and
the backward edge SYSTEM_DESIGN §3.3 names as the only one in the daily path is
a parameter rather than a read.

Nothing persists a candidate or its reasons. `GatedCandidate` is returned and
dropped; `decisions` and `candidates` are Phase 4's, and how a set of reasons
reaches one nullable column is a carried obligation.

Rolling has no rule, so `short_put` and `short_call` enumerate nothing. D-W14
permits rolling and bounds it; which contracts a roll offers is Phase 3's and
unwritten.

`corporate_actions` has its first writer, the mint, and no as-of read: 1.5
reaches a predecessor through `ContractLineage`, which is timeless, and nothing
wants the actions in force at a date yet. `earnings_calendar` got both its
writer and its read at 2.3, when the clearance constraint became its first
consumer.

No operator entry point ingests a chain. `ChainWriter`'s only callers are tests
until Phase 8's vendor ingest needs a verb, and a verb nothing calls is
speculation.

Nothing runs, so nothing produces output. Determinism is asserted over stored rows.

## Owed

Work deferred out of a checkpoint is registered in `BUILD_PLAN.md` carried
obligations, which is where planning for the phase that owns it will look. It is not
copied here: two registers of one list is how an obligation comes to exist in the one
nobody reads.

Entries stand against Phase 3, 4, 8, 9 and 11. The count is not restated
here. **Phase 2 owes nothing.** 2.1 discharged the reconciliation row raised at
v1.6.0, the table's oldest and open for twenty-three corpus versions, 2.3
discharged the crossed-quote row while opening two of its own, and 2.4
discharged the risk row while opening one of its own. All three discharged rows
were preconditions rather than work items, which is what put them at the front
of the phase.

2.4's row is what a covered call commits, owed at Phase 3. D-W17 fixes a trial's
committed capital at open, so a call written against shares already assigned may
commit nothing new, while 2.4 charges the candidate's own figure regardless of
right. That is the conservative reading and binds a cap that may not apply, and
no fixture reaches it because no covered call is gated before the state machine
exists.

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

---

# Prompts

## 2.1 The worked example, reconciled

Read `CLAUDE.md`, `BUILD_PLAN.md` §2.1 and the carried obligations,
`WORKED_EXAMPLE.md` in full, D-W4, D-W5, D-W10, D-W12, D-W22 to D-W25,
`CONFIG_REFERENCE.md`'s seeded Gate and Risk rows, FX-WorkedExampleChainLoads,
FX-WorkedExampleChainPersists, `WorkedExampleOracle`,
`synthetic/worked-example.json`, and Current state above. No production code.

### The invariant, and why the rewrite is small

- **Establish this before changing anything, by reading rather than by trusting
  it**: every figure in §4 through §7 derives from a bid, because fills are at
  the bid [D-W12], and the only figure derived from an ask is the spread ratio
  the gate reads. Read each section in full; a search for a column name answers
  a different question.
- So the rewrite changes asks and adds rejected candidates, and changes nothing
  else. Not the symbol, the snapshot date or the expiry; not any bid on any
  strike; not the strikes 45.00, 47.50 and 50.00, which are the feasible set §4,
  §6 and §7 depend on, including FX-ThreeMakersSameFeasibleSet's expected set;
  not any delta on those three; nothing in §4, §5, §6 or §7.
- Report what the check found, not that it was run. An §4-onward figure reading
  an ask is a finding and changes the shape of this checkpoint.

### What the revised chain must demonstrate

The chain teaches the gate, which means each constraint this snapshot can
demonstrate has a candidate demonstrating it, with obvious margins rather than
hundredths.

- The spread cap [D-W22]: the three feasible strikes pass comfortably, and one
  added candidate fails it and nothing else.
- The premium floor [D-W22]: one added candidate fails it and nothing else.
- The delta ceiling [D-W23]: 52.50 and 55.00 already fail it at 0.44 and 0.62
  against 0.35, and already fail the per-name cap. Keep both, because a
  candidate failing two constraints is what FX-GateRecordsAllReasons exists for
  and the document should show one.
- Added candidates extend the strike ladder downward at its existing 2.50
  spacing rather than inserting off-ladder strikes.
- **Expect the floor to constrain the added candidates**, since 45.00's bid is
  the floor exactly and its bid cannot move. A spread-cap-only failure below it
  is possible on one bid alone, and the geometry that results is the stale
  untransactable quote D-W22 describes, so state it in §3 as deliberate rather
  than letting it read as an accident.
- Constraints this snapshot cannot demonstrate, stated in the document rather
  than left as a gap: the expiry window [D-W24] and earnings clearance [D-W25],
  because one snapshot date with one expiry cannot show a window and no report
  date exists in this example. Say so in §3 and say which checkpoint's fixtures
  cover them instead.
- Compute every ratio and report the arithmetic. Carry no figure from this
  prompt: none is supplied, deliberately, because the numbers are the thing to
  get right and this project's arithmetic errors have all come from
  transcription rather than from calculation.

### The three artefacts move together

§2's table, §3's gate table and `synthetic/worked-example.json` state one chain.
All three change in one commit or the oracle disagrees with itself.

- §3's gate table gains a column per constraint, or a reason column naming every
  constraint each candidate failed, so the table shows the gate's whole verdict
  rather than the capital cap alone. It shows one constraint today and the
  banner names three more.
- The JSON gains the new quotes and the revised asks.
- Both pinning fixtures pass unchanged in code: the oracle helper parses §2 and
  the fixtures compare, so a revision needing a fixture edit has changed
  something the tests were holding.

### The delta the gate compares, and the deferral it settles

- §3's table carries magnitudes where §2 carries the sign, and the document says
  why: the ceiling compares absolute delta [D-W23]. One sentence, and it removes
  the only place in the revision where one quantity appears two ways without
  explanation.
- `ContractQuote.cs` says whether the ceiling compares magnitude is Phase 2's to
  settle [D-W23], and D-W23 said absolute delta from the day it was written. The
  question was answered by the decision the comment cites, in the sentence it
  cites it for. Correct the comment: the ceiling compares absolute delta, and
  the loader still carries the sign the chain states, which is the part that was
  genuinely a loader question. Enumerate the reach from the work rather than
  from this clause; the same deferral is elsewhere.
- It is a comment in `src/`, so the commit touching it is code-adjacent rather
  than docs-only. Say so rather than letting a docs-only claim carry a `.cs`
  edit unremarked.

### The banner and the obligation

- §3's banner comes down, replaced with nothing: a resolved conflict needs no
  monument, and CHANGELOG carries the history.
- Remove the Phase 2 reconciliation row from carried obligations, raised at
  v1.6.0 and open since. Report the count before and after.
- CHANGELOG, under Fixed: the example was written before the contract-level gate
  existed and taught a three-candidate feasible set the gate would render as
  two. Reconciled by revising the quoted markets so the feasible set survives,
  and by adding candidates that fail the constraints the example never
  demonstrated. No bid changed, so nothing downstream of the fill moved.

### Definitions of done

- 2.1 registers no checks and says so in its detail, per rule 2's clause. It
  changes a document two existing fixtures already pin, which is the coverage.
- No table, no decimal column, no config key: each checked and reported empty.
- The seeded Gate values are read from `CONFIG_REFERENCE.md` rather than
  assumed: if any margin depends on a value that is proposed rather than seeded,
  say which.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit files
with the file tools rather than a shell round trip, which mangles UTF-8 outside
ASCII. Reconcile the detail and the archive at sign-off, not during the build.

---

## 2.2 Enumeration and membership

Read `CLAUDE.md`, `BUILD_PLAN.md` 2.2 through 2.5 and the carried obligations,
`SYSTEM_DESIGN.md` §3.3 and §3.4, `DATA_AND_SCHEMA.md` §4.3's `candidates` and
`positions`, D-W3, D-W4, D-W5, D-W9, D-W10, D-W12, D-W14, D-W16, D-W17,
`WORKED_EXAMPLE.md` §2 and §3 as reconciled at 2.1, `AsOfMembership`,
`AsOfMarketData`, `ContractIdentity`, the fixtures registered against 2.2 in
`FIXTURES.md`, and Current state above. The first Phase 2 checkpoint that
writes code.

### Three decisions, settled before the code

2.2's detail says one sentence about what a candidate is. These three settle
what it leaves open, and each is a judgement rather than a transcription, so
argue it and record the argument in the detail.

- **How far a candidate is built here, and what waits for 2.4.** §4.3's
  `candidates` carries `contracts_qty`, `committed_capital`, `credit` and
  `feature_json`, and none of 2.3's four constraints needs any of them: the
  spread cap, premium floor and delta ceiling read the quote, and the expiry
  window reads a date. Only 2.4's capital caps need committed capital, and the
  quantity that computes it is the open Phase 3 metric question. Decide what
  2.2's candidate carries and report the reasoning; building economics here
  means choosing between the multiplier and the deliverable at the checkpoint
  that has no reason to. Whatever you choose, the type is not the `candidates`
  row: that table is Phase 4's and nothing persists at 2.2.
- **The simulated date is used on both axes, and that is a choice.** A read
  needs a snapshot date and an as-of instant, and 1.2 made them independent
  deliberately. On a simulated date D the generator wants the chain for D as
  known at D, so it passes D twice. State it in the detail and at the call site
  rather than letting the same variable land in two parameters unremarked. The
  two axes exist because they can differ; collapsing them is correct for a
  simulated run and would be wrong for a backfill. Membership resolves on the
  same pair, so one date reaches four parameters across two reads.
- **Enumeration is deliberately broad, and the detail should say why.** A deep
  in-the-money put is sellable and will be rejected by every constraint.
  Enumerating it anyway is what makes the gate's effect auditable [D-W5,
  D-W10], and 2.1's own §3 demonstrates it: seven strikes enumerated, three
  feasible, and the four rejections are the lesson. Say in the detail that
  enumeration filters on nothing but position state and membership. A generator
  that pre-filters produces a smaller enumerated set and a smaller record of
  what the gate did, which is the property Phase 4's decision record exists to
  hold.

### Position state, without the table

`positions` is §4.3 and unbuilt. 2.2 needs the concept and not the row.

- A `PositionState` in `Core` with the four tags §4.3 names, rendered through a
  declared `Store*` form now rather than when the table lands, on the
  `StoreOptionRight` precedent. Declared, not derived from the enum's spelling.
- Report what each state makes sellable and on whose authority. Cash to puts
  and shares to calls are the wheel [D-W16]; what a `short_put` or `short_call`
  state enumerates is a rolling question [D-W14] whose rules are Phase 3's, so
  2.2 enumerates nothing for them unless you can cite something that says
  otherwise. Say which you did.

### The generator's enumeration half

In `Core`, one type. Takes a symbol, a simulated date, a position state; asks
membership, then the chain; returns candidates in `ContractIdentity` order.

- **Test**: a symbol that was not a member at the simulated date enumerates
  nothing, even with a chain present for it. Both halves matter, since a
  chain-less symbol would pass for the wrong reason.
- **Test**: a symbol that joined after the simulated date is not a member at
  it, which is 1.3's point-in-time membership fixture reaching the generator
  rather than being restated.
- **Test**: the same inputs enumerate the same candidates in the same order,
  twice, which is 2.2's own definition of done and the first consumer of 1.5's
  five-component total order.
- **Test**: enumeration is a pure function of its three inputs. State how you
  asserted purity rather than asserting it in prose.

**The two membership reads must not be able to disagree.** A per-symbol read
and the set read agreeing across chosen cases is evidence; "cannot differ" is a
property of one SQL definition serving both. Make it structural: one text with
an optional symbol predicate, or the set read expressed through the per-symbol
one, so a change to the ranking reaches both by construction rather than by two
edits. If you keep two statements of the query, say so plainly and drop the
"cannot resolve differently" claim, because two copies of one query drift the
way two copies of one fact do and 1.5 removed `Contract.DeliverableShares` for
exactly that reason. Keep the agreement tests either way; they are the tripwire
if the structural choice is ever undone. Report which you did and what it cost.

**Nothing asserts that a shares position enumerates calls.** The only chain in
the repository is all puts, so an enumerator that ignored state entirely, or
filtered to puts unconditionally, passes every test above.

- A two-right chain built inline in the generator's own suite rather than as a
  new file under `synthetic/`: that directory holds hand-written chains the
  corpus refers to [D-W31], and this is test scaffolding for one suite.
- **Test**: on one chain carrying both rights, cash enumerates the puts and no
  call, and a shares position enumerates the calls and no put. Assert both
  directions on the same chain, so neither passes for want of data.
- **Test**: the two short legs enumerate nothing on that same chain, which is
  the searched-and-empty finding asserted rather than only reported. That one
  is worth having precisely because it will fail the day Phase 3 gives rolling
  a rule, which is the right moment to be told.
- Report whether the state actually reached the filter on first run. An
  enumerator that ignores its third parameter is the specific defect this
  closes, and the interesting answer is if it passed before the fix.

### The worked example is the enumeration oracle too

§2 carries seven strikes and §3 says all seven are enumerated with three
feasible. 2.2 owns the first half of that sentence and can prove it.

- **Test**: enumerating the worked example's chain in cash yields exactly the
  seven strikes §2's table states, parsed from the document through
  `WorkedExampleOracle` rather than restated.
- This is the third fixture pinning that document and the first to read §3's
  claim rather than §2's data. If §3 and the enumerator ever disagree, one of
  them is wrong and the suite says which.
- Its registry row is supplied with the prompt, so the fixture and its
  registration land together and both enforcement directions hold from the
  commit it arrives in. The marker moves in that same commit, counted from
  disk, when the count is true.

### Definitions of done

- Every check registered against 2.2 exists in its kind; the marker counted
  from disk, not transcribed.
- No table, no decimal column, no config key: each checked and reported empty,
  or reported with what it added.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit files
with the file tools rather than a shell round trip, which mangles UTF-8 outside
ASCII. Reconcile the detail and the archive at sign-off, not during the build.

---

## 2.3 The contract constraints

Read `CLAUDE.md`, `BUILD_PLAN.md` 2.3 through 2.5 and the carried obligations,
D-W5, D-W10, D-W12, D-W22 to D-W26, `DATA_AND_SCHEMA.md` §4.1's
`earnings_calendar` and §4.3's `candidates`, `WORKED_EXAMPLE.md` §2 and §3,
`CandidateGenerator`, `AsOfMarketData`, `AsOfConfiguration`,
`SyntheticChainReader`, `ChainWriter`, the fixtures registered against 2.3 in
`FIXTURES.md`, and Current state above.

### What the detail does not name

- **Earnings clearance is three builds, not one constraint.**
  `earnings_calendar` exists from migration 3, nothing reads it, nothing writes
  it, and `SyntheticChain` is symbol, bars and quotes, so no fixture can put a
  report date in a store. Correct 2.3's detail to say so: the constraint needs a
  synthetic format that can express a report date, a writer, and an as-of read
  before it is testable. Report the shape chosen for each. The format extension
  is a change to a hand-written file's schema [D-W31], so the report says what
  it costs a reader of the format. Splitting earnings out or deferring it to 2.5
  is worth weighing rather than assuming; the registry puts its check here and
  the constraint is the reason the table exists, so say why if you keep it.
- **Settle the crossed-quote obligation and record the decision.** Either a
  crossed quote is not a market and the loader keeps refusing it, so the gate
  never sees one, or it is a real vendor artefact and the gate rejects it, so
  the loader must stop refusing it. The argument has to reach D-W22's first
  rationale: the filter protects the measurement, so what matters is whether an
  untransactable quote can reach the scorer and present as the best available.
  If the loader stops refusing, say what changes in `SyntheticChainReader` and
  what the format then admits; if it keeps refusing, say what the gate does when
  Phase 8's vendor data delivers one anyway, since the loader does not stand
  between the vendor and the store. Close the obligation row either way and
  report the count.
- **The reason vocabulary is 2.3's, and 2.5 depends on it.** Every fixture here
  records a reason, `candidates` carries `gate_status` and `gate_reason`, and
  2.5 asserts that a candidate failing two constraints carries both. Nothing
  declares what a reason is. Declare it, stored form and all, on the
  `StoreOptionRight` precedent: declared not derived, refusing an unrecognised
  value. A candidate carries a set rather than one, and that shape is decided
  here even though 2.5 asserts it.

**Every reason must name a decision that states its ground.** A gate that
rejects on a ground its cited decision does not state is this corpus's citation
pattern, and creating one is worse than inheriting one. Check each reason
against the operative paragraph of the decision behind it before writing the
code, and report the sweep.

### The constraints

- Each reads its bound from configuration as of the simulated date [D-W26],
  never from a constant. **Test**: a constraint evaluated at a simulated date
  before a later config version uses the earlier bound.
- An unresolvable bound stops the evaluation naming the key and the date
  [D-W37], and bounds resolve once per evaluation rather than once per
  candidate. **Test**: a constraint evaluated at a date before its bound was
  written fails naming the key and the date, rather than admitting or
  rejecting. Report the implementation and the message, not the decision.
- The spread cap and the premium floor [D-W22], both rejections independent and
  both recorded. The delta ceiling [D-W23], comparing absolute delta, since the
  sign is where that constraint goes wrong: a put at -0.44 rejects against a
  0.35 ceiling and one at -0.24 does not. The expiry window [D-W24], both sides,
  since a one-sided test passes on a constraint that only checks one.
- Earnings clearance [D-W25], buffered both sides. **Test** three cases: a life
  containing a report date, a life clearing the buffer, and a life clearing the
  report by less than the buffer, which is the case an unbuffered filter would
  pass. **The admit case ingests a report date that exists and sits outside the
  buffer, never an absent one**, because an absent array means no reports and
  the constraint would never run. Report the dates chosen and their distances
  from the buffer edge, and say what the other fixtures do about earnings.
- Every boundary comes from the decision that states it. Where a decision does
  not state its edge, that is a finding before it is a choice: report it and
  have it stated rather than picking one.

### The mutation check

For each constraint, show the suite fails when the constraint is defeated
rather than only that it passes. A constraint returning admit unconditionally
should fail its own fixtures. Report which mutations ran and what failed; if any
mutation passes everything, that fixture is not testing what it claims, **or the
mutation did not reach the whole behaviour**, and the two are told apart by
mutating every site the behaviour has rather than one of them.

### The worked example, fourth pin

§3's table now states a verdict and reasons per strike, and 2.2 pinned only the
enumerated strikes.

- **Test**: gating the worked example's chain at its snapshot date produces
  §3's verdicts, parsed through `WorkedExampleOracle` rather than restated. Only
  the four contract constraints; the per-name cap is 2.4's, so the two strikes
  failing on both carry the delta reason here and gain the capital reason at
  2.4. Strip it by name against a declared phrase rather than by discarding what
  does not match, so a reason this checkpoint should have produced cannot vanish
  into the gap.
- Assert the phrase-to-reason mapping total over the vocabulary, so a reason
  with no phrase fails rather than narrowing every expected verdict silently.
- Report whether §3 held on first run. That is the point of the document being
  the oracle, and 2.1 wrote its verdicts before any gate existed.

### Definitions of done

- Every check registered against 2.3 exists in its kind; the marker counted from
  disk, not transcribed.
- Report what 2.3 adds to `AppendOnlyTables`, `DecimalColumns`, `ConfigKeys` and
  `CONFIG_REFERENCE.md`, or that each is checked and empty. A key the code reads
  is named in `ConfigKeys` rather than left a literal, and a bound the gate reads
  makes its Consumer column verified rather than assumed.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit files
with the file tools rather than a shell round trip, which mangles UTF-8 outside
ASCII. Reconcile the detail and the archive at sign-off, not during the build.

---

## 2.4 The portfolio constraints

Read `CLAUDE.md`, `BUILD_PLAN.md` 2.4 and 2.5 and the carried obligations,
D-W5, D-W10, D-W11, D-W17, D-W19, D-W26, D-W34, D-W37, `CONFIG_REFERENCE.md`'s
Risk rows and the paragraph above them, `WORKED_EXAMPLE.md` §1 and §3,
`GateBounds`, `GateReason`, `GatedCandidate`, `ContractConstraints`,
`EnumeratedCandidate`, the fixtures registered against 2.4 in `FIXTURES.md`,
and Current state above.

2.4 extends the shapes 2.3 built rather than inventing any. Bounds resolve once
per evaluation into a record of numbers, so a constraint is handed values and
cannot reach configuration [D-W37]. Reasons are enum members from 1 and a
candidate carries a set. Follow all three.

### Equity does not exist and every cap divides by it

Verified before the checkpoint: no key, no column and no table holds an account
value, and §1 states 100,000 in prose that nothing reads. So the obligation is
four keys rather than the three it names.

It is a configuration key rather than a derived figure, and D-W11's own
rationale is the argument: the caps are structural because the sample cannot
price the tail, and a denominator computed from the run's own state moves with
the run, so a drawdown would loosen every cap at the moment it should bind.
`rows`-classed under D-W27, a cap being read while producing a simulated
decision.

### The values, and which of them are decisions

`Risk:Equity` 100000.00, `Risk:PerNameCapFraction` 0.25 and
`Risk:TotalCapFraction` 0.60 are transcribed from §1, which states all three and
derives §3's 5,100.00 headroom from them.
`Risk:SimultaneousAssignmentLimitFraction` 0.60 is chosen, no document stating
it, and its reason is arithmetic: a cash-secured put's committed capital is its
assignment exposure, so any value below the total is unreachable and any above
it never binds. The relationship changes at Phase 3, when a covered call commits
shares rather than cash.

Each row's Notes state which of the two it is. Calling the transcribed ones
proposals inverts 0.8's distinction, whose argument was about who decides rather
than about which figures they would pick.

**Report what the seeded values make each cap admit**, so the three are
checkable as amounts and not only as ratios.

Do not add an invariant between the total cap and the assignment limit. They
coincide today because every position is a cash-secured put; the relationship
changes at Phase 3 rather than being wrong now. The per-name-against-total
invariant is worth weighing on D-W34's precedent and worth reporting either way.

### The bounds record

The four values, resolved once per evaluation, unresolvable stopping with the
key and the date [D-W37]. Report whether it is a second record or a widening of
`GateBounds`, and why: separate reads better if the two families are ever
evaluated apart, one reads better if they never are.

### Committed capital, and the exposure the caps read

`EnumeratedCandidate` declined economics and named 2.4 as the checkpoint needing
them, with the Phase 3 obligation as the reason. Act on that reasoning rather
than restating it: one site computes committed capital, it cites the obligation,
and Phase 3 changes one place.

- Per-name reads the candidate. Total and simultaneous-assignment read open
  exposure, which is `positions` and is Phase 3, so both take it as a parameter
  the way the constraints take bounds.
- **Test**: each portfolio cap rejects at non-zero exposure. A cap tested only
  against an empty portfolio passes whether or not it works, which is 1.1's
  empty-table shape.
- **The opposite vacuity is the one §3 cannot see.** §1 states 19,900.00
  committed in `WDGT` and 38,000.00 across all names and derives both headrooms;
  only the per-name figure reaches §3. Supply both, and the same figure for
  assignment exposure, so the third cap is exercised on a real number rather than
  on zero. Then assert both headrooms as the fixture computes them, because a
  total cap wired to the wrong figure, or not wired at all, reproduces §3
  exactly.

### The reasons and the basis rule

`GateReason` gains members from 7, sequentially, each with the decision it
cites. `StoreGateReason` gains their stored forms, declared not derived.

- D-W19 says "above basis" and the registry says "below gross basis is refused",
  which leaves a strike exactly at basis unsaid. State it in the decision before
  the code rests on it: a strike at basis recovers the outlay exactly, so
  excluding it would forbid the break-even strike for no stated reason.
- The basis rule binds a call against a position's gross basis, so basis is a
  parameter until Phase 3 supplies one.
- **Test**: a strike net basis admits and gross basis rejects is rejected, both
  bases in one test or it shows a rejection rather than the distinction.
- **Test**: a strike exactly at gross basis is admitted.

### Two families on one candidate

2.4 is the first checkpoint where a contract constraint and a cap can fail the
same candidate.

- **Test**: one candidate carrying a reason from each family, in declared order.
  2.5's fixture asserts the property; this asserts it is reachable.
- Mutation check per constraint, mutating the behaviour rather than a site, per
  2.3's correction. Report what you ran and what failed. **A mutation that passes
  everything may mean the seeded values hide the binding rather than that the
  test is weak**: two keys holding the same value make either readable from the
  other. Where that happens, add the assertion at a configuration the store does
  not hold and could, rather than recording the gap.

### The worked example, fifth pin

§3 carries per-name cap verdicts that 2.3 stripped by name.

- **Test**: gating the chain with the caps in place reproduces §3's full
  verdicts, both reasons on the two strikes failing both, nothing stripped.
- Report whether §3 held on first run.

### Definitions of done

- Registered checks exist in their kind; marker from disk.
- `ConfigKeys` and `CONFIG_REFERENCE.md` gain the Risk keys; report whether their
  consumers move from **Unverified** to verified.
- The Risk obligation closes. Report the count.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit files
with the file tools rather than a shell round trip, which mangles UTF-8 outside
ASCII. Reconcile the detail and the archive at sign-off, not during the build.

---

## 2.5 The feasible set

Read `CLAUDE.md`, `BUILD_PLAN.md` 2.5 and the carried obligations, D-W4, D-W5,
D-W10, `SYSTEM_DESIGN.md` §3.3 and §3.4, `DATA_AND_SCHEMA.md` §4.3,
`CandidateGenerator`, `GatedCandidate`, `ContractIdentity`, `AsOfMarketData`,
the fixtures registered against 2.5 in `FIXTURES.md`, and Current state above.

2.5 is the last checkpoint of Phase 2 and closes it. The gate is whole, so this
is a small code checkpoint and a large reconciliation one. Say so rather than
inflating the first half: two of this phase's obligations were discharged by
building something and this one is discharged by asserting what was built.

### What assembly produces is a choice

`GateFor` already returns every candidate with its reasons in identity order,
which is assembly, ordering and the refusal record. Decide whether a
`FeasibleSet` type ships and report the reason, not only the choice. A type
justified only by a later consumer is speculation, which is `GateReason`'s own
ground for declining 2.4's members at 2.3, and 1.5 removed
`Contract.DeliverableShares` for being a second statement made before its
consumer existed. Phase 4's obligation names the set's grain as (symbol, date);
whatever is decided, that row absorbs the reasoning, because Phase 4's planning
reads it and 2.5's detail is about to freeze.

### The definition of done asks for bytes that do not exist

It says the set is ordered "so three makers receiving it receive the same bytes
[D-W4]". Nothing serialises a candidate at 2.5 and `candidates` is Phase 4's.
2.5 is not built, so its detail is live intent: restate it as what this
checkpoint can assert, being that the same inputs produce the same sequence,
compared as an ordered sequence of identities and reasons, and that the ordering
is the identity total order. Carry the byte-level property to the checkpoint that
persists the set, and add to Phase 4's obligation that
`FX-ThreeMakersSameFeasibleSet` is where it lands.

**This is the fourth definition of done whose subject belongs to a later
checkpoint**, after 0.5's, 0.6's and 2.4's. Record it in the CHANGELOG under
Fixed with the class named, and check the earlier list while you are there: a
count of instances that nobody re-reads is how the wrong instance stays named.

### The order is not this checkpoint's to impose

The same clause calls 2.5 the first consumer of the total order 1.5 completed.
Check it before building on it. Assert the property at the gate's output and say
where the order actually comes from, because a second sort would be a second
statement of one guarantee, and `CandidateGenerator`'s own remark already says
enumeration inherits rather than re-imposes.

- **Test**: the gated sequence is in identity order, sorted through the
  identity's own comparison rather than against a hand-written list.
- **Test**: the same inputs gate to the same sequence, identities and reasons.
- **Test**: the gate returns what enumeration returned, in that order, so
  nothing reorders or drops.
- Identity orders on expiry before strike and every chain in this repository is
  one expiry, so a chain of one expiry cannot tell an identity comparison from a
  strike comparison. Supply more than one and scramble the input.

### The registered check

- **Test**: a candidate failing two constraints carries both, which is why the
  gate evaluates every constraint rather than short-circuiting. Two constraints
  in one family, one from each family, and the full set a single evaluation can
  produce, since the registered wording asks for two and two of one kind would
  discharge it while leaving the seam between the families untested.
- Assert the maximum against the vocabulary's own length rather than against a
  number, so a reason added later leaves it unasserted rather than wrong.

### Mutation checks

Per constraint and per property, mutating the behaviour rather than a site.
Include one that should change nothing, being the gate re-sorting its own
output, and say which of the three causes of a passing mutation applies when one
passes; if none does, that is a fourth outcome and worth naming as such.

### The markers, and the sweep they imply

`SYSTEM_DESIGN.md` §3.3 and §3.4 gain the build-state markers `CLAUDE.md` §5
requires. Sweep the rest while you are in there rather than writing two and
leaving the others: a marker is an observation and goes stale silently, and each
correction states what is not built beside what is.

### Definitions of done

- Every check registered against 2.5 exists in its kind; marker from disk.
- Report what 2.5 adds to `ConfigKeys` and `CONFIG_REFERENCE.md`, or that each is
  checked and empty.
- Phase 2's build state moves to complete, with what is not built stated beside
  it. Phase 3's detail is authored content and arrives as a corpus sync.

### Constraints

No `double` or `float`. No ambient clock. Money is decimal in `TEXT`. Edit files
with the file tools rather than a shell round trip, which mangles UTF-8 outside
ASCII. Reconcile the detail and the archive at sign-off, not during the build.
