using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Tests;

public sealed class ReferencePipelineTests
{
    [Fact]
    public async Task Success_UsesHarnessDelegationAndConcurrentAllSettledFanOut()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

        await using StreamingRun run = await StartAsync(
            runtime,
            checkpoints);
        var (result, checkpoint) = await ObserveAsync(
            run,
            captureCheckpoint: true);

        Assert.NotNull(checkpoint);
        Assert.Equal(ReferencePipelineOutcome.Completed, result.Outcome);
        Assert.All(
            result.Branches,
            branch => Assert.Equal(
                ReferencePipelineOutcome.Completed,
                branch.Outcome));
        Assert.Equal(6, runtime.HarnessAgents.Count);
        Assert.Equal(2, runtime.BackgroundGate.MaximumConcurrency);
        Assert.Equal(2, runtime.SpecialistGate.MaximumConcurrency);
        Assert.Equal(
            2,
            runtime.ResearchClient.InvokedToolNames.Count(
                name => name == "background_agents_start_task"));
        Assert.Equal(
            2,
            runtime.ResearchClient.InvokedToolNames.Count(
                name => name == "background_agents_wait_for_first_completion"));
        Assert.Equal(
            2,
            runtime.ResearchClient.InvokedToolNames.Count(
                name => name == "background_agents_get_task_results"));
        Assert.Equal(
            2,
            runtime.ResearchClient.InvokedToolNames.Count(
                name => name == "background_agents_clear_completed_task"));
        Assert.Equal(1, runtime.EvidenceWorkerClient.CallCount);
        Assert.Equal(1, runtime.FeasibilityWorkerClient.CallCount);
        Assert.Equal(1, runtime.Delivery.AuthoritativeCount);
        Assert.Contains(
            "manifest_id=",
            runtime.SynthesisClient.InitialPrompts[0],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "evidence-complete",
            runtime.SynthesisClient.InitialPrompts[0],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "risk-evidence",
            runtime.SynthesisClient.InitialPrompts[0],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionalFailure_ProducesPartialManifestAndPreservesRequiredArtifact()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default with
            {
                FailOptionalSpecialist = true,
            });

        await using StreamingRun run = await StartAsync(
            runtime,
            CheckpointManager.CreateInMemory());
        var (result, _) = await ObserveAsync(
            run,
            captureCheckpoint: false);

