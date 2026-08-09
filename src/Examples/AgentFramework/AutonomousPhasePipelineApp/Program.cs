using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

var request = new ReferencePipelineRequest(
    "offline-reference-run",
    "Synthetic release readiness");
var artifacts = new ReferenceArtifactStore();
var delivery = new IdempotentDeliverySink();
ReferencePipelineRuntime runtime = ReferencePipelineFactory.Create(
    request,
    ReferencePipelineOptions.Default,
    artifacts,
    delivery);
CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    runtime.Workflow,
    request,
    checkpoints,
    $"offline-fixed-macro-v1:{request.RunId}",
    CancellationToken.None);

CheckpointInfo? beforeSynthesis = null;
ReferencePipelineResult? initialResult = null;
bool manifestBoundaryCompleted = false;
await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    if (workflowEvent is ExecutorCompletedEvent
        {
            ExecutorId: SpecialistManifestBarrierExecutor.ExecutorId,
        })
    {
        manifestBoundaryCompleted = true;
    }

    if (beforeSynthesis is null &&
        manifestBoundaryCompleted &&
        workflowEvent is SuperStepCompletedEvent
        {
            CompletionInfo.Checkpoint: { } checkpoint,
        })
    {
        beforeSynthesis = checkpoint;
        manifestBoundaryCompleted = false;
    }

    if (workflowEvent is WorkflowOutputEvent
        {
            Data: ReferencePipelineResult result,
        })
    {
        initialResult = result;
    }

    if (workflowEvent is WorkflowErrorEvent error)
    {
        throw new InvalidOperationException(
            "The initial fixed-macro run failed.",
            error.Exception);
    }
}

if (beforeSynthesis is null || initialResult is null)
{
    throw new InvalidOperationException(
        "The initial run did not produce its checkpoint and result.");
}

await run.RestoreCheckpointAsync(
    beforeSynthesis,
    CancellationToken.None);
ReferencePipelineResult? restoredResult = null;
await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync())
{
    if (workflowEvent is WorkflowOutputEvent
        {
            Data: ReferencePipelineResult result,
        })
    {
        restoredResult = result;
    }

    if (workflowEvent is WorkflowErrorEvent error)
    {
        throw new InvalidOperationException(
            "The restored fixed-macro run failed.",
            error.Exception);
    }
}

if (restoredResult is null ||
    runtime.Delivery.AttemptCount != 2 ||
    runtime.Delivery.AuthoritativeCount != 1)
{
    throw new InvalidOperationException(
        "Checkpoint replay did not preserve idempotent delivery.");
}

Console.WriteLine(
    $"AutonomousPhasePipelineApp:outcome:{restoredResult.Outcome}");
Console.WriteLine(
    $"AutonomousPhasePipelineApp:background-concurrency:{runtime.BackgroundGate.MaximumConcurrency}");
Console.WriteLine(
    $"AutonomousPhasePipelineApp:specialist-concurrency:{runtime.SpecialistGate.MaximumConcurrency}");
Console.WriteLine(
    $"AutonomousPhasePipelineApp:delivery:{restoredResult.DeliveryId}");
Console.WriteLine("AutonomousPhasePipelineApp:completed");
return 0;
