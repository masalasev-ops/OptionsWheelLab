# CONFIG_REFERENCE

Every configuration key, its meaning, and its **verified** consumer. Verified
means someone read the composition code and confirmed the binding, not that it
looks like it should bind.

Build state: **partly verified**. A row's Consumer is verified when its
checkpoint lands and is specified-only until then; unverified rows carry
**Unverified**. A row whose consumer cannot be verified once its checkpoint has
landed is a defect, not a documentation gap.

The Consumer column names the component that READS the value, and once verified
also names the type it binds through, as `component via TypeName`. Both are
wanted and they are different facts: the type is where the value enters the
process, the component is what the value does. Naming only the type would lose
the second, and a column meaning one thing on verified rows and another on
unverified ones is not a column.

A key naming a proportion says `Fraction` and carries a fraction, so `0.12` is
twelve percent. The unit is in the name because a consumer comparing a percent
against a computed fraction rejects nothing and reports no error.

Every key carries a **Store** class [D-W27]. `rows` means a config row,
resolved as-of a simulated date [D-W26]. `app` means bound from `appsettings`,
which is not as-of resolvable and is therefore reserved for values that never
participate in a decision.

One key per row. A row naming two keys leaves the second unreadable to
anything parsing this document, and a suffix-only second token is not a key
path at all. Keys that constrain each other say so in their Notes instead.

The reverse direction, a documented key that nothing binds, is not a standing
check **for `rows` keys**. Most of those are documented and deliberately unbound
until their own phase, so a standing assertion would fire on all of them. It is a
definition of done on each checkpoint instead: a checkpoint is not done until
every key its sections introduce is bound and verified. Same shape as the two
directions of `FIXTURES.md` rule 2, and for the same reason.

That reasoning does not reach an `app` key. An `app` key is bound from
`appsettings` by definition, so one that binds to nothing is a key an operator
can set and nothing will read, which is a defect today rather than a future
phase's work. **FX-EveryAppKeyBinds checks that direction**, and it is the mirror
of FX-EveryBoundKeyIsDocumented: one walks the types and checks this document,
the other walks this document and checks the types. Between them the loop closes
for `app` keys.

Config rows are append-only and versioned; current is `MAX(version)` for a key. A revision inserts version + 1 and the old value stays readable, which is
what lets a later behaviour change be explained after the fact.

A value in the Notes column is the version 1 the Worker's `seed` verb writes, and
the reason it was chosen. **The store is the authority on what is in force**: a
revision inserts version + 1 and does not edit this document, so a value here
that the store has since revised is history rather than a contradiction. A key
marked **Unset** has no version at all, which is a different thing from a key
whose current value differs from the one recorded here.

## Risk

All four are set at version 1 in checkpoint 2.4, which is where the risk gate
first reads them. An equity-relative cap is the operator's risk appetite
[D-W11], and the worked example illustrating one account is not the operator
setting one, so 0.8 deliberately left them rather than transcribing an example's
figures. The Notes below say per key whether its value is transcribed or chosen.

The caps are fractions of equity, and equity is configuration rather than a
figure derived from cash and open positions. A derived denominator moves with
the run and would loosen every cap during a drawdown, which is when they matter
[D-W11].

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Risk:Equity` | rows | the account value the three caps are fractions of | Risk gate via `PortfolioBounds` | Structural, not learner-proposable [D-W11]. 100000.00, transcribed from `WORKED_EXAMPLE.md` section 1 |
| `Risk:PerNameCapFraction` | rows | max committed capital in one name, as a fraction of equity | Risk gate via `PortfolioBounds` | Structural, not learner-proposable [D-W11]. 0.25, transcribed from `WORKED_EXAMPLE.md` section 1, which derives the 5,100.00 headroom section 3 states |
| `Risk:TotalCapFraction` | rows | max total committed capital, as a fraction of equity | Risk gate via `PortfolioBounds` | Structural [D-W11]. 0.60, transcribed from `WORKED_EXAMPLE.md` section 1 |
| `Risk:SimultaneousAssignmentLimitFraction` | rows | max exposure if every open short put assigned at once, as a fraction of equity | Risk gate via `PortfolioBounds` | Structural [D-W11]. 0.60, chosen rather than transcribed, no document stating it. Held equal to `Risk:TotalCapFraction` because a cash-secured put's committed capital is its assignment exposure, so a lower value makes the total cap unreachable and a higher one never binds. The relationship changes at Phase 3, when a covered call commits shares rather than cash |

## Gate constraints

All structural, none learner-proposable [D-W11]. Every value below is the one its
decision proposed, set at version 1 in Phase 0.8.

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Gate:MaxSpreadFractionOfMid` | rows | reject above this spread as a fraction of mid | Risk gate via `GateBounds` | 0.12, the proposed default [D-W22] |
| `Gate:MinPremium` | rows | reject below this bid | Risk gate via `GateBounds` | 0.30, the proposed default [D-W22] |
| `Gate:MaxDelta` | rows | reject above this absolute delta | Risk gate via `GateBounds` | 0.35, the proposed default. Held equal to `Policy:Random:DeltaMax` by choice rather than by coincidence of defaults, which settles that decision's open clause. Must be no tighter than the loosest policy band [D-W23] |
| `Gate:MinDte` | rows | earliest admissible expiry | Risk gate via `GateBounds` | 7, the proposed default [D-W24] |
| `Gate:MaxDte` | rows | latest admissible expiry | Risk gate via `GateBounds` | 70, the proposed default. Must be less than `Trial:MaxTrialDays` [D-W24] |
| `Gate:EarningsClearanceDays` | rows | buffer either side of a report date | Risk gate via `GateBounds` | 7, the proposed default [D-W25] |