        Assert.Equal(ReferencePipelineOutcome.Partial, result.Outcome);
        ReferencePhaseArtifact risk = Assert.Single(
            result.Branches,
            branch => branch.Phase == "risk");
        ReferencePhaseArtifact operations = Assert.Single(
            result.Branches,
            branch => branch.Phase == "operations");
        Assert.Equal(ReferencePipelineOutcome.Completed, risk.Outcome);
        Assert.NotNull(risk.Artifact);
        Assert.Equal(ReferencePipelineOutcome.Failed, operations.Outcome);
        Assert.Contains(
            result.Gaps,
            gap => gap.Contains(
                "operations",
                StringComparison.Ordinal));
        Assert.Equal(result.Gaps, result.Synthesis.Gaps);
        Assert.Equal(
            ReferencePipelineOutcome.Completed,
            result.Synthesis.Outcome);
    }

    [Fact]
    public async Task RequiredFailure_SkipsSynthesisAndDeliversFailedOutcome()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default with
            {
                FailRequiredSpecialist = true,
            });

        await using StreamingRun run = await StartAsync(
            runtime,
            CheckpointManager.CreateInMemory());
        var (result, _) = await ObserveAsync(
            run,
            captureCheckpoint: false);

        Assert.Equal(ReferencePipelineOutcome.Failed, result.Outcome);
        Assert.Contains(
            result.Branches,
            branch =>
                branch.Phase == "risk" &&
                branch.Outcome == ReferencePipelineOutcome.Failed);
        Assert.Contains(
            result.Branches,
            branch =>
                branch.Phase == "operations" &&
                branch.Outcome == ReferencePipelineOutcome.Completed);
        Assert.Equal(
            ReferencePipelineOutcome.Skipped,
            result.Synthesis.Outcome);
        Assert.Equal(0, runtime.SynthesisClient.CallCount);
        Assert.Equal(1, runtime.Delivery.AuthoritativeCount);
    }

    [Fact]
    public async Task SynthesisLoop_CorrectsInvalidArtifactWithoutRerunningEarlierPhases()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default);

        await using StreamingRun run = await StartAsync(
            runtime,
            CheckpointManager.CreateInMemory());
        var (result, _) = await ObserveAsync(
            run,
            captureCheckpoint: false);

        Assert.Equal(ReferencePipelineOutcome.Completed, result.Outcome);
        Assert.Equal(1, runtime.EvidenceWorkerClient.CallCount);
        Assert.Equal(1, runtime.FeasibilityWorkerClient.CallCount);
        Assert.Equal(1, runtime.RiskClient.ArtifactResponseCount);
        Assert.Equal(1, runtime.OperationsClient.ArtifactResponseCount);
        Assert.Equal(2, runtime.SynthesisClient.ArtifactResponseCount);
        Assert.Single(runtime.SynthesisClient.InitialPrompts);
    }

    [Fact]
    public async Task Cancellation_StopsConcurrentSpecialistsWithoutDelivery()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default with
            {
                HoldSpecialistsUntilCancellation = true,
            });
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

        await using StreamingRun run = await StartAsync(
            runtime,
            checkpoints);
        Task<IReadOnlyList<WorkflowEvent>> observation = CollectEventsAsync(
            run);
        await runtime.SpecialistGate.AllEntered.WaitAsync(
            TestContext.Current.CancellationToken);

        await run.CancelRunAsync();
        IReadOnlyList<WorkflowEvent> events = await observation.WaitAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(2, runtime.SpecialistGate.EnteredCount);
        Assert.Equal(2, runtime.SpecialistGate.MaximumConcurrency);
        Assert.Equal(0, runtime.Delivery.AttemptCount);
        Assert.DoesNotContain(
            events,
            workflowEvent => workflowEvent is WorkflowOutputEvent
            {
                Data: ReferencePipelineResult,
            });
    }

    [Fact]
    public async Task Cancellation_DuringBackgroundDelegationDoesNotCancelChildren()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default with
            {
                HoldBackgroundWorkersUntilRelease = true,
            });
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

        await using StreamingRun run = await StartAsync(
            runtime,
            checkpoints);
        Task<IReadOnlyList<WorkflowEvent>> observation = CollectEventsAsync(
            run);
        await runtime.BackgroundGate.AllEntered.WaitAsync(
            TestContext.Current.CancellationToken);

        Task cancellation = run.CancelRunAsync().AsTask();
        await Task.Delay(
            TimeSpan.FromMilliseconds(50),
            TestContext.Current.CancellationToken);
        Assert.Equal(0, runtime.Delivery.AttemptCount);
        Assert.Equal(2, runtime.BackgroundGate.MaximumConcurrency);
        Assert.False(cancellation.IsCompleted);

        runtime.BackgroundGate.Release();
        await Task.WhenAll(cancellation, observation).WaitAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, runtime.EvidenceWorkerClient.CallCount);
        Assert.Equal(1, runtime.FeasibilityWorkerClient.CallCount);
        Assert.Equal(0, runtime.Delivery.AttemptCount);
    }

    [Fact]
    public async Task RestoreBeforeSynthesis_ReplaysOnlySynthesisAndDeduplicatesDelivery()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

        await using StreamingRun run = await StartAsync(
            runtime,
            checkpoints);
        var (initialResult, checkpoint) = await ObserveAsync(
            run,
            captureCheckpoint: true);
        Assert.NotNull(checkpoint);
        int researchCalls = runtime.ResearchClient.CallCount;
        int riskCalls = runtime.RiskClient.CallCount;
        int operationsCalls = runtime.OperationsClient.CallCount;
        int synthesisArtifacts =
            runtime.SynthesisClient.ArtifactResponseCount;
        int synthesisPrompts =
            runtime.SynthesisClient.InitialPrompts.Count;

        await run.RestoreCheckpointAsync(
            checkpoint,
            TestContext.Current.CancellationToken);
        var (restoredResult, _) = await ObserveAsync(
            run,
            captureCheckpoint: false);

        Assert.Equal(researchCalls, runtime.ResearchClient.CallCount);
        Assert.Equal(riskCalls, runtime.RiskClient.CallCount);
        Assert.Equal(
            operationsCalls,
            runtime.OperationsClient.CallCount);
        Assert.Equal(
            synthesisArtifacts + 2,
            runtime.SynthesisClient.ArtifactResponseCount);
        Assert.Equal(
            synthesisPrompts + 1,
            runtime.SynthesisClient.InitialPrompts.Count);
        Assert.Equal(2, runtime.Delivery.AttemptCount);
        Assert.Equal(1, runtime.Delivery.AuthoritativeCount);
        Assert.Equal(initialResult.DeliveryId, restoredResult.DeliveryId);
        Assert.Equal(
            initialResult.Synthesis.Artifact?.Digest,
            restoredResult.Synthesis.Artifact?.Digest);
    }

    [Fact]
    public async Task DeliveryReplay_ReturnsAuthoritativeResultAndRejectsConflict()
    {
        ReferencePipelineRuntime runtime = CreateRuntime(
            ReferencePipelineOptions.Default);

        await using StreamingRun run = await StartAsync(
            runtime,
            CheckpointManager.CreateInMemory());
        var (result, _) = await ObserveAsync(
            run,
            captureCheckpoint: false);

        ReferencePipelineResult replay = runtime.Delivery.Publish(result);

        Assert.Same(result, replay);
        Assert.Equal(2, runtime.Delivery.AttemptCount);
        Assert.Equal(1, runtime.Delivery.AuthoritativeCount);

        ReferencePipelineResult conflict = result with
        {
            Synthesis = result.Synthesis with
            {
                Gaps = ["conflicting-replay"],
            },
        };
        Assert.Throws<InvalidOperationException>(
            () => runtime.Delivery.Publish(conflict));
        Assert.Equal(1, runtime.Delivery.AuthoritativeCount);
    }

    [Fact]
    public void ArtifactStore_RejectsCrossRunReads()
    {
        var artifacts = new ReferenceArtifactStore();
        ReferenceArtifactReference reference = artifacts.Write(
            "run-a",
            """{"value":"isolated"}""");

        Assert.Throws<UnauthorizedAccessException>(
            () => artifacts.Read("run-b", reference.Id));
    }

    private static ReferencePipelineRuntime CreateRuntime(
        ReferencePipelineOptions options)
    {
        var request = new ReferencePipelineRequest(
            $"reference-{Guid.NewGuid():N}",
            "Synthetic release readiness");
        return ReferencePipelineFactory.Create(
            request,
            options,
            new ReferenceArtifactStore(),
            new IdempotentDeliverySink());
    }

    private static Task<StreamingRun> StartAsync(
        ReferencePipelineRuntime runtime,
        CheckpointManager checkpoints) =>
        InProcessExecution.RunStreamingAsync(
                runtime.Workflow,
                runtime.Request,
                checkpoints,
                $"offline-fixed-macro-v1:{runtime.Request.RunId}",
                TestContext.Current.CancellationToken)
            .AsTask();

    private static async Task<(
        ReferencePipelineResult Result,
        CheckpointInfo? Checkpoint)> ObserveAsync(
        StreamingRun run,
        bool captureCheckpoint)
    {
        ReferencePipelineResult? result = null;
        CheckpointInfo? checkpoint = null;
        bool manifestBoundaryCompleted = false;

        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            if (captureCheckpoint &&
                workflowEvent is ExecutorCompletedEvent
                {
                    ExecutorId: SpecialistManifestBarrierExecutor.ExecutorId,
                })
            {
                manifestBoundaryCompleted = true;
            }

            if (checkpoint is null &&
                manifestBoundaryCompleted &&
                workflowEvent is SuperStepCompletedEvent
                {
                    CompletionInfo.Checkpoint: { } currentCheckpoint,
                })
            {
                checkpoint = currentCheckpoint;
                manifestBoundaryCompleted = false;
            }

            if (workflowEvent is WorkflowOutputEvent
                {
                    Data: ReferencePipelineResult currentResult,
                })
            {
                result = currentResult;
            }

            if (workflowEvent is WorkflowErrorEvent error)
            {
                throw new InvalidOperationException(
                    "Reference pipeline execution failed.",
                    error.Exception);
            }
        }

        return (
            result ?? throw new InvalidOperationException(
                "Reference pipeline produced no result."),
            checkpoint);
    }

    private static async Task<IReadOnlyList<WorkflowEvent>> CollectEventsAsync(
        StreamingRun run)
    {
        var events = new List<WorkflowEvent>();
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            events.Add(workflowEvent);
        }

        return events;
    }
}
