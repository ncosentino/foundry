# Contract: Harness Diagnostics and Progress

## Purpose

Preserve one coherent Foundry diagnostics model across upstream Harness and
Foundry middleware.

## Required event categories

- Harness profile resolved
- Session created, restored, or rejected
- Model invocation
- Tool invocation and result
- Approval requested, granted, denied, or restored
- Todo or mode transition
- Compaction started, completed, failed, or declined
- Tool result offloaded
- Artifact rehydrated, stale, missing, unauthorized, or refused
- Background work started, updated, completed, failed, or cancelled
- Outer loop iteration
- Context irreducible
- Run terminated

## Attribution

Every event carries:

- run identity;
- session identity;
- orchestration identity;
- agent identity;
- step or iteration;
- parent-child relationship;
- capability profile;
- outcome or termination evidence.

## Token and size taxonomy

At minimum:

- ordinary model input;
- ordinary model output;
- compaction input;
- compaction output;
- offload summary;
- rehydration content;
- cached input;
- reasoning tokens where reported.

## Deduplication

- Exactly one layer owns agent-run telemetry.
- Model and tool events are not double-recorded by Foundry and upstream
  OpenTelemetry.
- Suppressed upstream or Foundry instrumentation is recorded in capability
  evidence.
- Deterministic fixtures define the expected event count and relationship graph.

