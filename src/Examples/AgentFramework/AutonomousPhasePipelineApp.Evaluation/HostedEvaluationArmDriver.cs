using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationArmDriver
{
    internal static async Task<HostedEvaluationArmResult> RunAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationCase @case,
        int trialIndex,
        HostedEvaluationArm arm,
        HostedEvaluationChatClientFactory clientFactory,
        CancellationToken cancellationToken)
    {
        string trialId =
            $"{@case.CaseId}:{@case.Scenario}:r{trialIndex:D2}:{arm}";
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
        HostedFaultPlan faultPlan = HostedEvaluationFaultCatalog.GetPlan(
            @case.Scenario,
            arm);
        using Activity? activity =
            HostedEvaluationActivitySource.Source.StartActivity(
                "phase.synthesis",
                ActivityKind.Internal);
        activity?.SetTag("foundry.eval.arm", arm.ToString());
        activity?.SetTag(
            "foundry.eval.scenario",
            @case.Scenario.ToString());
        activity?.SetTag(
            "foundry.eval.protocol",
            HostedEvaluationProtocol.Version);
        var stopwatch = Stopwatch.StartNew();

        HostedEvaluationRunObservation observation = EmptyObservation();
        ReferencePipelineResult? result = null;
        bool restoreAttempted = false;
        bool restoreSucceeded = false;
        bool acceptedPriorReran = false;
        int restoreEvents = 0;
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
                        faultPlan,
                        clientFactory,
                        runId,
                        cancellationToken);
                    executionStatus =
                        ClassifyCancellation(observation);
                    failureCode = observation.FailureCode;
                    break;

                case HostedEvaluationScenario.CheckpointRestore:
                    restoreAttempted = true;
                    (observation, result, acceptedPriorReran) =
                        await RunSameRunRestoreAsync(
                            protocol,
                            arm,
                            artifacts,
                            delivery,
                            fixture,
                            telemetry,
                            probe,
                            clientFactory,
                            runId,
                            cancellationToken);
                    restoreSucceeded = result is not null;
                    restoreEvents = restoreSucceeded ? 1 : 0;
                    executionStatus = restoreSucceeded
                        ? HostedEvaluationExecutionStatus.Completed
                        : HostedEvaluationExecutionStatus.Failed;
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
                        faultPlan,
                        clientFactory,
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
                        faultPlan,
                        clientFactory,
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
            observation = EmptyObservation() with
            {
                FailureCode = "caller-canceled",
                Canceled = true,
            };
            executionStatus =
                HostedEvaluationExecutionStatus.Canceled;
            failureCode = "caller-canceled";
        }
        catch (OperationCanceledException)
        {
            observation = EmptyObservation() with
            {
                FailureCode = "provider-timeout",
            };
            executionStatus =
                HostedEvaluationExecutionStatus.Failed;
            failureCode = "provider-timeout";
            phaseFailures++;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or HttpRequestException
                or TimeoutException)
        {
            observation = EmptyObservation() with
            {
                FailureCode = exception.GetType().Name,
            };
            executionStatus =
                HostedEvaluationExecutionStatus.Failed;
            failureCode = exception.GetType().Name;
            phaseFailures++;
        }

        stopwatch.Stop();
        result ??= observation.Result;
        var (correctness, outputText) =
            HostedDeterministicScorer.Score(
                artifacts,
                runId,
                fixture,
                result,
                delivery.AuthoritativeCount);
        HostedEvaluationTelemetrySnapshot telemetrySnapshot =
            telemetry.Snapshot();
        bool faultAfterProviderWork =
            !faultPlan.MustActivate ||
            telemetrySnapshot.CompletedModelCallsAtFault > 0;
        bool collaboratorRequired =
            @case.Scenario ==
                HostedEvaluationScenario.Cancellation &&
            (arm is HostedEvaluationArm.HarnessDelegated
                or HostedEvaluationArm.Magentic);
        bool faultAfterCollaboratorWork =
            !faultPlan.MustActivate ||
            !collaboratorRequired ||
            telemetrySnapshot.CompletedChildSessionCountAtFault > 0;
        bool cancellationObserved =
            executionStatus ==
                HostedEvaluationExecutionStatus.Canceled;
        bool noDeliveryAfterCancellation =
            @case.Scenario !=
                HostedEvaluationScenario.Cancellation ||
            delivery.AuthoritativeCount == 0;
        bool replayIdempotent =
            @case.Scenario !=
                HostedEvaluationScenario.DeliveryReplay ||
            (delivery.AttemptCount == 2 &&
             delivery.AuthoritativeCount == 1);
        bool correctionRecovered =
            @case.Scenario is not (
                HostedEvaluationScenario.CorrectionSucceeds or
                HostedEvaluationScenario.IneffectiveProgress) ||
            (telemetrySnapshot.FaultActivated &&
             correctness.TaskContractPass);
        var resilience = new HostedEvaluationResilience(
            faultPlan.MustActivate,
            telemetrySnapshot.FaultActivated,
            faultAfterProviderWork,
            faultAfterCollaboratorWork,
            cancellationObserved,
            noDeliveryAfterCancellation,
            restoreAttempted,
            restoreSucceeded,
            acceptedPriorReran,
            replayIdempotent,
            correctionRecovered,
            failureCode ?? result?.Synthesis.Error);
        var resources = new HostedEvaluationResources(
            observation.ProviderCallLimit,
            observation.ProviderCallsUsed,
            telemetrySnapshot.ModelCalls,
            telemetrySnapshot.ToolCalls,
            telemetrySnapshot.InputTokens,
            telemetrySnapshot.OutputTokens,
            telemetrySnapshot.CachedInputTokens,
            telemetrySnapshot.ChildSessionCount,
            telemetrySnapshot.ChildFailureCount,
            observation.CheckpointEvents,
            restoreEvents,
            probe.Plans.Count,
            probe.Replans.Count,
            probe.ProgressEventCount,
            (long)stopwatch.Elapsed.TotalMilliseconds);
        var infrastructure = new HostedEvaluationInfrastructure(
            executionStatus,
            telemetrySnapshot.ProviderFailures,
            phaseFailures,
            outputText is not null &&
                (!faultPlan.MustActivate ||
                 (telemetrySnapshot.FaultActivated &&
                  telemetrySnapshot.BoundaryOutputOrdinal >
                    telemetrySnapshot.FaultActivationOrdinal)),
            HostedSemanticQualityStatus
                .NotScoredCalibrationRequired);
        bool scenarioPass = EvaluateScenarioContract(
            arm,
            @case.Scenario,
            correctness,
            resilience,
            infrastructure,
            probe);

        return new HostedEvaluationArmResult(
            arm,
            @case.Scenario,
            Applicable: true,
            scenarioPass,
            result?.Outcome,
            result?.Synthesis.Outcome,
            activity?.TraceId.ToString() ?? string.Empty,
            result?.Synthesis.Artifact?.Digest,
            outputText,
            correctness,
            resilience,
            resources,
            infrastructure,
            CreateProvenance(
                protocol,
                @case,
                trialId,
                arm,
                telemetrySnapshot.ObservedModel,
                faultPlan));
    }

    private static async Task<HostedEvaluationRunObservation> RunOnceAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultPlan faultPlan,
        HostedEvaluationChatClientFactory clientFactory,
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
            faultPlan,
            clientFactory,
            failFirstBeforeSynthesis: false,
            runId);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        await using StreamingRun run =
            await InProcessExecution.RunStreamingAsync(
                execution.Workflow,
                "start",
                checkpoints,
                $"hosted-eval:{runId}",
                cancellationToken);
        HostedEvaluationRunObservation observation =
            await ObserveAsync(
                run,
                telemetry,
                cancellationToken);
        return AddBudget(
            observation,
            execution.Synthesis.Budget);
    }

    private static async Task<HostedEvaluationRunObservation>
        RunCancellationAsync(
            HostedEvaluationProtocol protocol,
            HostedEvaluationArm arm,
            ReferenceArtifactStore artifacts,
            IdempotentDeliverySink delivery,
            HostedEvaluationFixtureData fixture,
            HostedEvaluationTelemetry telemetry,
            MagenticPhaseProbe probe,
            HostedFaultPlan faultPlan,
            HostedEvaluationChatClientFactory clientFactory,
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
            faultPlan,
            clientFactory,
            failFirstBeforeSynthesis: false,
            runId);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        await using StreamingRun run =
            await InProcessExecution.RunStreamingAsync(
                execution.Workflow,
                "start",
                checkpoints,
                $"hosted-eval:{runId}",
                cancellationToken);
        Task<HostedEvaluationRunObservation> observation =
            ObserveAsync(
                run,
                telemetry,
                cancellationToken);
        await telemetry.FaultActivated.Task.WaitAsync(
            TimeSpan.FromMinutes(2),
            cancellationToken);
        await run.CancelRunAsync();
        HostedEvaluationRunObservation observed =
            await observation.WaitAsync(
                TimeSpan.FromMinutes(2),
                cancellationToken);
        RunStatus status = await run.GetStatusAsync(
            cancellationToken);
        return AddBudget(
            observed with
            {
                Canceled = status == RunStatus.Ended,
                FailureCode = status == RunStatus.Ended
                    ? "caller-canceled"
                    : "cancellation-not-observed",
            },
            execution.Synthesis.Budget);
    }

    private static async Task<(
        HostedEvaluationRunObservation Observation,
        ReferencePipelineResult? Result,
        bool AcceptedPriorReran)> RunSameRunRestoreAsync(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedEvaluationChatClientFactory clientFactory,
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
            HostedFaultPlan.None,
            clientFactory,
            failFirstBeforeSynthesis: true,
            runId);
        CheckpointManager checkpoints = CheckpointManager.CreateInMemory();
        await using StreamingRun run =
            await InProcessExecution.RunStreamingAsync(
                execution.Workflow,
                "start",
                checkpoints,
                $"hosted-eval:{runId}",
                cancellationToken);
        HostedEvaluationRunObservation initial =
            await ObserveAsync(
                run,
                telemetry,
                cancellationToken);
        if (initial.BeforeSynthesis is null)
        {
            return (
                AddBudget(
                    initial,
                    execution.Synthesis.Budget),
                Result: null,
                AcceptedPriorReran: true);
        }

        await run.RestoreCheckpointAsync(
            initial.BeforeSynthesis,
            cancellationToken);
        HostedEvaluationRunObservation restored =
            await ObserveAsync(
                run,
                telemetry,
                cancellationToken);
        return (
            AddBudget(
                restored with
                {
                    CheckpointEvents =
                        initial.CheckpointEvents +
                        restored.CheckpointEvents,
                },
                execution.Synthesis.Budget),
            restored.Result,
            AcceptedPriorReran:
                execution.Start.CallCount != 1);
    }

    private static HostedEvaluationArmExecution BuildExecution(
        HostedEvaluationProtocol protocol,
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery,
        HostedEvaluationFixtureData fixture,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultPlan faultPlan,
        HostedEvaluationChatClientFactory clientFactory,
        bool failFirstBeforeSynthesis,
        string runId)
    {
        HostedSynthesisArm synthesis = HostedSynthesisArmFactory.Create(
            arm,
            artifacts,
            runId,
            telemetry,
            probe,
            faultPlan,
            clientFactory);
        var start = new HostedManifestStartExecutor(
            fixture.Manifest);
        var preSynthesisFault = new HostedPreSynthesisFaultExecutor(
            failFirstBeforeSynthesis);
        var boundary = new HostedSynthesisBoundaryExecutor(
            artifacts,
            runId);
        var deliveryExecutor = new HostedDeliveryExecutor(
            artifacts,
            delivery,
            runId);
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, preSynthesisFault)
            .AddEdge(preSynthesisFault, synthesis.Executor)
            .AddEdge(synthesis.Executor, boundary)
            .AddEdge(boundary, deliveryExecutor)
            .WithOutputFrom(deliveryExecutor)
            .WithName($"hosted-synthesis-{arm}.v2")
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
        HostedEvaluationTelemetry telemetry,
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
                telemetry.RecordBoundaryOutput();
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
            checkpointEvents,
            ProviderCallLimit: 0,
            ProviderCallsUsed: 0);
    }

    private static HostedEvaluationRunObservation AddBudget(
        HostedEvaluationRunObservation observation,
        ReferenceSynthesisBudget budget) =>
        observation with
        {
            ProviderCallLimit = budget.MaxProviderCalls,
            ProviderCallsUsed = budget.UsedProviderCalls,
        };

    private static bool EvaluateScenarioContract(
        HostedEvaluationArm arm,
        HostedEvaluationScenario scenario,
        HostedEvaluationCorrectness correctness,
        HostedEvaluationResilience resilience,
        HostedEvaluationInfrastructure infrastructure,
        MagenticPhaseProbe probe)
    {
        bool infrastructureHealthy =
            infrastructure.ProviderFailureCount == 0 &&
            infrastructure.PhaseFailureCount == 0;
        if (!infrastructureHealthy)
        {
            return false;
        }

        if (resilience.FaultRequired &&
            (!resilience.FaultActivated ||
             !resilience.FaultActivatedAfterProviderWork ||
             !resilience.FaultActivatedAfterCollaboratorWork))
        {
            return false;
        }

        return scenario switch
        {
            HostedEvaluationScenario.Cancellation =>
                resilience.CancellationObserved &&
                resilience.NoDeliveryAfterCancellation,
            HostedEvaluationScenario.CheckpointRestore =>
                correctness.TaskContractPass &&
                resilience.RestoreAttempted &&
                resilience.SameRunRestoreSucceeded &&
                !resilience.AcceptedPriorPhaseReran,
            HostedEvaluationScenario.DeliveryReplay =>
                correctness.TaskContractPass &&
                resilience.ReplayIdempotent,
            HostedEvaluationScenario.CorrectionSucceeds =>
                correctness.TaskContractPass &&
                resilience.CorrectionRecoveredWithinBound,
            HostedEvaluationScenario.IneffectiveProgress =>
                correctness.TaskContractPass &&
                resilience.CorrectionRecoveredWithinBound &&
                (arm != HostedEvaluationArm.Magentic ||
                 probe.Replans.Count > 0),
            _ => correctness.TaskContractPass,
        };
    }

    private static HostedEvaluationRunObservation EmptyObservation() =>
        new(
            Result: null,
            BeforeSynthesis: null,
            FailureCode: null,
            Canceled: false,
            CheckpointEvents: 0,
            ProviderCallLimit:
                HostedSynthesisArmFactory.MaxProviderCalls,
            ProviderCallsUsed: 0);

    internal static HostedEvaluationExecutionStatus ClassifyCancellation(
        HostedEvaluationRunObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return observation.Canceled
            ? HostedEvaluationExecutionStatus.Canceled
            : HostedEvaluationExecutionStatus.Failed;
    }

    private static HostedEvaluationProvenance CreateProvenance(
        HostedEvaluationProtocol protocol,
        HostedEvaluationCase @case,
        string trialId,
        HostedEvaluationArm arm,
        string? observedModel,
        HostedFaultPlan faultPlan)
    {
        string configuration = string.Join(
            "|",
            HostedEvaluationProtocol.Version,
            protocol.Model,
            arm,
            @case.Scenario,
            HostedSynthesisArmFactory.MaxProviderCalls,
            HostedSynthesisArmFactory.MaxArtifactAttempts,
            faultPlan.Mode,
            faultPlan.ActivationPoint,
            faultPlan.TargetRole);
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
