# Phase 0 Foundations: spent prompts

Current state below is the whole state of the repository and the only
description of the present.

One prompt per checkpoint, being the prompt that produces the checkpoint as it
now stands. Corrections found while building are folded back into the
checkpoint's prompt rather than appended as further entries, so replaying the
prompts in order against the corpus reproduces the current state without
replaying the mistakes.

One file per phase. It closes when Phase 0 signs off; Phase 1 opens its own.

---

# Current state

Corpus v1.9.6.

| | |
|---|---|
| Phase 0 | 0.1 and 0.2 built; 0.3 onward not started |
| Branch | `phase-0/checkpoint-0.3`, off `main` |
| Merged | PR #1 into `main` as `53cc0b4`, 24 commits preserved, not squashed |
| CI | green, 36 tests, restore and build and test on push to `main` and every pull request |

## Build

.NET 10 solution, nullable enabled, warnings as errors, central package
management: `OptionsWheelLab.Core` holding the composition root and options
types, `.Worker` and `.Api` as thin hosts both calling it, and `.Tests`.

One shared `src/appsettings.json` linked into both hosts and the test project,
loaded from `AppContext.BaseDirectory` because the generic host and the web host
default their content roots differently. No `Logging` section is committed, so
every top-level section must bind and the binding test needs no framework
allowlist. `appsettings.Secrets.json` is gitignored with a committed empty
`.example` and loads optionally, so a fresh clone builds without it.

`Microsoft.AspNetCore.OpenApi` is absent. Version 10.0.9 pulls
`Microsoft.OpenApi` 2.0.0, carrying advisory GHSA-v5pm-xwqc-g5wc, which the
build's audit fails on.

## Configuration

One section bound: `Eodhd` to `EodhdOptions`, verified by reading the
composition root.

Six sections deliberately unbound because `CONFIG_REFERENCE.md` classes them
`rows` and a registered options type is itself a current-value accessor: `Risk`,
`Gate`, `Costs`, `Policy`, `Trial`, `Scoring`.

`CONFIG_REFERENCE.md` carries 26 key rows, one key per row. Three Consumer cells
are verified as `Ingest via EodhdOptions`; 23 carry **Unverified**. No value is
set that the document marks unset.

The two cross-key invariants are pure predicates in `Core` over supplied values,
with no host, no config store, no startup wiring and no clock.

## Tests

36 across six fixtures plus the 0.1 smoke test.

| Fixture | Tests |
|---|---|
| FX-ConfigStoreClassHonoured | 12 |
| FX-CeilingNotInsidePolicyBand | 7 |
| FX-EveryConfigSectionBinds | 6 |
| FX-EveryBoundKeyIsDocumented | 5 |
| FX-MaxDteBelowTrialBound | 4 |
| FX-RegistryMatchesDisk | 1 |

All six fixtures registered against 0.2 are implemented and named for their
registry entry. The suite parses `CONFIG_REFERENCE.md` and `FIXTURES.md`, so
both are load-bearing rather than descriptive: an edit breaking their table
shape fails the build.

## Layout

Repository root holds `README.md` and `CLAUDE.md` only. Every other document is
in `docs/`. Spent prompts are in `prompts/spent/`.

## Working rules in force

- Commit subjects are prefixed with the phase name and stage, as
  `Phase 0 Foundations / 0.2 - <type>: <subject>`.
- The pull request description is updated on every check-in.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

## Not built

The store, migrations and `migrate.ps1`. The config read service and its as-of
resolver. The deterministic clock. Money and ticker primitives. The append-only
CI greps. Every checkpoint from 0.3 onward.

## Owed

- **Phase 11**: re-add `Microsoft.AspNetCore.OpenApi` against a version whose
  `Microsoft.OpenApi` dependency clears the audit. In `BUILD_PLAN.md` carried
  obligations.
- **0.8**: wire the two cross-key invariants to the config write path, and
  FX-ConfigWriteRefusesInvariantBreach.
- **0.3**: the config read service and its as-of resolver, FX-ConfigResolvesAsOf
  and FX-NoCurrentConfigReadOnSimulatedPath.

---

# Prompts

## 0.1 Repository skeleton

Read `CLAUDE.md`, `README.md`, `SYSTEM_DESIGN.md` §7, `BUILD_PLAN.md` §0.1.
`CLAUDE.md` is binding for how you work, not only for what you build.

- .NET 10 solution: `OptionsWheelLab.Core`, `OptionsWheelLab.Worker`,
  `OptionsWheelLab.Api`, `OptionsWheelLab.Tests`. Nullable enabled, warnings as
  errors, central package management. `Core` holds the composition root; the two
  hosts are thin and both call it, so there is one binding site.
