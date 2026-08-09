using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.Copilot;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationArmDriver
{
    internal static async Task<HostedEvaluationArmResult> RunAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationCase @case,
        int trialIndex,
        HostedEvaluationArm arm,
        CancellationToken cancellationToken)
    {
        string trialId =
            $"{@case.CaseId}:{@case.Scenario}:r{trialIndex:D2}:{arm}";
        if (!IsApplicable(@case.Scenario, arm))
        {
            return CreateNotApplicable(
                protocol,
                @case,
                trialId,
                arm);
        }

        string runId = $"{protocol.RunId}:{trialId}";
        var artifacts = new ReferenceArtifactStore();
        var delivery = new IdempotentDeliverySink();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                runId,
                @case.Scenario);
        var telemetry = new HostedEvaluationTelemetry();
        var probe = new MagenticPhaseProbe();
        HostedFaultMode faultMode = GetFaultMode(
            @case.Scenario,
            arm);
        using Activity activity = new Activity(
            "autonomous-phase.arm").Start();
        var stopwatch = Stopwatch.StartNew();

        ReferencePipelineResult? result = null;
        HostedEvaluationRunObservation observation;
        int restoreEvents = 0;
        bool restoreAttempted = false;
        bool restoreSucceeded = false;
        bool acceptedPriorReran = false;
        int phaseFailures = 0;
        HostedEvaluationExecutionStatus executionStatus;
        string? failureCode = null;

        try
        {
            switch (@case.Scenario)
            {
                case HostedEvaluationScenario.Cancellation:
                    observation = await RunCancellationAsync(
                        protocol,
                        arm,
                        artifacts,
                        delivery,
                        fixture,
                        telemetry,
                        probe,
                        faultMode,
                        runId,
                        cancellationToken);
                    executionStatus = HostedEvaluationExecutionStatus.Canceled;
                    failureCode = observation.FailureCode;
                    break;

                case HostedEvaluationScenario.CheckpointRestore:
                    restoreAttempted = true;
                    (observation, result, acceptedPriorReran) =
                        await RunFreshRestoreAsync(
                            protocol,
                            arm,
                            artifacts,
                            delivery,
                            fixture,
                            telemetry,
                            probe,
                            runId,
                            cancellationToken);
                    restoreEvents = result is null ? 0 : 1;
                    restoreSucceeded = result is not null;
                    executionStatus = result is null
                        ? HostedEvaluationExecutionStatus.Failed
                        : HostedEvaluationExecutionStatus.Completed;
                    failureCode = observation.FailureCode;
                    break;

                case HostedEvaluationScenario.DeliveryReplay:
                    observation = await RunOnceAsync(
                        protocol,
                        arm,
                        artifacts,
                        delivery,
                        fixture,
                        telemetry,
                        probe,
                        faultMode,
                        runId,
                        cancellationToken);
                    result = observation.Result;
                    if (result is not null)
                    {
                        _ = delivery.Publish(result);
                    }

                    executionStatus = result is null
                        ? HostedEvaluationExecutionStatus.Failed
                        : HostedEvaluationExecutionStatus.Completed;
                    failureCode = observation.FailureCode;
                    break;

                default:
                    observation = await RunOnceAsync(
                        protocol,
                        arm,
                        artifacts,
                        delivery,
                        fixture,
                        telemetry,
                        probe,
                        faultMode,
                        runId,
                        cancellationToken);
                    result = observation.Result;
                    executionStatus = observation.Canceled
                        ? HostedEvaluationExecutionStatus.Canceled
                        : result is null
                            ? HostedEvaluationExecutionStatus.Failed
                            : HostedEvaluationExecutionStatus.Completed;
                    failureCode = observation.FailureCode;
                    break;
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            observation = new HostedEvaluationRunObservation(
                Result: null,
                BeforeSynthesis: null,
                FailureCode: "caller-canceled",
                Canceled: true,
                CheckpointEvents: 0);
            executionStatus = HostedEvaluationExecutionStatus.Canceled;
            failureCode = "caller-canceled";
        }
        catch (OperationCanceledException)
        {
            observation = new HostedEvaluationRunObservation(
                Result: null,
                BeforeSynthesis: null,
                FailureCode: "provider-timeout",
                Canceled: false,
                CheckpointEvents: 0);
            executionStatus = HostedEvaluationExecutionStatus.Failed;
            failureCode = "provider-timeout";
            phaseFailures++;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or HttpRequestException
                or TimeoutException
                or CopilotAuthException
                or CopilotRateLimitException)
        {
            observation = new HostedEvaluationRunObservation(
                Result: null,
                BeforeSynthesis: null,
                FailureCode: exception.GetType().Name,
                Canceled: false,
                CheckpointEvents: 0);
            executionStatus = HostedEvaluationExecutionStatus.Failed;
            failureCode = exception.GetType().Name;
            phaseFailures++;
        }

        stopwatch.Stop();
        result ??= observation.Result;
        var scores = HostedDeterministicScorer.Score(
            artifacts,
            runId,
            @case.Scenario,
            fixture,
            result);
        HostedEvaluationTelemetrySnapshot telemetrySnapshot =
            telemetry.Snapshot();
        if (result?.Synthesis.Outcome == ReferencePipelineOutcome.Failed)
        {
            phaseFailures++;
        }

        bool contractPass = EvaluateContract(
            arm,
            @case.Scenario,
            executionStatus,
            result,
            scores,
            delivery,
            acceptedPriorReran,
            restoreAttempted,
            restoreSucceeded,
            probe);
        return new HostedEvaluationArmResult(
            Arm: arm,
            Scenario: @case.Scenario,
            ExecutionStatus: executionStatus,
            Applicable: true,
            ScenarioContractPass: contractPass,
            ArtifactSchemaValid: scores.SchemaValid,
            RequiredArtifactCoverage: scores.RequiredCoverage,
            GapReportingCorrect: scores.GapCorrect,
            SuccessfulSiblingPreserved: scores.SiblingPreserved,
            EvidenceReferencesValid: scores.EvidenceValid,
            AcceptedPriorPhaseReran: acceptedPriorReran,
            RestoreAttempted: restoreAttempted,
            RestoreSucceeded: restoreSucceeded,
            DeliveryAttempts: delivery.AttemptCount,
            AuthoritativeDeliveries: delivery.AuthoritativeCount,
            ModelCalls: telemetrySnapshot.ModelCalls,
            ToolCalls: telemetrySnapshot.ToolCalls,
            InputTokens: telemetrySnapshot.InputTokens,
            OutputTokens: telemetrySnapshot.OutputTokens,
            CachedInputTokens: telemetrySnapshot.CachedInputTokens,
            ChildSessionCount: telemetrySnapshot.ChildSessionCount,
            ChildFailureCount: telemetrySnapshot.ChildFailureCount,
            ProviderFailureCount: telemetrySnapshot.ProviderFailures,
            PhaseFailureCount: phaseFailures,
            CheckpointCount: observation.CheckpointEvents,
            RestoreEventCount: restoreEvents,
            PlanCount: probe.Plans.Count,
            ReplanCount: probe.Replans.Count,
            ProgressEventCount: probe.ProgressEventCount,
            DurationMilliseconds: (long)stopwatch.Elapsed.TotalMilliseconds,
            TraceId: activity?.TraceId.ToString() ?? string.Empty,
            ArtifactDigest: result?.Synthesis.Artifact?.Digest,
            FailureCode: failureCode ?? result?.Synthesis.Error,
            OutputText: scores.OutputText,
            QualityEvidenceStatus: "DETERMINISTIC_ONLY",
            Provenance: CreateProvenance(
                protocol,
                @case,
                trialId,
                arm,
                telemetrySnapshot.ObservedModel));
    }

    private static async Task<HostedEvaluationRunObservation> RunOnceAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultMode faultMode,
        string runId,
        CancellationToken cancellationToken)
    {
        using HostedEvaluationArmExecution execution = BuildExecution(
            protocol,
            arm,
            artifacts,
            delivery,
            fixture,
            telemetry,
            probe,
            faultMode,
            runId);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            execution.Workflow,
            "start",
            checkpoints,
            $"hosted-eval:{runId}",
            cancellationToken);
        return await ObserveAsync(
            run,
            cancellationToken);
    }

    private static async Task<HostedEvaluationRunObservation> RunCancellationAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultMode faultMode,
        string runId,
        CancellationToken cancellationToken)
    {
        using HostedEvaluationArmExecution execution = BuildExecution(
            protocol,
            arm,
            artifacts,
            delivery,
            fixture,
            telemetry,
            probe,
            faultMode,
            runId);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            execution.Workflow,
            "start",
            checkpoints,
            $"hosted-eval:{runId}",
            cancellationToken);
        Task<HostedEvaluationRunObservation> observation = ObserveAsync(
            run,
            cancellationToken);
        await telemetry.FirstCallStarted.Task.WaitAsync(
            TimeSpan.FromMinutes(2),
            cancellationToken);
        Task cancellation = run.CancelRunAsync().AsTask();
        await Task.WhenAll(cancellation, observation).WaitAsync(
            TimeSpan.FromMinutes(2),
            cancellationToken);
        HostedEvaluationRunObservation observed = await observation;
        return observed with
        {
            Canceled = true,
            FailureCode = "caller-canceled",
        };
    }

    private static async Task<(
        HostedEvaluationRunObservation Observation,
        ReferencePipelineResult? Result,
        bool AcceptedPriorReran)> RunFreshRestoreAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        string runId,
        CancellationToken cancellationToken)
    {
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        HostedEvaluationRunObservation initial;
        using (HostedEvaluationArmExecution first = BuildExecution(
            protocol,
            arm,
            artifacts,
            delivery,
            fixture,
            telemetry,
            probe,
            HostedFaultMode.FailFirstTerminal,
            runId))
        {
            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
                first.Workflow,
                "start",
                checkpoints,
                $"hosted-eval:{runId}",
                cancellationToken);
            initial = await ObserveAsync(
                run,
                cancellationToken);
        }

        if (initial.BeforeSynthesis is null)
        {
            return (
                initial,
                Result: null,
                AcceptedPriorReran: true);
        }

        using HostedEvaluationArmExecution recovered = BuildExecution(
            protocol,
            arm,
            artifacts,
            delivery,
            fixture,
            telemetry,
            probe,
            HostedFaultMode.None,
            runId);
        await using StreamingRun resumed =
            await InProcessExecution.ResumeStreamingAsync(
                recovered.Workflow,
                initial.BeforeSynthesis,
                checkpoints,
                cancellationToken);
        HostedEvaluationRunObservation restored = await ObserveAsync(
            resumed,
            cancellationToken);
        return (
            restored,
            restored.Result,
            AcceptedPriorReran: recovered.Start.CallCount != 0);
    }

    private static HostedEvaluationArmExecution BuildExecution(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultMode faultMode,
        string runId)
    {
        HostedSynthesisArm synthesis = HostedSynthesisArmFactory.Create(
            arm,
            protocol.Model,
            artifacts,
            runId,
            telemetry,
            probe,
            faultMode);
        var start = new HostedManifestStartExecutor(
            fixture.Manifest);
        var boundary = new SynthesisArtifactBoundaryExecutor(
            artifacts,
            runId);
        var deliveryExecutor = new DeliveryExecutor(
            artifacts,
            delivery,
            runId);
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, synthesis.Executor)
            .AddEdge(synthesis.Executor, boundary)
            .AddEdge(boundary, deliveryExecutor)
            .WithOutputFrom(deliveryExecutor)
            .WithName($"hosted-synthesis-{arm}.v1")
            .Build();
        return new HostedEvaluationArmExecution(
            workflow,
            start,
            synthesis,
            artifacts,
            delivery);
    }

    private static async Task<HostedEvaluationRunObservation> ObserveAsync(
        StreamingRun run,
        CancellationToken cancellationToken)
    {
        ReferencePipelineResult? result = null;
        CheckpointInfo? beforeSynthesis = null;
        string? failureCode = null;
        int checkpointEvents = 0;
        bool startCompleted = false;
        await foreach (WorkflowEvent workflowEvent in run.WatchStreamAsync(
            cancellationToken))
        {
            if (workflowEvent is ExecutorCompletedEvent
                {
                    ExecutorId: HostedManifestStartExecutor.ExecutorId,
                })
            {
                startCompleted = true;
            }

            if (workflowEvent is SuperStepCompletedEvent
                {
                    CompletionInfo.Checkpoint: { } checkpoint,
                })
            {
                checkpointEvents++;
                if (startCompleted && beforeSynthesis is null)
                {
                    beforeSynthesis = checkpoint;
                    startCompleted = false;
                }
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
                failureCode = error.Exception?.GetType().Name
                    ?? "workflow-error";
            }

            if (workflowEvent is ExecutorFailedEvent executorFailure)
            {
                failureCode = executorFailure.Data?.GetType().Name
                    ?? "executor-failed";
            }
        }

        return new HostedEvaluationRunObservation(
            result,
            beforeSynthesis,
            failureCode,
            Canceled: false,
            checkpointEvents);
    }

    private static bool EvaluateContract(
        HostedEvaluationArm arm,
        HostedEvaluationScenario scenario,
        HostedEvaluationExecutionStatus executionStatus,
        ReferencePipelineResult? result,
        (
            bool SchemaValid,
            bool RequiredCoverage,
            bool GapCorrect,
            bool SiblingPreserved,
            bool EvidenceValid,
            string? OutputText) scores,
        IdempotentDeliverySink delivery,
        bool acceptedPriorReran,
        bool restoreAttempted,
        bool restoreSucceeded,
        MagenticPhaseProbe probe) =>
        scenario switch
        {
            HostedEvaluationScenario.Success =>
                executionStatus == HostedEvaluationExecutionStatus.Completed &&
                result?.Outcome == ReferencePipelineOutcome.Completed &&
                scores.SchemaValid &&
                scores.RequiredCoverage &&
                scores.EvidenceValid &&
                delivery.AuthoritativeCount == 1,
            HostedEvaluationScenario.OptionalBranchFailure =>
                result?.Outcome == ReferencePipelineOutcome.Partial &&
                scores.SchemaValid &&
                scores.GapCorrect &&
                scores.SiblingPreserved &&
                scores.EvidenceValid,
            HostedEvaluationScenario.RequiredBranchFailure =>
                result?.Outcome == ReferencePipelineOutcome.Failed &&
                result.Synthesis.Outcome == ReferencePipelineOutcome.Skipped &&
                scores.SiblingPreserved &&
                delivery.AuthoritativeCount == 1,
            HostedEvaluationScenario.CorrectionSucceeds =>
                result?.Outcome == ReferencePipelineOutcome.Completed &&
                scores.SchemaValid &&
                scores.EvidenceValid,
            HostedEvaluationScenario.CorrectionExhausted =>
                result?.Outcome == ReferencePipelineOutcome.Failed &&
                delivery.AuthoritativeCount == 1,
            HostedEvaluationScenario.Cancellation =>
                executionStatus == HostedEvaluationExecutionStatus.Canceled &&
                delivery.AuthoritativeCount == 0,
            HostedEvaluationScenario.CheckpointRestore =>
                restoreAttempted &&
                restoreSucceeded &&
                !acceptedPriorReran &&
                result?.Outcome == ReferencePipelineOutcome.Completed &&
                delivery.AuthoritativeCount == 1,
            HostedEvaluationScenario.DeliveryReplay =>
                result is not null &&
                delivery.AttemptCount == 2 &&
                delivery.AuthoritativeCount == 1,
            HostedEvaluationScenario.IneffectiveProgress =>
                result?.Outcome == ReferencePipelineOutcome.Completed &&
                scores.SchemaValid &&
                (arm == HostedEvaluationArm.Magentic
                    ? probe.Replans.Count > 0
                    : result.Synthesis.Artifact is not null),
            _ => false,
        };

    private static bool IsApplicable(
        HostedEvaluationScenario scenario,
        HostedEvaluationArm arm) =>
        scenario switch
        {
            HostedEvaluationScenario.CorrectionSucceeds
                when arm == HostedEvaluationArm.Magentic => false,
            _ => true,
        };

    private static HostedFaultMode GetFaultMode(
        HostedEvaluationScenario scenario,
        HostedEvaluationArm arm) =>
        scenario switch
        {
            HostedEvaluationScenario.CorrectionSucceeds =>
                HostedFaultMode.InvalidFirstTerminal,
            HostedEvaluationScenario.CorrectionExhausted
                when arm == HostedEvaluationArm.Magentic =>
                HostedFaultMode.InvalidMagenticFinal,
            HostedEvaluationScenario.CorrectionExhausted =>
                HostedFaultMode.InvalidEveryTerminal,
            HostedEvaluationScenario.Cancellation =>
                HostedFaultMode.DelayFirstCallUntilCanceled,
            HostedEvaluationScenario.CheckpointRestore =>
                HostedFaultMode.FailFirstTerminal,
            HostedEvaluationScenario.IneffectiveProgress
                when arm == HostedEvaluationArm.Magentic =>
                HostedFaultMode.ForceFirstMagenticStall,
            HostedEvaluationScenario.IneffectiveProgress =>
                HostedFaultMode.InvalidFirstTerminal,
            _ => HostedFaultMode.None,
        };

    private static HostedEvaluationArmResult CreateNotApplicable(
        HostedEvaluationProtocol protocol,
        HostedEvaluationCase @case,
        string trialId,
        HostedEvaluationArm arm) =>
        new(
            Arm: arm,
            Scenario: @case.Scenario,
            ExecutionStatus: HostedEvaluationExecutionStatus.NotApplicable,
            Applicable: false,
            ScenarioContractPass: false,
            ArtifactSchemaValid: false,
            RequiredArtifactCoverage: false,
            GapReportingCorrect: false,
            SuccessfulSiblingPreserved: false,
            EvidenceReferencesValid: false,
            AcceptedPriorPhaseReran: false,
            RestoreAttempted: false,
            RestoreSucceeded: false,
            DeliveryAttempts: 0,
            AuthoritativeDeliveries: 0,
            ModelCalls: 0,
            ToolCalls: 0,
            InputTokens: 0,
            OutputTokens: 0,
            CachedInputTokens: null,
            ChildSessionCount: 0,
            ChildFailureCount: 0,
            ProviderFailureCount: 0,
            PhaseFailureCount: 0,
            CheckpointCount: 0,
            RestoreEventCount: 0,
            PlanCount: 0,
            ReplanCount: 0,
            ProgressEventCount: 0,
            DurationMilliseconds: 0,
            TraceId: string.Empty,
            ArtifactDigest: null,
            FailureCode: null,
            OutputText: null,
            QualityEvidenceStatus: "NOT_APPLICABLE",
            Provenance: CreateProvenance(
                protocol,
                @case,
                trialId,
                arm,
                observedModel: null));

    private static HostedEvaluationProvenance CreateProvenance(
        HostedEvaluationProtocol protocol,
        HostedEvaluationCase @case,
        string trialId,
        HostedEvaluationArm arm,
        string? observedModel)
    {
        string configuration = string.Join(
            "|",
            HostedEvaluationProtocol.Version,
            protocol.Model,
            arm,
            @case.Scenario);
        string configurationHash = Convert
            .ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(configuration)))
            .ToLowerInvariant();
        return new HostedEvaluationProvenance(
            protocol.Repository,
            protocol.CommitSha,
            protocol.Ref,
            HostedEvaluationProtocol.Version,
            protocol.Model,
            observedModel,
            typeof(FoundryHarnessAgentFactory).Assembly
                .GetName()
                .Version?
                .ToString() ?? "unknown",
            typeof(AIAgent).Assembly
                .GetName()
                .Version?
                .ToString() ?? "unknown",
            typeof(IChatClient).Assembly
                .GetName()
                .Version?
                .ToString() ?? "unknown",
            configurationHash,
            @case.CaseId,
            trialId);
    }
}
