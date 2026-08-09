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
        AssertAcceptedSynthesis(harness, harnessResult);
        AssertAcceptedSynthesis(magentic, magenticResult);
        Assert.Equal(
            harness.SynthesisBudget.MaxProviderCalls,
            magentic.SynthesisBudget.MaxProviderCalls);
        Assert.InRange(
            harness.SynthesisBudget.UsedProviderCalls,
            1,
            harness.SynthesisBudget.MaxProviderCalls);
        Assert.InRange(
            magentic.SynthesisBudget.UsedProviderCalls,
            1,
            magentic.SynthesisBudget.MaxProviderCalls);
        MagenticPhaseProbe probe = Assert.IsType<MagenticPhaseProbe>(
            magentic.MagenticProbe);
        Assert.Equal(2, probe.Plans.Count);
        Assert.Equal(2, probe.Replans.Count);
        Assert.Equal(6, probe.Ledgers.Count);
        Assert.Equal(6, probe.ProgressEventCount);
        Assert.Equal(
            2,
            probe.Ledgers.Count(
                ledger =>
                    ledger.IsInLoop &&
                    !ledger.IsProgressBeingMade &&
                    !ledger.IsRequestSatisfied));
        Assert.Equal(
            2,
            probe.Ledgers.Count(
                ledger => ledger.IsRequestSatisfied));
        Assert.Equal(2, probe.ExecutionCount);
        Assert.Null(magentic.SynthesisClient);

        var (_, runId, _, _) = CreateAcceptedMagenticTask();
        MagenticPhaseRuntime phaseRuntime =
            MagenticPhaseFactory.CreateStallThenRecover(
                new ReferenceArtifactStore(),
                runId,
                new MagenticPhaseProbe(),
                new ReferenceSynthesisBudget(24),
                requirePlanSignoff: false,
                requireArtifactCorrection: false);
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
        var (artifacts, runId, task, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        var budget = new ReferenceSynthesisBudget(12);
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateRevisionThenApprove(
                artifacts,
                runId,
                probe,
                budget);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        var environment = InProcessExecution.Lockstep.WithCheckpointing(
            checkpoints);

        await using StreamingRun run = await OpenAsync(
            runtime,
            environment,
            task);
        MagenticRunSlice first = await DrainAsync(run, probe);
        ExternalRequest initialRequest = Assert.IsType<ExternalRequest>(
            first.Request);
        MagenticPlanReviewRequest initialReview =
            initialRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Initial plan review was unavailable.");

        await run.SendResponseAsync(
            initialRequest.CreateResponse(
                initialReview.Revise(
                    "Include explicit artifact-contract validation.")));
        MagenticRunSlice second = await DrainAsync(run, probe);
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

        await run.SendResponseAsync(
            revisedRequest.CreateResponse(
                revisedReview.Approve()));
        MagenticRunSlice completed = await DrainAsync(run, probe);

        Assert.NotNull(completed.Output);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                completed.Output[^1].Text,
                manifest,
                out string error),
            error);
        Assert.Single(probe.Plans);
        Assert.Single(probe.Replans);
        Assert.InRange(
            budget.UsedProviderCalls,
            1,
            budget.MaxProviderCalls);
    }

    [Fact]
    public async Task StallReplanCheckpoint_RestoresOnSameRun()
    {
        var (artifacts, runId, task, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        var budget = new ReferenceSynthesisBudget(12);
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateStallThenRecover(
                artifacts,
                runId,
                probe,
                budget,
                requirePlanSignoff: true,
                requireArtifactCorrection: false);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        var environment = InProcessExecution.Lockstep.WithCheckpointing(
            checkpoints);

        await using StreamingRun run = await OpenAsync(
            runtime,
            environment,
            task);
        MagenticRunSlice first = await DrainAsync(run, probe);
        ExternalRequest initialRequest = Assert.IsType<ExternalRequest>(
            first.Request);
        MagenticPlanReviewRequest initialReview =
            initialRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Initial plan review was unavailable.");

        await run.SendResponseAsync(
            initialRequest.CreateResponse(
                initialReview.Approve()));
        MagenticRunSlice replanned = await DrainAsync(
            run,
            probe);
        ExternalRequest replanRequest = Assert.IsType<ExternalRequest>(
            replanned.Request);
        MagenticPlanReviewRequest replanReview =
            replanRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Replanned review was unavailable.");
        Assert.True(replanReview.IsStalled);
        Assert.Single(probe.Replans);

        await run.RestoreCheckpointAsync(
            Assert.IsType<CheckpointInfo>(replanned.Checkpoint),
            TestContext.Current.CancellationToken);
        MagenticRunSlice restored = await DrainAsync(run, probe);
        ExternalRequest restoredRequest = Assert.IsType<ExternalRequest>(
            restored.Request);
        MagenticPlanReviewRequest restoredReview =
            restoredRequest.Data.As<MagenticPlanReviewRequest>()
            ?? throw new InvalidOperationException(
                "Restored replan review was unavailable.");
        Assert.True(restoredReview.IsStalled);

        await run.SendResponseAsync(
            restoredRequest.CreateResponse(
                restoredReview.Approve()));
        MagenticRunSlice completed = await DrainAsync(
            run,
            probe);

        Assert.NotNull(completed.Output);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                completed.Output[^1].Text,
                manifest,
                out string error),
            error);
        Assert.True(
            runtime.ManifestAnalystClient.CallCount > 0);
    }

    [Fact]
    public async Task InvalidSpeaker_UpstreamFinalizesButAdapterRejects()
    {
        var (artifacts, runId, task, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        var budget = new ReferenceSynthesisBudget(12);
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateInvalidSpeaker(
                artifacts,
                runId,
                probe,
                budget);

        MagenticPhaseRunResult result =
            await MagenticPhaseRunner.RunAsync(
                runtime,
                task,
                TestContext.Current.CancellationToken);

        Assert.False(
            result.Succeeded,
            $"failure={result.FailureCode}; warnings={string.Join(" | ", probe.Warnings)}");
        Assert.Equal("invalid-next-speaker", result.FailureCode);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                result.FinalText,
                manifest,
                out string error),
            error);
        Assert.Contains(
            probe.Warnings,
            warning => warning.Contains(
                "Invalid next speaker",
                StringComparison.Ordinal));
        Assert.Equal(0, runtime.ManifestAnalystClient.CallCount);
        Assert.Equal(0, runtime.ContractCriticClient.CallCount);
    }

    [Fact]
    public async Task EmptySpeaker_FallbackStillFailsClosed()
    {
        var (artifacts, runId, task, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        var budget = new ReferenceSynthesisBudget(12);
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateEmptySpeaker(
                artifacts,
                runId,
                probe,
                budget);

        MagenticPhaseRunResult result =
            await MagenticPhaseRunner.RunAsync(
                runtime,
                task,
                TestContext.Current.CancellationToken);

        Assert.False(
            result.Succeeded,
            $"failure={result.FailureCode}; warnings={string.Join(" | ", probe.Warnings)}");
        Assert.Equal("invalid-next-speaker", result.FailureCode);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            manifestReference,
            runId);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                result.FinalText,
                manifest,
                out string error),
            error);
        Assert.Contains(
            probe.Warnings,
            warning => warning.Contains(
                "Next speaker answer empty",
                StringComparison.Ordinal));
        Assert.True(runtime.ManifestAnalystClient.CallCount > 0);
    }

    [Fact]
    public async Task InvalidMagenticFinal_IsRejectedByExistingArtifactBoundary()
    {
        var (artifacts, runId, task, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        var budget = new ReferenceSynthesisBudget(12);
        MagenticPhaseRuntime runtime =
            MagenticPhaseFactory.CreateInvalidFinalArtifact(
                artifacts,
                runId,
                probe,
                budget);
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
    public async Task MagenticArtifactCorrection_UsesSharedManifestPolicy()
    {
        var (artifacts, runId, _, manifestReference) =
            CreateAcceptedMagenticTask();
        var probe = new MagenticPhaseProbe();
        var budget = new ReferenceSynthesisBudget(24);
        var runtimes = new List<MagenticPhaseRuntime>();
        var executor = new MagenticSynthesisPhaseExecutor(
            artifacts,
            runId,
            () =>
            {
                MagenticPhaseRuntime runtime =
                    MagenticPhaseFactory.CreateStallThenRecover(
                        artifacts,
                        runId,
                        probe,
                        budget,
                        requirePlanSignoff: false,
                        requireArtifactCorrection: true);
                runtimes.Add(runtime);
                return runtime;
            },
            budget,
            maxArtifactAttempts: 2);
        ReferencePhaseArtifact manifestArtifact =
            ReferencePhaseArtifact.WithArtifact(
                ReferencePhaseArtifact.Candidate(
                    SpecialistManifestBarrierExecutor.Phase,
                    ordinal: 100,
                    required: true,
                    content: "{}"),
                manifestReference);

        ReferencePhaseArtifact candidate = await executor.HandleAsync(
            manifestArtifact,
            context: null!,
            TestContext.Current.CancellationToken);
        var boundary = new SynthesisArtifactBoundaryExecutor(
            artifacts,
            runId);
        ReferencePhaseArtifact validated = await boundary.HandleAsync(
            candidate,
            context: null!,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, runtimes.Count);
        Assert.Equal(2, probe.ExecutionCount);
        Assert.False(
            ContainsManagerText(
                runtimes[0],
                ReferenceArtifactValidator.SynthesisCorrectionCode));
        Assert.True(
            ContainsManagerText(
                runtimes[1],
                ReferenceArtifactValidator.CreateSynthesisCorrection(
                    "missing-recommendation")));
        Assert.Equal(
            ReferencePipelineOutcome.Completed,
            validated.Outcome);
        Assert.InRange(
            budget.UsedProviderCalls,
            1,
            budget.MaxProviderCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynthesisArms_EnforceSameProviderCallBudget(
        bool useMagentic)
    {
        ReferenceSynthesisExecutorKind synthesisKind = useMagentic
            ? ReferenceSynthesisExecutorKind.Magentic
            : ReferenceSynthesisExecutorKind.Harness;
        var request = new ReferencePipelineRequest(
            $"budget-{synthesisKind}-{Guid.NewGuid():N}",
            "Synthetic release readiness");
        ReferencePipelineRuntime runtime = ReferencePipelineFactory.Create(
            request,
            ReferencePipelineOptions.Default with
            {
                SynthesisExecutorKind = synthesisKind,
                SynthesisMaxProviderCalls = 2,
            },
            new ReferenceArtifactStore(),
            new IdempotentDeliverySink());

        ReferencePipelineResult result = await RunOuterAsync(
            runtime,
            captureCheckpoint: false);

        Assert.Equal(2, runtime.SynthesisBudget.MaxProviderCalls);
        Assert.Equal(2, runtime.SynthesisBudget.UsedProviderCalls);
        Assert.Equal(ReferencePipelineOutcome.Failed, result.Outcome);
        Assert.Equal(
            ReferencePipelineOutcome.Failed,
            result.Synthesis.Outcome);
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
        Assert.Equal(4, runtime.MagenticProbe?.ExecutionCount);
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

    private static void AssertAcceptedSynthesis(
        ReferencePipelineRuntime runtime,
        ReferencePipelineResult result)
    {
        ReferenceArtifactReference synthesisReference =
            Assert.IsType<ReferenceArtifactReference>(
                result.Synthesis.Artifact);
        ReferenceArtifactManifest manifest = runtime.Artifacts.GetManifest(
            result.Manifest,
            runtime.Request.RunId);
        string content = runtime.Artifacts.Read(
            runtime.Request.RunId,
            synthesisReference.Id);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                content,
                manifest,
                out string error),
            error);
    }

    private static bool ContainsManagerText(
        MagenticPhaseRuntime runtime,
        string expected) =>
        runtime.ManagerClient.RecordedInputs
            .SelectMany(messages => messages)
            .Select(message => message.Text)
            .OfType<string>()
            .Any(text => text.Contains(
                expected,
                StringComparison.Ordinal));

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
