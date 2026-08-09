---
title: "ADR-0017: Calibrated autonomous evaluation evidence"
status: "Accepted"
date: "2026-08-09"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "evaluation", "langfuse", "agentic-workflows"]
supersedes: ""
superseded_by: ""
---

# ADR-0017: Calibrated autonomous evaluation evidence

## Context and scope

The first hosted autonomous-phase matrix completed operationally but produced
invalid comparative evidence. Correctness depended on internal delegation
topology, prompts leaked expected answers, evidence subsets passed, fault
activation was unreliable, cancellation preceded real work, budgets differed,
and no calibrated semantic judge existed.

This decision governs comparative evaluation of alternative autonomous phase
executors. It does not choose a synthesis architecture or authorize a live
model run.

## Decision drivers

- Measure stable task contracts rather than nondeterministic trajectories.
- Keep infrastructure failure separate from arm quality.
- Make every fault and budget auditable.
- Preserve exact, content-addressed evidence and gap contracts.
- Support paired experiments over one immutable hosted dataset.
- Prevent uncalibrated semantic scores from becoming recommendations.
- Keep all live execution manual and explicitly authorized.

## Decision

Foundry evaluation protocols separate four evidence families:

1. deterministic correctness;
2. resilience and recovery;
3. descriptive resource usage; and
4. infrastructure health.

Topology, child count, tool order, plans, and replans are resource or behavioral
evidence unless the scenario explicitly targets them. They are never implicit
correctness requirements.

Protocol cases are stored as versioned Langfuse dataset items. Alternative arms
run as separate experiments over the same item IDs and dataset version.
Item-level scores preserve the four evidence families, and run-level scores
record aggregates and recommendation status.

Required faults carry typed target roles and activation points. Failure to
activate a required fault invalidates the item. Cancellation faults activate
only after provider work and, for delegated or Magentic arms, collaborator
work.

All compared arms receive the same maximum artifact attempts, correction
feedback, and provider-call budget. Equal opportunity is controlled; actual
consumption remains a measured outcome.

Checkpoint scenarios restore the retained live run. Fresh-workflow restoration
is not evaluation evidence while ADR-0016's upstream blockers remain.

Recommendation status progresses through protocol validity, infrastructure
reliability, statistical power, pilot evidence, and finally a preregistered
support gate. Raw pass counts cannot directly produce an architecture
recommendation.

LLM-as-a-judge scores are inadmissible until a versioned calibration attestation
records a blinded labelled corpus, held-out split, judge and prompt versions,
confusion matrix, bias checks, thresholds, and an admitted result.

The evaluation executable is model-free and publishes only protocol and dataset
state. It exposes no direct live-model path. Any future live study must use a
separately accepted architecture that preserves the repository rule that
Copilot CLI is the sole permitted live-model process.

## Alternatives considered

### Keep the deterministic scoreboard and add more trials

Rejected. More samples do not repair construct invalidity, answer leakage,
unmatched budgets, or broken fault activation.

### Use one weighted composite score

Rejected. Weighting hides whether a result failed correctness, resilience,
cost, or infrastructure and invites topology proxies back into quality.

### Let an uncalibrated model judge provide semantic quality

Rejected. Judge agreement, class-direction bias, position bias, and held-out
generalization must be measured before semantic scores influence decisions.

### Store only local JSON artifacts

Rejected as the complete evidence path. Local artifacts remain useful, but a
versioned hosted dataset and separate experiment runs provide reproducible
case identity, item scores, comparison, and read-back.

## Consequences

### Positive

- Correctness is manifest-grounded and topology-independent.
- Fault misses and infrastructure failures cannot masquerade as arm failures.
- Arm budgets and model identity are explicit.
- Langfuse provides durable dataset and experiment identity.
- Semantic recommendation claims have a calibration gate.
- Protocol-only execution is safe without model credentials.

### Negative

- The protocol produces no architecture recommendation until enough admissible
  evidence exists.
- Live studies require explicit operational authorization and secrets.
- Statistical and judge calibration add work before confirmatory claims.
- Existing protocol-v1 reports remain historical rather than reusable.

### Neutral

- Foundry's production phase APIs remain unchanged.
- Langfuse is an evidence backend, not the workflow runtime.
- Provider calls, tokens, and latency remain descriptive until a comparison
  hypothesis defines their role.

## Confirmation

The decision is confirmed by deterministic tests for:

- exact, subset, superset, and gap-mismatch evidence;
- typed fault activation after provider work;
- fail-closed recommendation states;
- stable versioned dataset items;
- common arm budgets; and
- a manual model-free Langfuse data-plane dry run that published and read back
  the versioned dataset.

Any live confirmation remains separately authorized.

## References

- [Autonomous Phase Evaluation 002](../evaluations/autonomous-phase-002.md)
- [Langfuse experiments](https://langfuse.com/docs/evaluation/experiments/experiments-via-sdk)
- [Langfuse experiment data model](https://langfuse.com/docs/evaluation/experiments/data-model)
- [Langfuse tracing best practices](https://langfuse.com/docs/observability/best-practices)
- ADR-0016 limits checkpoint recovery to same-run restoration.
