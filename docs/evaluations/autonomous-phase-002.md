---
description: Calibrated protocol for evaluating fixed-macro autonomous synthesis arms without topology-based scoring or automated live-model CI.
status: protocol-defined
---

# Autonomous Phase Evaluation 002

Protocol v2 replaces the invalidated first autonomous-phase comparison. It
evaluates plain Harness, delegated Harness, and phase-local Magentic as
alternative executors inside the same fixed synthesis phase.

No live execution result is published for this protocol yet. The repository
contains deterministic protocol tests and a model-free Langfuse dataset
publication mode. A live pilot is a separate, explicitly authorized operation.

## Non-negotiable boundaries

- The outer manifest, artifact gate, checkpoint, and delivery contracts are
  identical across arms.
- Correctness depends on the accepted artifact, never child count, tool order,
  delegation shape, or Magentic topology.
- All arms receive two artifact attempts and one shared 24-provider-call phase
  budget.
- Faults use typed target roles and activation points.
- A required fault that does not activate invalidates that item.
- Checkpoint recovery uses the retained live `StreamingRun`; fresh-workflow
  resume is outside the supported protocol.
- Automated CI never invokes a live model.
- Semantic scores cannot influence a recommendation until a judge calibration
  attestation is admitted.

## Langfuse dataset

The hosted dataset is:

```text
foundry/autonomous-phase-synthesis
```

Each item represents one case and scenario. Arms are separate experiment runs
over the same immutable dataset version.

Dataset input contains:

- protocol and schema version;
- case and scenario identifiers; and
- the common artifact-attempt and provider-call limits.

Expected output contains:

- pipeline and synthesis outcomes;
- exact content-addressed evidence references;
- exact gaps;
- sibling phases whose artifacts must survive; and
- authoritative delivery count.

Expected output is never passed to the model task. Stable project-global item
IDs and fixture digests make dataset publication idempotent and auditable.

## Scenarios

| Scenario | Contract under test |
| --- | --- |
| Success | Exact manifest-grounded synthesis and one delivery |
| Optional branch failure | Partial outcome, exact gap, required sibling preserved |
| Required branch failure | Failed outcome, synthesis skipped, optional sibling preserved |
| Correction succeeds | First terminal artifact invalid, corrected within two attempts |
| Correction exhausted | Every terminal artifact invalid, bounded failed outcome |
| Cancellation | Cancellation activates only after terminal provider work; no delivery |
| Checkpoint restore | Same-run restore succeeds without replaying the accepted start |
| Delivery replay | Two publication attempts, one authoritative result |
| Ineffective progress | Harness corrects; Magentic activates a typed stall and replans |

Required-branch failure and delivery replay are macro controls. They remain
visible but are excluded from pooled arm-quality claims. Ineffective progress
is also non-poolable because it deliberately targets different arm-specific
coordination behavior.

## Metric families

### Correctness

- artifact contract valid;
- evidence exact set;
- gaps exact set;
- required coverage matches the manifest;
- successful siblings preserved;
- pipeline outcome matches;
- synthesis outcome matches; and
- authoritative delivery count matches.

The final artifact text read from the content-addressed store is the scored
output. Provider text observed before a fault or boundary is never a fallback.

### Resilience

- required fault activated;
- activation occurred after provider work;
- delegated or Magentic cancellation occurred after collaborator work;
- cancellation produced no delivery;
- same-run restore succeeded;
- accepted prior phase did not rerun;
- replay remained idempotent; and
- correction recovered within the declared bound.

### Resources

Provider calls, tool calls, tokens, duration, child sessions, checkpoints,
plans, replans, and progress events are descriptive measurements. They are not
correctness inputs unless the scenario explicitly targets that behavior.

### Infrastructure

Provider failures, phase exceptions, missing items, and publication failures
are exclusions, not arm failures. An infrastructure exclusion rate above 10%
blocks comparative interpretation.

## Trace contract

One Langfuse trace represents one arm execution:

```text
autonomous-phase.synthesis
  phase.synthesis
    phase.manifest-start
    phase.fault-boundary
    provider generations and tools
    phase.artifact-boundary
    phase.delivery
```

Stable trace metadata records protocol, arm, scenario, case, trial, commit,
requested model, fixture digest, fault plan, and budgets. Observed model identity
is recorded in the immutable arm provenance and result scores. Dynamic values
remain metadata rather than observation names.

The repository defines this trace and score contract but supplies no live chat
client factory. The model-free executable publishes only the dataset.

## Recommendation gate

The report status is evaluated in order:

1. `ProtocolInvalid` — systemic budget mismatch, inconsistent observed model,
   semantic-score admission without calibration, or topology-based correctness.
2. `InfrastructureUnreliable` — infrastructure exclusion rate exceeds the
   declared limit.
3. `InsufficientlyPowered` — fewer than three trials or insufficient admissible
   paired results.
4. `Pilot` — admissible descriptive evidence exists, but no confirmatory
   inference gate has passed.
5. `Supported` — a preregistered effect-size and paired-inference gate passes.

Only `Supported` permits an architecture recommendation.

A required fault miss invalidates and excludes that item. It does not
automatically invalidate unrelated arms or blocks.

Protocol v2 currently has no path that manufactures `Supported` from raw pass
counts. This is intentional.

## Future semantic judge calibration

A model-based judge remains inadmissible until a separate calibration corpus
exists with:

- blinded arm, model, topology, length, and token metadata;
- two independent human labels plus adjudication;
- grouped held-out splits by source run and scenario family;
- near-miss failures for each rubric dimension;
- confusion-matrix metrics and disagreement analysis; and
- a versioned attestation containing judge, prompt, corpus and split hashes.

Proposed admission targets are Cohen or weighted kappa of at least 0.60,
ordinal correlation of at least 0.80, defect sensitivity and specificity of at
least 0.75, and position-order flip rate no greater than 10%. These are protocol
proposals, not universal quality standards.

Until admitted, semantic scores may be recorded descriptively but cannot affect
the recommendation gate.

## Execution mode

The evaluation application is model-free. It can:

- generate the deterministic dataset contract locally;
- upsert and read back the hosted Langfuse dataset when process-scoped
  credentials are present; and
- exit without constructing or invoking a model provider.

It exposes no live-model mode. The repository's manually authorized Copilot CLI
workflow does not invoke this application or another provider SDK. Any future
live pilot requires both explicit per-run permission and a separately accepted
architecture that keeps Copilot CLI as the sole live-model process.

## Historical evidence

[Autonomous Phase Evaluation 001 Results](autonomous-phase-001-results.md)
remains immutable protocol-invalidated debugging evidence. None of its
comparative counts are carried into protocol v2.
