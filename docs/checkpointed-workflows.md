---
description: Resume Microsoft Agent Framework workflows from caller-owned checkpoints without adding a second Foundry persistence model.
---

# Checkpointed Workflows

Foundry exposes the Microsoft Agent Framework checkpoint lifecycle directly for
agent workflows whose macro topology is fixed in code. The caller supplies the
upstream `CheckpointManager`, observes upstream `CheckpointInfo` values, and
retains the upstream `StreamingRun` handle needed for live restoration.

The [Fixed-Macro Autonomous Phases](autonomous-phase-pipeline.md) reference
shows checkpoint selection after an all-settled artifact manifest and before
autonomous synthesis.

This surface belongs to
`NexusLabs.Foundry.MicrosoftAgentFramework.Workflows`:

```xml
<PackageReference Include="NexusLabs.Foundry.MicrosoftAgentFramework.Workflows" />
```

## Start a checkpointed run

`StartCheckpointedAgentRunAsync` opens a checkpoint-enabled in-process run,
sends the initial user message, and sends the `TurnToken` required by MAF agent
workflow executors:

```csharp
CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

await using StreamingRun run =
    await workflow.StartCheckpointedAgentRunAsync(
        "Create the release brief.",
        checkpointManager,
        "release-brief-42",
        cancellationToken);
```

The returned object is the raw upstream `StreamingRun`. The caller owns:

- consuming `WatchStreamAsync` through one active event-loop owner;
- selecting and persisting checkpoints;
- calling `CancelRunAsync` when execution should stop;
- restoring the live run;
- disposing the run; and
- the lifetime and backing store of the `CheckpointManager`.

Foundry does not wrap checkpoint bytes or define another storage interface.

`WatchStreamAsync` permits only one active enumerator. A second concurrent
consumer throws `InvalidOperationException`. Hosts that need multiple event
consumers must appoint one event-loop owner and multiplex the observed events
to diagnostics, progress, checkpoint selection, and other subscribers.

## Select an accepted artifact boundary

MAF creates a checkpoint after each completed super-step. Observe it on
`SuperStepCompletedEvent`:

```csharp
CheckpointInfo? acceptedArtifactCheckpoint = null;
bool acceptedArtifactBoundaryCompleted = false;

await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(cancellationToken))
{
    if (workflowEvent is ExecutorCompletedEvent
        {
            ExecutorId: "accepted-artifact-boundary",
        })
    {
        acceptedArtifactBoundaryCompleted = true;
    }

    if (acceptedArtifactCheckpoint is null &&
        acceptedArtifactBoundaryCompleted &&
        workflowEvent is SuperStepCompletedEvent
        {
            CompletionInfo.Checkpoint: { } checkpoint,
        })
    {
        acceptedArtifactCheckpoint = checkpoint;
        acceptedArtifactBoundaryCompleted = false;
    }
}
```

A super-step checkpoint records workflow state, not an application-level claim
that an artifact is acceptable. Put a deterministic validation or acceptance
executor after an autonomous phase, then select the checkpoint completed after
that boundary. This separates:

- the agent's nondeterministic internal work;
- deterministic artifact validation; and
- the downstream phase that may be replayed.

The runnable example uses a stable `ChatForwardingExecutor` as the minimal
boundary:

```csharp
ExecutorBinding producer = new AIAgentBinding(producerAgent, agentOptions);
ExecutorBinding acceptedArtifactBoundary =
    new ChatForwardingExecutor("accepted-artifact-boundary").BindExecutor();
ExecutorBinding synthesis = new AIAgentBinding(synthesisAgent, agentOptions);

Workflow workflow = new WorkflowBuilder(producer)
    .AddEdge(producer, acceptedArtifactBoundary)
    .AddEdge(acceptedArtifactBoundary, synthesis)
    .WithOutputFrom(synthesis)
    .Build();
```

Production boundaries normally validate a typed artifact or persisted artifact
reference rather than forwarding chat messages unchanged.

## Restore the same run

While a run remains usable, restore a selected checkpoint on the same handle:

```csharp
await run.RestoreCheckpointAsync(
    acceptedArtifactCheckpoint,
    cancellationToken);

await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(cancellationToken))
{
    // Events describe only the restored execution segment.
}
```

The restored event stream is not a cumulative replay of the original stream.
Diagnostics middleware records the provider calls that actually execute after
the restore. Existing terminal helpers such as `RunWithDiagnosticsAsync` do not
expose a restorable handle and remain unchanged.

## Fresh-workflow resume is not exposed

MAF 1.17 includes `InProcessExecution.ResumeStreamingAsync`, but Foundry does
not expose a convenience wrapper for it. Two upstream behaviors make a
fresh-workflow recovery contract unsafe:

- resume takes ownership of the supplied `Workflow` before checkpoint
  compatibility is validated, and a failed resume returns no run handle that
  can release that ownership; and
- checkpointed state for fan-in and fan-out edges is keyed by an `EdgeId`
  assigned from builder insertion order, while compatibility validation
  compares edge structure without comparing those IDs.

An equivalent workflow rebuilt with a different edge insertion order can
therefore pass compatibility validation while mapping stateful edge data to a
different edge. A durable checkpoint store does not resolve either problem.

Keep the original `StreamingRun` alive when same-process replay is required.
If that handle becomes unusable or is disposed, or if the process restarts,
recover from application-owned accepted artifacts and idempotency records.
Foundry will not claim fresh-process workflow recovery until the upstream
ownership and edge-identity contracts are safe.

## Delivery and side-effect semantics

Checkpoint recovery is at-least-once:

- work after the selected checkpoint may execute again;
- output events from the restored segment may repeat output observed on the
  superseded timeline;
- external writes performed after the checkpoint may repeat; and
- the checkpoint does not roll back external systems.

Use idempotency keys for tools and delivery, persist accepted artifacts under
stable identities, and deduplicate terminal publication outside the workflow.
The Foundry API does not claim exactly-once execution.

## Cancellation

The cancellation token passed to `WatchStreamAsync` controls only that event
consumer. In MAF 1.17, canceling it ends observation normally and leaves the
workflow running. A later watcher can continue observing the run.

Call `StreamingRun.CancelRunAsync` to cancel execution. A canceled run should
not be treated as a live restore handle. Recover from application-owned
accepted artifacts rather than assuming the MAF checkpoint can open a new run.

## NativeAOT

The checkpoint example is configured for NativeAOT and hosted CI publishes and
executes it on Linux. Its workflow uses only upstream built-in message and state
types.

Custom executor messages and checkpointed state still need source-generated
`System.Text.Json` metadata. A checkpoint store that reflectively serializes
unknown application types can remain incompatible with trimming or NativeAOT
even though these Foundry extensions are compatible.

## Runnable example

`src/Examples/AgentFramework/CheckpointWorkflowApp` runs offline. Its downstream
phase completes once, the same live run restores the accepted artifact
checkpoint, the accepted producer phase is not called again, and one restored
terminal output is observed.

```bash
dotnet run --project src/Examples/AgentFramework/CheckpointWorkflowApp
```
