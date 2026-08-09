using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Tests;

public sealed class MagenticPhaseComparisonTests
{
    [Fact]
    public async Task MagenticArm_PreservesMacroContractAndCapturesReplanning()
    {
        var request = new ReferencePipelineRequest(
            $"comparison-{Guid.NewGuid():N}",
            "Synthetic release readiness");
        ReferencePipelineRuntime harness = CreateOuterRuntime(
            request,
            ReferenceSynthesisExecutorKind.Harness);
        ReferencePipelineRuntime magentic = CreateOuterRuntime(
            request,
            ReferenceSynthesisExecutorKind.Magentic);

        ReferencePipelineResult harnessResult = await RunOuterAsync(
            harness,
            captureCheckpoint: false);
        ReferencePipelineResult magenticResult = await RunOuterAsync(
            magentic,
            captureCheckpoint: false);

        Assert.True(
            harnessResult.Outcome == magenticResult.Outcome,
            $"Magentic synthesis outcome: {magenticResult.Synthesis.Outcome}; " +
            $"error: {magenticResult.Synthesis.Error}; " +
            $"warnings: {string.Join(" | ", magentic.MagenticProbe?.Warnings ?? [])}; " +
            $"failures: {string.Join(" | ", magentic.MagenticProbe?.Failures ?? [])}");
        Assert.Equal(
            harnessResult.Branches.Select(branch => branch.Outcome),
            magenticResult.Branches.Select(branch => branch.Outcome));
        Assert.Equal(
            harnessResult.Synthesis.Artifact?.Digest,
            magenticResult.Synthesis.Artifact?.Digest);
        MagenticPhaseProbe probe = Assert.IsType<MagenticPhaseProbe>(
            magentic.MagenticProbe);
        Assert.Single(probe.Plans);
        Assert.Single(probe.Replans);
        Assert.Equal(3, probe.Ledgers.Count);
        Assert.Equal(3, probe.ProgressEventCount);
        Assert.True(probe.Ledgers[0].IsInLoop);
        Assert.False(probe.Ledgers[0].IsProgressBeingMade);
        Assert.False(probe.Ledgers[0].IsRequestSatisfied);
        Assert.False(probe.Ledgers[1].IsInLoop);
        Assert.True(probe.Ledgers[1].IsProgressBeingMade);
        Assert.False(probe.Ledgers[1].IsRequestSatisfied);
        Assert.True(probe.Ledgers[2].IsRequestSatisfied);
        Assert.Equal(1, probe.ExecutionCount);
        Assert.Null(magentic.SynthesisClient);

        var (_, runId, _, _) = CreateAcceptedMagenticTask();
        MagenticPhaseRuntime phaseRuntime =
            MagenticPhaseFactory.CreateStallThenRecover(
                new ReferenceArtifactStore(),
                runId,
                new MagenticPhaseProbe(),
                requirePlanSignoff: false);
        Assert.Equal(8, phaseRuntime.MaxRounds);
        Assert.Equal(0, phaseRuntime.MaxStalls);
        Assert.Equal(2, phaseRuntime.MaxResets);
        Assert.Equal(
            [
                MagenticPhaseFactory.ManagerName,
                MagenticPhaseFactory.ManifestAnalystName,
                MagenticPhaseFactory.ContractCriticName,
            ],
            phaseRuntime.Agents.Select(agent => agent.Name));
    }

