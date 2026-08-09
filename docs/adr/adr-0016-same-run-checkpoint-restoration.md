---
title: "ADR-0016: Same-run checkpoint restoration"
status: "Accepted"
date: "2026-08-10"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "agent-framework", "workflows", "checkpointing", "recovery"]
supersedes: "adr-0015-upstream-first-checkpointed-workflow-runs.md"
superseded_by: ""
---

# ADR-0016: Same-run checkpoint restoration

## Context and scope

ADR-0015 exposed both checkpoint-enabled workflow start and fresh-workflow
resume. Review against Microsoft Agent Framework 1.17 found that the start and
same-run restore paths have a defensible contract, but fresh-workflow resume
does not.

MAF takes ownership of a supplied `Workflow` while constructing the runner,
before it validates checkpoint compatibility. If compatibility validation
throws, no `StreamingRun` is returned and the caller has no handle through
which to dispose the runner. The supplied workflow therefore remains owned and
cannot be used by another non-concurrent runner.

MAF also assigns internal edge IDs from `WorkflowBuilder` insertion order.
Checkpoint state for stateful fan-in and fan-out edges is keyed by those IDs,
while workflow compatibility compares edge kinds and connections without
comparing the IDs. Equivalent rebuilt workflows can therefore pass
compatibility validation even when an insertion-order change maps restored
state to a different edge.

This decision governs Foundry's public checkpoint convenience API in
`NexusLabs.Foundry.MicrosoftAgentFramework.Workflows`. It does not prohibit
direct use of upstream experimental APIs by consumers that accept their
current risks.

## Decision drivers

- Expose only recovery behavior whose ownership and state-identity contracts
  can be defended.
- Preserve MAF as the checkpoint implementation rather than adding a parallel
  Foundry persistence format.
- Avoid a compatibility shim around upstream experimental behavior.
- Keep accepted autonomous phases behind deterministic artifact boundaries.
- State event-consumer ownership and process-recovery limitations explicitly.

## Decision

Foundry retains the two `StartCheckpointedAgentRunAsync` overloads and removes
its `ResumeCheckpointedAgentRunAsync` convenience.

The start methods return the raw upstream `StreamingRun`. While that handle
remains usable, callers may select a checkpoint and call
`StreamingRun.RestoreCheckpointAsync` to replay the same run from that point.
The caller continues to own checkpoint selection, execution cancellation,
restoration, disposal, and the checkpoint manager.

One component must own the active `WatchStreamAsync` enumeration. MAF 1.17
rejects a second concurrent watcher. Hosts that need multiple observers must
multiplex events from the single event loop.

Foundry does not claim recovery from a disposed or unusable run, or across a
process restart. Applications must persist accepted artifacts and idempotency
records outside the workflow so they can reconstruct downstream work without
depending on unsafe fresh-workflow checkpoint restoration.

Recovery remains at-least-once. Output events and external side effects after
the selected checkpoint can occur again. Tools, artifact persistence, and
terminal delivery must therefore be idempotent or deduplicated.

## Alternatives considered

### Keep fresh-workflow resume with warnings

This would preserve the widest surface, but warnings cannot prevent a failed
resume from consuming a workflow or prevent compatible-looking edge reorderings
from restoring state to the wrong edge. The resulting API would imply a
recovery guarantee Foundry cannot enforce.

### Add Foundry-owned workflow fingerprints and edge-state translation

Foundry could persist a second topology manifest, prevalidate ownership, and
translate edge state. This was rejected because it would duplicate and couple
to MAF's internal checkpoint format, creating the compatibility layer this
integration is intended to avoid.

### Remove all checkpoint conveniences

This would avoid every upstream limitation but would also discard the proven
start and same-run restore lifecycle. The narrower API keeps useful replay
behavior without extending it into unsupported process recovery.

## Consequences

### Positive

- A failed Foundry resume helper cannot strand a caller's workflow instance.
- Foundry does not certify insertion-order-dependent state restoration.
- The public surface remains upstream-first and small.
- Same-run replay after deterministic artifact acceptance remains available.
- Event ownership is explicit for hosts with diagnostics or progress fan-out.

### Negative

- Foundry no longer offers a fresh-process checkpoint resume convenience.
- Callers must keep the live run handle for MAF checkpoint replay.
- Application-owned artifact recovery is required after process loss.
- Consumers using upstream resume directly own its compatibility risks.

### Neutral

- The checkpoint manager and store remain caller-owned.
- Existing non-checkpointed workflow helpers are unchanged.
- Workflow topology remains developer-authored.
- Durable distributed execution remains out of scope.

## Confirmation

The decision is confirmed by:

- sequential and concurrent checkpoint-start tests;
- same-run restore after an accepted artifact boundary;
- a contract test proving that a second concurrent watcher is rejected;
- observer cancellation followed by later observation;
- diagnostics assertions showing only replayed provider calls;
- one restored terminal output without replaying the accepted producer phase;
- an offline runnable example; and
- hosted NativeAOT publication and execution of that example.

## References

- Supersedes ADR-0015.
- [MAF 1.17 `InProcessExecutionEnvironment`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/InProc/InProcessExecutionEnvironment.cs)
- [MAF 1.17 `InProcessRunner`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/InProc/InProcessRunner.cs)
- [MAF 1.17 `WorkflowInfo`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/Checkpointing/WorkflowInfo.cs)
- [MAF 1.17 `WorkflowBuilder`](https://github.com/microsoft/agent-framework/blob/dotnet-1.17.0/dotnet/src/Microsoft.Agents.AI.Workflows/WorkflowBuilder.cs)