- The repository root holds `README.md` and `CLAUDE.md` only. Every other
  document lives in `docs/`.
- One shared `src/appsettings.json`, linked as content into both hosts and the
  test project rather than one file per host, because a Worker and an Api
  disagreeing about the lab's configuration is a defect. Load it from
  `AppContext.BaseDirectory` rather than the host default: the generic host and
  the web host default their content roots differently and relying on that
  difference is a trap.
- Commit no `Logging` section, so every top-level section in `appsettings.json`
  must bind to one of the lab's own options types and the binding test needs no
  framework allowlist. An allowlist is where a stray section would hide.
- `appsettings.Secrets.json` in `.gitignore`, with
  `appsettings.Secrets.example.json` committed alongside carrying empty values
  and no real credentials. Load the secrets file optionally so a fresh clone
  builds without it. Inspect the `.gitignore` diff before committing it, and
  confirm the `.example` is not matched by the exclusion.
- Pin line endings to LF so a Windows checkout cannot produce a whitespace-only
  diff, which by rule would have to be its own commit.
- Do not reference `Microsoft.AspNetCore.OpenApi`. Version 10.0.9 pulls
  `Microsoft.OpenApi` 2.0.0, which carries a high severity advisory that the
  build's vulnerability audit fails on. The API surface is Phase 11 and has no
  endpoint to describe, so suppressing the audit to keep an unused package would
  cost detection everywhere [`CLAUDE.md` 4a]. Record the reason at the reference
  site and add the re-adding to carried obligations in `BUILD_PLAN.md`.
- CI workflow running restore, build and test on push to `main` and on every
  pull request, on actions targeting a supported Node runtime.
- Code reaches GitHub as a pull request with CI, never by committing to `main`.

- **Test**: solution builds; a trivial test passes in `OptionsWheelLab.Tests`.
- **DoD**: CI green on a fresh clone with no local state.

## 0.2 Configuration binding

Read `CONFIG_REFERENCE.md` and `FIXTURES.md` before starting. Confirm the
working copy is current before reporting any corpus entry as absent
[`CLAUDE.md` 10].

- Bind only the sections `CONFIG_REFERENCE.md` classes as `app` [D-W27]. A
  section classed `rows` is never given an appsettings-bound options type, not
  even as a placeholder, because a registered options class is itself a
  current-value accessor and 0.3 would then hold two paths to the same values
  [D-W26].
- Leave values unset wherever that document marks them unset. Do not invent
  values.
- Route every binding through one helper that records the section path and
  options type in the same call, so the record is a by-product of binding rather
  than a parallel list. A test reading a hand-maintained list passes while the
  list is stale, which is the failure this checkpoint exists to prevent.
- Implement the fixtures registered against 0.2 in `FIXTURES.md`, reading their
  assertions from that file. Name each test file exactly for its fixture.
- The binding test must ENUMERATE the options types registered in composition
  and fail on any section that binds to nothing. A test that only asserts known
  sections populate does not satisfy this. Check both directions, and also that
  every options type configured at composition was registered through the
  helper, or the bypass leaves the enumeration comparing an incomplete set.
- The binding check covers every committed configuration file, being
  `appsettings.json` and `appsettings.Secrets.example.json`. The uncommitted
  `appsettings.Secrets.json` is out of scope, being absent on a fresh clone.
- Deliver the two cross-key invariants [D-W23, D-W24] as pure predicates in
  `Core` over supplied values, with no host, no config store and no startup
  wiring. Enforcement is at config-write time and lands at 0.8. Cover the
  holding case, the violating case, and equality at the boundary.
- Any test that parses a corpus document or enumerates a set asserts its input
  is non-empty before comparing anything. A parse that silently matches nothing
  passes without testing anything, which has already happened once in this
  corpus.

- **DoD**: the binding test fails when a stray section is added to a committed
  configuration file and passes when it is removed. Demonstrate both, and commit
  neither stray section.
- **DoD**: `CONFIG_REFERENCE.md`'s Consumer column names the component and the
  verified type, as `component via TypeName`, for every key bound in this
  checkpoint. Verify by reading the composition code, not by a grep for the key
  name.
- **DoD**: every fixture registered against 0.2 in `FIXTURES.md` exists and is
  named for it.

Constraints
- No `DateTime.Now` or `DateTime.UtcNow`. The clock abstraction lands in 0.5.
- No `double` or `float` in any type that will carry money.

Out of scope
- The store, migrations, and any table. That is 0.3.
- The `config_rows`-backed configuration path and the as-of resolver. That is
  0.3. Do not introduce a current-value config accessor that 0.3 would have to
  remove.
- Setting values for any key `CONFIG_REFERENCE.md` marks unset. Those are 0.8.
