# ADR-0010: Retain all Harness execution modes

## Status

Accepted — 2026-07-30

## Context

Foundry ships three overlapping ways to run a long-lived agent:

1. the Foundry workspace-driven iterative loop;
2. plain Harness with explicit compaction; and
3. an experimental internal hybrid of Harness compaction plus the Foundry
   workspace.

That overlap was accepted during Harness integration on the assumption that a
later comparison would show which one should become the default and which could
be removed.

## What we tried

We built a hosted agent evaluation to answer that question: a frozen case set,
three execution arms, paired trials, and statistical comparison. It ran to
completion against a real provider.

**It could not answer the question.** Eight test cases is far too small a
population to separate the three approaches. Every completion interval included
zero, every continuous measure was an insufficient sample, and every contrast
was formally underpowered. The comparison produced no actionable signal.

## Decision

**Retain all three execution modes. Change no default. Remove nothing.**

These overlaps are retained:

| Overlap | Rationale |
|---|---|
| Foundry `IWorkspace` alongside upstream file stores | Workspace remains the authoritative bulk-content store |
| Iterative loop alongside Harness/LoopAgent | Nothing distinguishes them on the evidence available |
| Foundry preservation policy alongside upstream compaction | Hybrid compaction demonstrated a fail-closed case that argues for keeping the Foundry contract |
| Selected-provider lane alongside the complete bundle | Each retains a distinct supported scenario |

Consumers continue to choose explicitly between plain Foundry MAF agents, the
optional Harness bundle, and the Foundry iterative loop.

## A second, more durable decision

**Agent evaluation was the wrong instrument for this question, and we are not
repeating it.**

Deciding which internal execution strategy should be the default is an
architecture and ergonomics judgement. Answering it statistically would have
required a large case population, a controlled provider, and calibrated
subjective scoring — a research programme, not a design decision. The apparatus
was disproportionate to the question and returned nothing.

Consequently the entire study — case set, run evidence, comparison reporter,
per-dimension evaluators, LLM-as-judge scoring, the hosted driver, and its
workflow — was removed from the repository. It remains recoverable from git
history.

## Consequences

- No ADR is superseded. ADR-0005 through ADR-0008 continue to describe retained,
  optional, and experimental surfaces rather than a selected default.
- The experimental hybrid profile stays internal and experimental.
- Foundry keeps no bespoke Harness comparison machinery. The general-purpose
  evaluation primitives in `NexusLabs.Foundry.Evaluation` — experiments,
  evaluators, capture, and reporting — are unaffected and remain supported.
- If consolidating to one execution path ever becomes a goal, drive it from API
  design, maintenance cost, and real consumer workloads rather than another
  statistical comparison of synthetic cases.
