---
title: "ADR-0015: Upstream-first checkpointed workflow runs"
status: "Accepted"
date: "2026-08-08"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "workflows", "checkpointing", "recovery"]
supersedes: ""
superseded_by: "adr-0016-same-run-checkpoint-restoration.md"
---

# ADR-0015: Upstream-first checkpointed workflow runs

## Context and scope

Foundry's existing workflow conveniences run to a terminal result. They do not
accept an upstream `CheckpointManager`, return a restorable run handle, or
define how a caller selects checkpoints from the MAF event stream.

Microsoft Agent Framework 1.17 can checkpoint in-process workflow state at
completed super-step boundaries. `StreamingRun` exposes checkpoint metadata,
live restoration, cancellation, and event observation, while
`InProcessExecution.ResumeStreamingAsync` creates a fresh run from a stored
checkpoint.

The fixed-macro autonomous-phase architecture needs this recovery mechanism,
but it does not need a second Foundry persistence model. This decision governs
the public checkpoint execution path in
`NexusLabs.Foundry.MicrosoftAgentFramework.Workflows`. It does not define a
database, distributed workflow host, exactly-once delivery protocol, or
provider-session persistence.

## Decision drivers

- Preserve MAF as the workflow state and checkpoint implementation.
- Let callers choose the checkpoint manager and backing store.
- Retain the upstream run handle needed for live restore.
- Support failed-run recovery with a fresh compatible workflow.
- Keep accepted autonomous phases behind deterministic artifact boundaries.
- State replay, cancellation, diagnostics, output, compatibility, and
  NativeAOT behavior accurately.
- Avoid extracting a general run abstraction before the reference pipeline and
  hosted evaluation provide evidence for one.

## Decision

Foundry adds three extensions:

- `StartCheckpointedAgentRunAsync` for a string message;
- `StartCheckpointedAgentRunAsync` for a `ChatMessage`; and
- `ResumeCheckpointedAgentRunAsync` for a `CheckpointInfo`.

Each returns the raw upstream `StreamingRun`. Start sends the initial message
and the `TurnToken` required by MAF agent workflow executors. Resume sends
neither because both are represented by restored workflow state.

The caller owns event consumption, checkpoint selection and persistence,
execution cancellation, restoration, disposal, and the checkpoint manager.
Foundry validates direct arguments and disposes a newly opened run if
initialization fails, but otherwise does not intercept the lifecycle.

`StreamingRun.RestoreCheckpointAsync` is the live same-handle path. After a run
has failed or been disposed, the caller rebuilds a structurally compatible
workflow and calls `ResumeCheckpointedAgentRunAsync`. Stable executor IDs and
identical topology are part of that compatibility contract.

A completed MAF super-step is not automatically an accepted application phase.
To prevent replay of an accepted autonomous phase, the macro workflow places a
deterministic artifact validation or acceptance executor after that phase and
selects the checkpoint committed after the boundary. Work queued after that
checkpoint may replay.

Recovery remains at-least-once. Output events and external side effects after
the selected checkpoint can occur again. Tools, artifact persistence, and
terminal delivery must therefore be idempotent or deduplicated. Foundry does
not claim exactly-once execution.

Canceling a `WatchStreamAsync` token stops only that observer. MAF 1.17 ends the
observation normally and leaves execution running. `CancelRunAsync` is the
execution-cancellation operation.

Raw upstream workflow events remain the checkpoint lifecycle authority.
Foundry diagnostics middleware records each provider call that actually runs
on the original or restored segment. Terminal result helpers remain
non-checkpointed because they do not retain a restorable handle.

The built-in checkpoint path is NativeAOT compatible when the workflow state
uses types with available JSON metadata. Custom executor messages and
checkpointed state remain responsible for source-generated
`System.Text.Json` metadata, and custom stores may introduce their own trimming
constraints.

## Alternatives considered

### Return the raw upstream streaming run

This is the chosen option. It is the smallest surface, preserves every upstream
operation, and keeps checkpoint storage replaceable. The tradeoff is that the
caller must process events and coordinate restore explicitly.

### Add a Foundry-owned checkpoint run handle and result model

A wrapper could combine diagnostics, progress, terminal results, and restore
operations. It was rejected for this slice because the stable cross-provider
contract is not yet known, and a wrapper would either hide upstream behavior or
mirror most of `StreamingRun`.

### Add checkpoint overloads to terminal graph runners

This would keep a familiar entry point but cannot expose live restore after the
method has returned a terminal result. It would also conflate the raw MAF BSP
workflow with Foundry's separate `WaitAny` graph execution path.

## Consequences

### Positive

- Checkpoint storage and execution semantics remain upstream-owned.
- Callers can restore a live run or resume a fresh compatible workflow.
- Deterministic artifact boundaries can protect accepted autonomous phases.
- Existing non-checkpointed APIs and result contracts remain unchanged.
- The public surface stays small enough to revise after reference-pipeline
  evidence.

### Negative

- Callers must consume and interpret raw workflow events.
- Stable executor IDs become mandatory for fresh-run recovery.
- Recovery provides no exactly-once guarantee.
- Existing terminal diagnostics helpers cannot be retrofitted onto a run after
  the fact.
- Custom checkpointed state may require explicit NativeAOT serialization
  metadata.

### Neutral

- The checkpoint manager and store remain caller-owned.
- Durable distributed execution remains out of scope.
- Workflow topology is still authored by the developer.
- Phase-local Harness behavior is independent of the outer checkpoint engine.

## Confirmation

The decision is confirmed by:

- sequential and concurrent workflow checkpoint tests;
- live restore after an accepted artifact boundary;
- failed-run recovery through a fresh compatible workflow;
- incompatible-topology rejection;
- observer cancellation that does not cancel execution;
- raw event and diagnostics assertions across restore;
- one recovered terminal output without replaying the accepted producer phase;
- an offline runnable example; and
- hosted NativeAOT publication and execution of that example.

## References

- ADR-0013 keeps Harness loop evaluation phase-local.
- ADR-0014 keeps Harness background delegation phase-local.
- [MAF checkpoint and resume sample](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/samples/03-workflows/Checkpoint/CheckpointAndResume/Program.cs)
- [MAF `StreamingRun`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/StreamingRun.cs)
