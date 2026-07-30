# OptionsWheelLab

A paper-trading laboratory that studies whether a decision-maker's decisions
improve over time, using the options wheel as its task environment.

Corpus version 1.18.0. The corpus was regenerated from scratch at v1.0.0 on
2026-07-26, superseding the lost v0.1 entirely.

Phase 0 is complete and reviewed. The repository compiles, tests, migrates and
seeds its configuration, and holds no market data and no domain logic. Phase 1 is
in progress.

## What this lab is not

It is not a search for a profitable wheel strategy. Whether the wheel beats
buying and holding the same shares is a secondary question that this lab can
answer but was not built to answer. The lab succeeds if the decision-maker's
decisions measurably improve, and it can succeed even if no configuration of
the wheel ever beats its controls [D-W2].

## Reading order

Read these three, in order, to understand the lab:

1. `ORIENTATION.md` — plain language, with the pictures. Start here.
2. `SYSTEM_DESIGN.md` — the narrative design, including the complete phase map.
3. `WORKED_EXAMPLE.md` — one decision traced end to end with arithmetic.

Read these first if the wheel or its vocabulary is new:

- `PRIMER_THE_WHEEL.md` — the strategy itself, in plain terms. Not about this lab.
- `GLOSSARY.md` — options terms and this lab's own terms, both defined.

Read these when you need them:

- `DECISIONS.md` — the numbered register. Looked up, not read start to finish.
- `VALIDITY.md` — what the lab claims, what would falsify it, what it cannot test.
- `DATA_AND_SCHEMA.md` — the data contract and the store.
- `BUILD_PLAN.md` — phase map plus detail for the phase in progress, and the
  reconciled detail of every checkpoint already signed off.
- `FIXTURES.md` — the single registry of test fixtures.
- `CONFIG_REFERENCE.md` — every configuration key and its verified consumer.
- `CLAUDE.md` — rules for agents working in this repo.
- `UI_MOCKUPS.md` — the visual specification and the brief that produced it.

These track state and are appended to, never rewritten:

- `PROGRESS.md`
- `CHANGELOG.md`

## Where these files live

The repository root holds `README.md` and `CLAUDE.md` only. Every other
document in this corpus lives in `docs/`. One rule, so anything resolving a
corpus path needs to know one thing rather than a list of exceptions.

That rule governs the corpus. Code lives in `src/` and `tests/`, spent prompts in
`prompts/`, and hand-written synthetic chains in `synthetic/`. None of those are
documents and none belong in `docs/`.

## Corpus rules

Four rules govern every document here. They exist because the AlphaLab corpus
had to be retrofitted with all four, and it is cheaper to be born with them.

**Build-state markers.** Every section that describes a component carries a
build-state marker. Without one, shipped and aspirational prose read identically
and a reader cannot tell a description from a plan.

**The bracket rule.** Prose states the rule; the decision number is a trailing
bracket. Test: delete the bracket and the sentence must still read correctly and
still state the rule. A sentence that becomes meaningless without its bracket has
made the narrative dependent on the ledger.

**Narrative and ledger are separate documents.** `SYSTEM_DESIGN.md` is read start
to finish by a human. `DECISIONS.md` is looked up. They have different audiences
and different lifecycles and must not share a file.

**Never renumber.** A superseded decision keeps its number and gains a status
pointing at what replaced it.

## Repository conventions inherited from AlphaLab

.NET 10, SQLite, Blazor. Worker is the sole writer. Money is stored as decimal
in TEXT columns. Configuration lives in append-only versioned rows where current
is `MAX(version)`. Migrations are snapshot-first. Secrets live in
`appsettings.Secrets.json` and are never committed. API surface is `/api/v1` with
native OpenAPI and Scalar. State is modelled with discriminated unions. Tickers
use the EODHD dash form, for example `BRK-B`.
