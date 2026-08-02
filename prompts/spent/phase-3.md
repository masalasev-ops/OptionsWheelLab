# Phase 3 Thin slice, one full wheel turn: spent prompts

Current state below is the whole state of the repository and the only description
of the present. Phase 0's, Phase 1's and Phase 2's files hold the state as it
stood when the next file opened and are not corrected further.

One prompt per checkpoint, being the prompt that produces the checkpoint as it now
stands. Corrections found while building are folded back into the checkpoint's
prompt rather than appended as further entries, so replaying the prompts in order
against the corpus reproduces the current state without replaying the mistakes.

One file per phase. This file opened at 3.1's sign-off, which closed
`phase-2.md`, on the practice that file records: a phase's archive stays open
past its own sign-off and closes when the next one opens, because closing at
sign-off leaves nothing describing the present in between.

---

# Current state

Corpus v1.37.0.

| | |
|---|---|
| Phase 0 | complete and reviewed, 0.1 to 0.8 built and signed off |
| Phase 1 | complete, 1.1 to 1.5 built and signed off |
| Phase 2 | complete, 2.1 to 2.5 built and signed off |
| Phase 3 | 3.1 built and signed off, 3.2 to 3.5 not started |
| CI | green, 503 tests, guards then restore then build then test, on push to `main` and every pull request |

**3.1 changed no code**, so every section below except this table, `Owed` and
`Lessons that transfer` stands as it did at Phase 2's close. The suite is
unchanged at 503 and the guards scan the same 149 files.

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

Entries stand against checkpoints 3.2 to 3.5 and against Phases 4, 8, 9 and 11.
The count is not restated here. **The column now names a checkpoint once the
owning phase's detail exists and a phase otherwise**, stated at the table
because two readings of it disagreed: a count over phase names alone misses the
rows that have moved on. **3.1 owes nothing**, and it closed four rows while
raising three, all three at 3.3. **Phase 2 owes nothing.** 2.1 discharged the reconciliation row raised at
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

## 3.1 The mechanics, settled before the machine

Docs only. New branch `phase-3/checkpoint-3.1` off `main`. No code, and the
suite must come out unchanged. The branch stays open until all seven mechanics
are settled: a half-settled 3.1 on `main` would leave the corpus asserting some
mechanics and silent on the rest with nothing marking the difference.

### The division of labour

Verification is the build's and the decisions are authored, which is the
v1.26.0 obligation's own wording: verify *before the state machine's decisions
are authored*. Where the evidence is in the build's hands and the wording is
not, draft and land for correction rather than wait for supplied prose, and
record in the corpus that the prose is the build's. A reader must be able to
tell which author produced a sentence.

### The house form, which D-W38 sets

Each decision carries the operative statement first; one Source paragraph per
authority, quoting the clause with its release number, file number, Federal
Register citation and retrieval date; a paragraph naming what the authority does
not reach and what the lab models instead; and a Test line naming a fixture
registered at the checkpoint that builds it. 3.1 registers none against itself
and says so, per `FIXTURES.md` rule 2.

### Routes, measured before relying on them

`theocc.com` and `infomemo.theocc.com` return 403 to this environment.
`sec.gov` serves SRO filings to `curl` with a declared agent and 403s WebFetch.
`govinfo.gov` serves GPO's own text of the Federal Register, and
`federalregister.gov`'s JSON API locates documents by date and agency while its
full-text path returns a bot wall. A rule whose text no filing exhibit
reproduces can still be read in the notice that approved it, which is the route
that reaches Rule 805 and Rule 903. Contract adjustment info memos are not
Federal Register documents and have no such fallback.

### The seven, and what each cites

- **Expiry resolution.** One cent in the money against the session's close, from
  Release No. 34-57163, File No. SR-OCC-2007-18, 73 FR 4297, 24 January 2008,
  which amended Rule 805 to reduce the threshold from $.05 to $.01 and states
  the in-the-money test. The Options Industry Council supplies the limit that
  the filing does not: the threshold runs between OCC and its clearing members
  rather than binding an account.
- **When assignment is known.** Determined after the close and known the next
  morning, which is [D-W8] applied to the account. Split by authority: Rule 803
  and its Interpretation .01 for the clearing layer, with SR-OCC-95-16 for why
  the method is deliberately outside the rule; Rule 804 for the account layer,
  where the member's own fixed procedures govern and no rule fixes when a
  customer is told.
- **T+1 cash availability.** Rule 15c6-1(a) for the cycle and OCC Rule 903 for
  the exercise leg, an exercise being a clearing event rather than a purchase or
  sale. When a broker releases proceeds to trade against is house policy.
- **Dividend entitlement.** FINRA Rule 11140(b)(1) as amended fixes the ex-date
  at the record date. Whether a dividend enters the record has no authority and
  the lab decides it: it does, in `ledger_entries` and in the control [D-W13].
- **Early exercise around ex-dividend.** No rule governs whether a holder
  exercises, so the decision cites nothing and says that citing nothing differs
  from citing weakly. The condition is chosen.
- **Committed capital's quantity.** Strike times the multiplier, from Release
  No. 34-54748, File No. SR-OCC-2006-01, 71 FR 67415, 21 November 2006, approved
  by Release No. 34-55258, 72 FR 7701, 16 February 2007. Amend [D-W17] rather
  than adding a decision, and stamp the amendment.
- **What a covered call commits.** Nothing beyond the trial's figure, fixed at
  open. Chosen, with no external source of any kind.

### The two checks that are not about sources

- **Run [D-W35] against every decision.** Can a projection rebuilt from
  `ledger_entries` reproduce what this decision requires? `known_on` falls out
  of that question, not out of any rule. The session calendar does not resolve
  and is raised rather than deduced.
- **State which claims are transcribed and which are chosen before retrieving,
  not after.** That is 2.4's distinction moved from numbers to mechanics, and it
  is what stops the last one arriving dressed as a fact.

### Constraints

- Every citation is checked against retrieved text before it lands, never from
  a search snippet or from recollection. A quotation is not evidence until its
  position in the document is established: the footnote that appears to settle
  the adjustment question sits in the Background describing what the filing
  exists to replace.
- Counts are read off the table they describe, at the moment they are written.
- `CommittedCapital.cs` is not edited. 3.1 is not code, and the correction is
  scheduled at 3.3 rather than remembered.