Two cross-key invariants are enforced when a config version is WRITTEN, not at
startup: `Gate:MaxDelta` against every policy band [D-W23], and `Gate:MaxDte`
against `Trial:MaxTrialDays` [D-W24]. A version violating either is refused.
Because config rows are versioned and insertable at runtime, a startup check
would leave every later version unguarded; it survives only as a backstop.

## Trial bounds

Neither value is proposed anywhere, so both are judgement, set at version 1 in
Phase 0.8.

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Trial:MaxRolls` | rows | rolls permitted before forced close at market | State machine **Unverified** | 2, judged [D-W14]. Low enough to bind sometimes rather than be decorative: the day bound already caps most rolled chains |
| `Trial:MaxTrialDays` | rows | total days before forced close at market | State machine **Unverified** | 120, judged [D-W14], and constrained twice. Must exceed `Gate:MaxDte` [D-W24], and must leave the worked example's own 109-day trial representable or its total becomes unreachable |

## Scoring

Neither value is constrained by any statement in this corpus. Both are free
judgement, which is what D-W20 left open, set at version 1 in Phase 0.8.

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Scoring:DivergenceThreshold` | rows | rank-correlation floor between fast and slow scores | Divergence monitor **Unverified** | 0.70, judged and free [D-W20] |
| `Scoring:DivergenceWindowDays` | rows | window over which divergence is measured | Divergence monitor **Unverified** | 90, judged and free [D-W20]. Long enough for a rank correlation over a meaningful number of decisions, short enough to notice a change within a quarter |

## Costs

Provenance is judged per key, not per section. Two are set in Phase 0.8 and the
third is owed.

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Costs:CommissionPerContract` | rows | per-contract commission | Fill model **Unverified** | 0.65, from `WORKED_EXAMPLE.md` section 1, which that document's fills, ledger and expected total all depend on [D-W12]. A real broker's rate replaces it by version + 1 |
| `Costs:AssignmentFee` | rows | fee on assignment or exercise | Fill model **Unverified** | **Unset.** Phase 3. No document states it, and zero inferred from an absent ledger line is weaker than a stated number and invisible when wrong [D-W12] |
| `Costs:FillPoint` | rows | where in the spread a sale fills | Fill model **Unverified** | `bid`, fixed in advance and not a tunable [D-W12]. Set anyway because a fixed value still has to be readable, and a `rows` key never written cannot be resolved as-of at all |

## Policy bands

Every band value is transcribed from `WORKED_EXAMPLE.md` section 1 and set at
version 1 in Phase 0.8. The random maker reads the baseline's expiry window,
which is why `Policy:Random:` carries no DTE keys: their absence is the coupling,
not an omission.

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Policy:Baseline:DeltaMin` | rows | frozen baseline delta band, lower bound | Frozen baseline maker **Unverified** | 0.20. Never changes for the life of the experiment [D-W4] |
| `Policy:Baseline:DeltaMax` | rows | frozen baseline delta band, upper bound | Frozen baseline maker **Unverified** | 0.30. Never changes [D-W4]. The gate's delta ceiling must be no tighter than this [D-W23] |
| `Policy:Baseline:DteMin` | rows | frozen baseline expiry window, lower bound | Frozen baseline maker **Unverified** | 30. Never changes [D-W4] |
| `Policy:Baseline:DteMax` | rows | frozen baseline expiry window, upper bound | Frozen baseline maker **Unverified** | 60. Never changes [D-W4]. The random maker draws inside this window too |
| `Policy:Random:DeltaMin` | rows | random control band, lower bound | Random maker **Unverified** | 0.10, inherited rather than argued: there is no `Gate:MinDelta`, so this floor sits strictly inside what the gate admits and D-W4's argument does not reach it [D-W4] |
| `Policy:Random:DeltaMax` | rows | random control band, upper bound | Random maker **Unverified** | 0.35, sitting exactly at the gate's delta ceiling by choice: a control drawing from a smaller opportunity set than the gate admits would make a difference between makers partly permission rather than judgement [D-W4, D-W23] |
| `Policy:Random:Seed` | rows | seed for the random maker | Random maker **Unverified** | 20260729, chosen rather than stated. The value is arbitrary; that it is fixed is not, since the control has to draw the same way on a re-run |

## Storage

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Storage:Path` | app | absolute directory holding the store and its snapshots | Storage via `StorageOptions` | **Unset.** Supplied per machine through `Storage__Path` |

## Data

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Eodhd:BaseUrl` | app | API root | Ingest via `EodhdOptions` | **Unset.** Set at Phase 8 [D-W7] |
| `Eodhd:ApiKey` | app | credential | Ingest via `EodhdOptions` | Lives in `appsettings.Secrets.json`, never committed |
| `Eodhd:OptionsAddOnEnabled` | app | whether the options add-on is purchased | Ingest via `EodhdOptions` | False until Phase 8 [D-W7] |
