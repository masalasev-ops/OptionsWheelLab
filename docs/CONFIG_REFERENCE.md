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

Every key carries a **Store** class [D-W27]. `rows` means a config row,
resolved as-of a simulated date [D-W26]. `app` means bound from `appsettings`,
which is not as-of resolvable and is therefore reserved for values that never
participate in a decision.

One key per row. A row naming two keys leaves the second unreadable to
anything parsing this document, and a suffix-only second token is not a key
path at all. Keys that constrain each other say so in their Notes instead.

Config rows are append-only and versioned; current is `MAX(version)` for a key. A revision inserts version + 1 and the old value stays readable, which is
what lets a later behaviour change be explained after the fact.

## Risk

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Risk:PerNameCapPct` | rows | max committed capital in one name, as a fraction of equity | Risk gate **Unverified** | Structural, not learner-proposable [D-W11] |
| `Risk:TotalCapPct` | rows | max total committed capital, as a fraction of equity | Risk gate **Unverified** | Structural [D-W11] |
| `Risk:SimultaneousAssignmentLimitPct` | rows | max exposure if every open short put assigned at once | Risk gate **Unverified** | Structural [D-W11] |

## Gate constraints

All structural, none learner-proposable [D-W11]. Values below are proposed
defaults, set in Phase 0.8.

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Gate:MaxSpreadPctOfMid` | rows | reject above this spread as a fraction of mid | Risk gate **Unverified** | Proposed 12. **Unset** [D-W22] |
| `Gate:MinPremium` | rows | reject below this bid | Risk gate **Unverified** | Proposed 0.30. **Unset** [D-W22] |
| `Gate:MaxDelta` | rows | reject above this absolute delta | Risk gate **Unverified** | Proposed 0.35. Must be no tighter than the loosest policy band. **Unset** [D-W23] |
| `Gate:MinDte` | rows | earliest admissible expiry | Risk gate **Unverified** | Proposed 7. **Unset** [D-W24] |
| `Gate:MaxDte` | rows | latest admissible expiry | Risk gate **Unverified** | Proposed 70. Must be less than `Trial:MaxTrialDays`. **Unset** [D-W24] |
| `Gate:EarningsClearanceDays` | rows | buffer either side of a report date | Risk gate **Unverified** | Proposed 7. **Unset** [D-W25] |

Two cross-key invariants are enforced when a config version is WRITTEN, not at
startup: `Gate:MaxDelta` against every policy band [D-W23], and `Gate:MaxDte`
against `Trial:MaxTrialDays` [D-W24]. A version violating either is refused.
Because config rows are versioned and insertable at runtime, a startup check
would leave every later version unguarded; it survives only as a backstop.

## Trial bounds

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Trial:MaxRolls` | rows | rolls permitted before forced close at market | State machine **Unverified** | **Unset.** Phase 0.8 [D-W14] |
| `Trial:MaxTrialDays` | rows | total days before forced close at market | State machine **Unverified** | **Unset.** Phase 0.8 [D-W14] |

## Scoring

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Scoring:DivergenceThreshold` | rows | rank-correlation floor between fast and slow scores | Divergence monitor **Unverified** | **Unset.** Phase 0.8 [D-W20] |
| `Scoring:DivergenceWindowDays` | rows | window over which divergence is measured | Divergence monitor **Unverified** | **Unset.** Phase 0.8 [D-W20] |

## Costs

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Costs:CommissionPerContract` | rows | per-contract commission | Fill model **Unverified** | [D-W12] |
| `Costs:AssignmentFee` | rows | fee on assignment or exercise | Fill model **Unverified** | [D-W12] |
| `Costs:FillPoint` | rows | where in the spread a sale fills | Fill model **Unverified** | Fixed at `bid`; not a tunable [D-W12] |

## Policy bands

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Policy:Baseline:DeltaMin` | rows | frozen baseline delta band, lower bound | Frozen baseline maker **Unverified** | Never changes for the life of the experiment [D-W4] |
| `Policy:Baseline:DeltaMax` | rows | frozen baseline delta band, upper bound | Frozen baseline maker **Unverified** | Never changes [D-W4]. The gate's delta ceiling must be no tighter than this [D-W23] |
| `Policy:Baseline:DteMin` | rows | frozen baseline expiry window, lower bound | Frozen baseline maker **Unverified** | Never changes [D-W4] |
| `Policy:Baseline:DteMax` | rows | frozen baseline expiry window, upper bound | Frozen baseline maker **Unverified** | Never changes [D-W4] |
| `Policy:Random:DeltaMin` | rows | random control band, lower bound | Random maker **Unverified** | [D-W4] |
| `Policy:Random:DeltaMax` | rows | random control band, upper bound | Random maker **Unverified** | Proposed 0.35 sits exactly at the gate's delta ceiling; see [D-W23] |
| `Policy:Random:Seed` | rows | seed for the random maker | Random maker **Unverified** | Fixed so runs reproduce |

## Data

| Key | Store | Meaning | Consumer | Notes |
|---|---|---|---|---|
| `Eodhd:BaseUrl` | app | API root | Ingest via `EodhdOptions` | **Unset.** Set at Phase 8 [D-W7] |
| `Eodhd:ApiKey` | app | credential | Ingest via `EodhdOptions` | Lives in `appsettings.Secrets.json`, never committed |
| `Eodhd:OptionsAddOnEnabled` | app | whether the options add-on is purchased | Ingest via `EodhdOptions` | False until Phase 8 [D-W7] |