    [Fact]
    public async Task PlanReview_RevisionThenApproval_EmitsReplanAndCompletes()
    {
        var (artifacts, runId, task, _) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateRevisionThenApprove(
                artifacts,
                runId,
                probe);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        var environment = InProcessExecution.Lockstep.WithCheckpointing(
            checkpoints);

        MagenticRunSlice first;
        await using (StreamingRun initial = await OpenAsync(
            runtime,
            environment,
            task))
        {
            first = await DrainAsync(initial, probe);
        }
        ExternalRequest initialRequest = Assert.IsType<ExternalRequest>(
            first.Request);
        MagenticPlanReviewRequest initialReview =
            initialRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Initial plan review was unavailable.");

        MagenticRunSlice second;
        await using (StreamingRun revised =
            await environment.ResumeStreamingAsync(
                runtime.Workflow,
                Assert.IsType<CheckpointInfo>(first.Checkpoint),
                TestContext.Current.CancellationToken))
        {
            await revised.SendResponseAsync(
                initialRequest.CreateResponse(
                    initialReview.Revise(
                        "Include explicit artifact-contract validation.")));
            second = await DrainAsync(revised, probe);
        }
        ExternalRequest revisedRequest = Assert.IsType<ExternalRequest>(
            second.Request);
        MagenticPlanReviewRequest revisedReview =
            revisedRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Revised plan review was unavailable.");
        Assert.Contains(
            "Revised approved plan",
            revisedReview.Plan.Text,
            StringComparison.Ordinal);

        MagenticRunSlice completed;
        await using (StreamingRun approved =
            await environment.ResumeStreamingAsync(
                runtime.Workflow,
                Assert.IsType<CheckpointInfo>(second.Checkpoint),
                TestContext.Current.CancellationToken))
        {
            await approved.SendResponseAsync(
                revisedRequest.CreateResponse(
                    revisedReview.Approve()));
            completed = await DrainAsync(approved, probe);
        }

        Assert.NotNull(completed.Output);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                completed.Output[^1].Text,
                out _));
        Assert.Single(probe.Plans);
        Assert.Single(probe.Replans);
    }

    [Fact]
    public async Task StallReplanCheckpoint_RestoresWithStableFreshWorkflow()
    {
        var (artifacts, runId, task, _) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        MagenticPhaseRuntime initialRuntime =
            MagenticPhaseFactory.CreateStallThenRecover(
                artifacts,
                runId,
                probe,
                requirePlanSignoff: true);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        var environment = InProcessExecution.Lockstep.WithCheckpointing(
            checkpoints);

        MagenticRunSlice first;
        await using (StreamingRun initial = await OpenAsync(
            initialRuntime,
            environment,
            task))
        {
            first = await DrainAsync(initial, probe);
        }
        ExternalRequest initialRequest = Assert.IsType<ExternalRequest>(
            first.Request);
        MagenticPlanReviewRequest initialReview =
            initialRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Initial plan review was unavailable.");

        MagenticRunSlice replanned;
        await using (StreamingRun afterInitialApproval =
            await environment.ResumeStreamingAsync(
                initialRuntime.Workflow,
                Assert.IsType<CheckpointInfo>(first.Checkpoint),
                TestContext.Current.CancellationToken))
        {
            await afterInitialApproval.SendResponseAsync(
                initialRequest.CreateResponse(
                    initialReview.Approve()));
            replanned = await DrainAsync(
                afterInitialApproval,
                probe);
        }
        ExternalRequest replanRequest = Assert.IsType<ExternalRequest>(
            replanned.Request);
        MagenticPlanReviewRequest replanReview =
            replanRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Replanned review was unavailable.");
        Assert.True(replanReview.IsStalled);
        Assert.Single(probe.Replans);

        MagenticPhaseRuntime recoveredRuntime =
            MagenticPhaseFactory.CreateReplanRecovery(
                artifacts,
                runId,
                probe);
        MagenticRunSlice completed;
        await using (StreamingRun recovered =
            await environment.ResumeStreamingAsync(
                recoveredRuntime.Workflow,
                Assert.IsType<CheckpointInfo>(replanned.Checkpoint),
                TestContext.Current.CancellationToken))
        {
            await recovered.SendResponseAsync(
                replanRequest.CreateResponse(
                    replanReview.Approve()));
            completed = await DrainAsync(
                recovered,
                probe);
        }

        Assert.NotNull(completed.Output);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                completed.Output[^1].Text,
                out _));
        Assert.True(
            recoveredRuntime.ManifestAnalystClient.CallCount > 0);
    }

    [Fact]
    public async Task InvalidSpeaker_UpstreamFinalizesButAdapterRejects()
    {
        var (artifacts, runId, task, _) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateInvalidSpeaker(
                artifacts,
                runId,
                probe);

        MagenticPhaseRunResult result =
            await MagenticPhaseRunner.RunAsync(
                runtime,
                task,
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid-next-speaker", result.FailureCode);
        Assert.Equal(ReferenceSynthesisArtifacts.Valid, result.FinalText);
        Assert.Contains(
            probe.Warnings,
            warning => warning.Contains(
                "Invalid next speaker",
                StringComparison.Ordinal));
        Assert.Equal(0, runtime.ManifestAnalystClient.CallCount);
        Assert.Equal(0, runtime.ContractCriticClient.CallCount);
    }

    [Fact]
    public async Task InvalidMagenticFinal_IsRejectedByExistingArtifactBoundary()
    {
        var (artifacts, runId, task, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateInvalidFinalArtifact(
                artifacts,
                runId,
                probe);
        MagenticPhaseRunResult phaseResult =
            await MagenticPhaseRunner.RunAsync(
                runtime,
                task,
                TestContext.Current.CancellationToken);
        Assert.True(phaseResult.Succeeded);

        var candidate = ReferencePhaseArtifact.Candidate(
            SynthesisPhaseExecutor.Phase,
            ordinal: 200,
            required: true,
            phaseResult.FinalText!,
            manifestReference);
        var boundary = new SynthesisArtifactBoundaryExecutor(
            artifacts,
            runId);
        ReferencePhaseArtifact validated = await boundary.HandleAsync(
            candidate,
            context: null!,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ReferencePipelineOutcome.Failed,
            validated.Outcome);
        Assert.Equal("missing-recommendation", validated.Error);
    }

    [Fact]
    public async Task OuterCheckpointRestore_ReplaysOnlyMagenticAndDeduplicatesDelivery()
    {
        var request = new ReferencePipelineRequest(
            $"magentic-restore-{Guid.NewGuid():N}",
            "Synthetic release readiness");
        ReferencePipelineRuntime runtime = CreateOuterRuntime(
            request,
            ReferenceSynthesisExecutorKind.Magentic);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();

        await using StreamingRun run = await StartOuterAsync(
            runtime,
            checkpoints);
        var (initial, checkpoint) = await ObserveOuterAsync(
            run,
            captureCheckpoint: true);
        Assert.NotNull(checkpoint);
        int researchCalls = runtime.ResearchClient.CallCount;
        int riskResponses = runtime.RiskClient.ArtifactResponseCount;
        int operationsResponses =
            runtime.OperationsClient.ArtifactResponseCount;

        await run.RestoreCheckpointAsync(
            checkpoint,
            TestContext.Current.CancellationToken);
        var (restored, _) = await ObserveOuterAsync(
            run,
            captureCheckpoint: false);

        Assert.Equal(researchCalls, runtime.ResearchClient.CallCount);
        Assert.Equal(
            riskResponses,
            runtime.RiskClient.ArtifactResponseCount);
        Assert.Equal(
            operationsResponses,
            runtime.OperationsClient.ArtifactResponseCount);
        Assert.Equal(2, runtime.MagenticProbe?.ExecutionCount);
        Assert.Equal(2, runtime.Delivery.AttemptCount);
        Assert.Equal(1, runtime.Delivery.AuthoritativeCount);
        Assert.Equal(
            initial.Synthesis.Artifact?.Digest,
            restored.Synthesis.Artifact?.Digest);
    }

    private static ReferencePipelineRuntime CreateOuterRuntime(
        ReferencePipelineRequest request,
        ReferenceSynthesisExecutorKind synthesisKind) =>
        ReferencePipelineFactory.Create(
            request,
            ReferencePipelineOptions.Default with
            {
                SynthesisExecutorKind = synthesisKind,
            },
            new ReferenceArtifactStore(),
            new IdempotentDeliverySink());

    private static async Task<ReferencePipelineResult> RunOuterAsync(
        ReferencePipelineRuntime runtime,
        bool captureCheckpoint)
    {
        await using StreamingRun run = await StartOuterAsync(
            runtime,
            CheckpointManager.CreateInMemory());
        var (result, _) = await ObserveOuterAsync(
            run,
            captureCheckpoint);
        return result;
    }

    private static Task<StreamingRun> StartOuterAsync(
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
        CheckpointInfo? Checkpoint)> ObserveOuterAsync(
        StreamingRun run,
        bool captureCheckpoint)
    {
        ReferencePipelineResult? result = null;
        CheckpointInfo? checkpoint = null;
        bool gateCompleted = false;
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            TestContext.Current.CancellationToken))
        {
            if (captureCheckpoint &&
                workflowEvent is ExecutorCompletedEvent
                {
                    ExecutorId: SpecialistManifestBarrierExecutor.ExecutorId,
                })
            {
                gateCompleted = true;
            }

            if (checkpoint is null &&
                gateCompleted &&
                workflowEvent is SuperStepCompletedEvent
                {
                    CompletionInfo.Checkpoint: { } currentCheckpoint,
                })
            {
                checkpoint = currentCheckpoint;
                gateCompleted = false;
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
                    "Outer reference pipeline failed.",
                    error.Exception);
            }
        }

        return (
            result ?? throw new InvalidOperationException(
                "Outer reference pipeline produced no result."),
            checkpoint);
    }

    private static async Task<StreamingRun> OpenAsync(
        MagenticPhaseRuntime runtime,
        Microsoft.Agents.AI.Workflows.InProc.InProcessExecutionEnvironment environment,
        string task)
    {
        StreamingRun run = await environment.OpenStreamingAsync(
            runtime.Workflow);
        bool inputSent = await run.TrySendMessageAsync(
            new List<ChatMessage>
            {
                new(ChatRole.User, task),
            });
        bool turnSent = await run.TrySendMessageAsync(
            new TurnToken(emitEvents: true));
        Assert.True(inputSent);
        Assert.True(turnSent);
        return run;
    }

    private static async Task<MagenticRunSlice> DrainAsync(
        StreamingRun run,
        MagenticPhaseProbe probe)
    {
        List<ChatMessage>? output = null;
        CheckpointInfo? checkpoint = null;
        ExternalRequest? request = null;
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            TestContext.Current.CancellationToken))
        {
            probe.Record(workflowEvent);
            if (workflowEvent is WorkflowOutputEvent currentOutput &&
                currentOutput.Is<List<ChatMessage>>())
            {
                output = currentOutput.As<List<ChatMessage>>();
            }

            if (workflowEvent is SuperStepCompletedEvent
                {
                    CompletionInfo.Checkpoint: { } currentCheckpoint,
                })
            {
                checkpoint = currentCheckpoint;
            }

            if (workflowEvent is RequestInfoEvent requestInfo)
            {
                request = requestInfo.Request;
            }

            if (workflowEvent is WorkflowErrorEvent error)
            {
                throw new InvalidOperationException(
                    "Magentic workflow failed.",
                    error.Exception);
            }
        }

        return new MagenticRunSlice(
            output,
            checkpoint,
            request);
    }

    private static (
        ReferenceArtifactStore Artifacts,
        string RunId,
        string Task,
        ReferenceArtifactReference Manifest) CreateAcceptedMagenticTask()
    {
        string runId = $"magentic-{Guid.NewGuid():N}";
        var artifacts = new ReferenceArtifactStore();
        ReferenceArtifactReference research = artifacts.Write(
            runId,
            """{"topic":"synthetic","evidence":["research"]}""");
        ReferenceArtifactReference risk = artifacts.Write(
            runId,
            """{"summary":"risk","evidence":["risk"]}""");
        ReferenceArtifactReference operations = artifacts.Write(
            runId,
            """{"summary":"operations","evidence":["operations"]}""");
        ReferencePhaseArtifact[] branches =
        [
            ReferencePhaseArtifact.WithArtifact(
                ReferencePhaseArtifact.Candidate(
                    "risk",
                    0,
                    true,
                    "{}",
                    research),
                risk),
            ReferencePhaseArtifact.WithArtifact(
                ReferencePhaseArtifact.Candidate(
                    "operations",
                    1,
                    false,
                    "{}",
                    research),
                operations),
        ];
        var manifest = new ReferenceArtifactManifest(
            runId,
            research,
            branches,
            ReferencePipelineOutcome.Completed,
            []);
        ReferenceArtifactReference manifestReference =
            artifacts.WriteManifest(manifest);
        string task = ReferenceSynthesisPrompt.Build(
            manifestReference,
            manifest);
        return (
            artifacts,
            runId,
            task,
            manifestReference);
    }
}
