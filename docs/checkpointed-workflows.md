---
description: Resume Microsoft Agent Framework workflows from caller-owned checkpoints without adding a second Foundry persistence model.
---

# Checkpointed Workflows

Foundry exposes the Microsoft Agent Framework checkpoint lifecycle directly for
agent workflows whose macro topology is fixed in code. The caller supplies the
upstream `CheckpointManager`, observes upstream `CheckpointInfo` values, and
retains the upstream `StreamingRun` handle needed for live restoration.

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

- consuming `WatchStreamAsync`;
- selecting and persisting checkpoints;
- calling `CancelRunAsync` when execution should stop;
- restoring or resuming;
- disposing the run; and
- the lifetime and backing store of the `CheckpointManager`.

Foundry does not wrap checkpoint bytes or define another storage interface.

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

## Resume after failure or process restart

A failed or disposed run cannot be used as the live restore handle. Rebuild a
structurally compatible workflow and resume through the same checkpoint manager:

```csharp
Workflow recoveredWorkflow = BuildWorkflowWithTheSameStableIds();

await using StreamingRun recoveredRun =
    await recoveredWorkflow.ResumeCheckpointedAgentRunAsync(
        acceptedArtifactCheckpoint,
        checkpointManager,
        cancellationToken);
```

The rebuilt workflow must retain the same topology and stable executor IDs.
For agents created from `IChatClient`, set `ChatClientAgentOptions.Id`
explicitly. Random executor IDs make a fresh workflow incompatible with the
stored checkpoint and MAF throws `InvalidDataException`.

Successful same-handle restoration is not proof that a newly constructed
workflow will skip the same work. Exercise the fresh-workflow resume path when
process recovery matters.

Process restart also requires a durable caller-owned checkpoint store.
`CheckpointManager.CreateInMemory()` supports recovery only while that in-memory
store remains available.

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
not be treated as a live restore handle; resume a compatible fresh workflow from
a previously persisted checkpoint instead.

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
phase fails once, a fresh compatible workflow resumes from the accepted artifact
boundary, the accepted producer phase is not called again, and one recovered
terminal output is observed.

```bash
dotnet run --project src/Examples/AgentFramework/CheckpointWorkflowApp
```
