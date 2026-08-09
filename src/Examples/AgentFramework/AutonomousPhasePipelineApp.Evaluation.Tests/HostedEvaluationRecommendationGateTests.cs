using AutonomousPhasePipelineApp.Core;
using AutonomousPhasePipelineApp.Evaluation;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedEvaluationRecommendationGateTests
{
    [Fact]
    public void MissingRequiredFaultActivation_ExcludesOnlyThatItem()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 3);
        HostedEvaluationArmResult result = CreateResult(
            HostedEvaluationArm.HarnessPlain,
            faultRequired: true,
            faultActivated: false);

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks([result]),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus
                .InsufficientlyPowered,
            decision.Status);
        Assert.Equal(1, decision.ExcludedResults);
    }

    [Fact]
    public void BudgetMismatch_InvalidatesProtocol()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 3);
        HostedEvaluationArmResult result = CreateResult(
            HostedEvaluationArm.HarnessPlain) with
        {
            Resources = CreateResult(
                HostedEvaluationArm.HarnessPlain)
                .Resources with
            {
                ProviderCallLimit = 25,
            },
        };

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks([result]),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus.ProtocolInvalid,
            decision.Status);
    }

    [Fact]
    public void MissingObservedModel_InvalidatesProtocol()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 3);
        HostedEvaluationArmResult[] results =
        [
            CreateResult(
                HostedEvaluationArm.HarnessPlain,
                observedModel: null),
            CreateResult(
                HostedEvaluationArm.HarnessDelegated),
            CreateResult(
                HostedEvaluationArm.Magentic),
        ];

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks(results),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus.ProtocolInvalid,
            decision.Status);
    }

    [Fact]
    public void InfrastructureFailureRateAboveLimit_IsRejected()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 3);
        HostedEvaluationArmResult[] results =
        [
            .. Enumerable.Range(0, 9)
                .Select(index => CreateResult(
                    (HostedEvaluationArm)(index % 3),
                    providerFailures: index < 2 ? 1 : 0)),
        ];

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks(results),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus
                .InfrastructureUnreliable,
            decision.Status);
    }

    [Fact]
    public void OneTrial_RemainsInsufficientlyPowered()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 1);
        HostedEvaluationArmResult[] results =
        [
            CreateResult(HostedEvaluationArm.HarnessPlain),
            CreateResult(HostedEvaluationArm.HarnessDelegated),
            CreateResult(HostedEvaluationArm.Magentic),
        ];

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks(results),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus
                .InsufficientlyPowered,
            decision.Status);
    }

    [Fact]
    public void AdmissibleThreeTrialRun_RemainsPilotWithoutInferenceGate()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 3);
        HostedEvaluationArmResult[] results =
        [
            .. Enumerable.Range(0, 9)
                .Select(index => CreateResult(
                    (HostedEvaluationArm)(index % 3))),
        ];

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks(results),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus.Pilot,
            decision.Status);
    }

    [Fact]
    public void NonPoolableControls_DoNotSatisfyPowerGate()
    {
        HostedEvaluationProtocol protocol = CreateProtocol(
            trialCount: 3);
        HostedEvaluationArmResult[] results =
        [
            .. Enumerable.Range(0, 9)
                .Select(index => CreateResult(
                    (HostedEvaluationArm)(index % 3),
                    scenario:
                        HostedEvaluationScenario.IneffectiveProgress)),
        ];

        HostedEvaluationRecommendationDecision decision =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                CreateBlocks(results),
                itemFailureCount: 0);

        Assert.Equal(
            HostedEvaluationRecommendationStatus
                .InsufficientlyPowered,
            decision.Status);
    }

    private static HostedEvaluationProtocol CreateProtocol(
        int trialCount) =>
        new()
        {
            Repository = "ncosentino/foundry",
            CommitSha = "commit",
            Ref = "ref",
            Model = "model",
            TrialCount = trialCount,
            OutputDirectory = "artifacts",
            RunId = "run",
            DatasetName = "dataset",
        };

    private static HostedEvaluationBlockResult[] CreateBlocks(
        IReadOnlyList<HostedEvaluationArmResult> results) =>
        results
            .Chunk(3)
            .Select((arms, index) =>
                new HostedEvaluationBlockResult(
                    $"block-{index}",
                    "case",
                    arms[0].Scenario,
                    index + 1,
                    arms.Select(result => result.Arm).ToArray(),
                    arms))
            .ToArray();

    private static HostedEvaluationArmResult CreateResult(
        HostedEvaluationArm arm,
        bool faultRequired = false,
        bool faultActivated = true,
        int providerFailures = 0,
        HostedEvaluationScenario scenario =
            HostedEvaluationScenario.Success,
        string? observedModel = "model") =>
        new(
            arm,
            scenario,
            Applicable: true,
            ScenarioContractPass: true,
            PipelineOutcome: ReferencePipelineOutcome.Completed,
            SynthesisOutcome: ReferencePipelineOutcome.Completed,
            TraceId: "trace",
            ArtifactDigest: "digest",
            OutputText: "{}",
            new HostedEvaluationCorrectness(
                ArtifactContractValid: true,
                EvidenceExactSet: true,
                GapsExactSet: true,
                BranchesExact: true,
                RequiredCoverageMatches: true,
                SuccessfulSiblingsPreserved: true,
                PipelineOutcomeMatches: true,
                SynthesisOutcomeMatches: true,
                AuthoritativeDeliveryExact: true,
                TaskContractPass: true,
                ValidationErrorCode: null),
            new HostedEvaluationResilience(
                FaultRequired: faultRequired,
                FaultActivated: faultActivated,
                FaultActivatedAfterProviderWork: true,
                FaultActivatedAfterCollaboratorWork: true,
                CancellationObserved: false,
                NoDeliveryAfterCancellation: true,
                RestoreAttempted: false,
                SameRunRestoreSucceeded: false,
                AcceptedPriorPhaseReran: false,
                ReplayIdempotent: true,
                CorrectionRecoveredWithinBound: true,
                FailureCode: null),
            new HostedEvaluationResources(
                ProviderCallLimit:
                    HostedSynthesisArmFactory.MaxProviderCalls,
                ProviderCallsUsed: 1,
                ModelCalls: 1,
                ToolCalls: 0,
                InputTokens: 1,
                OutputTokens: 1,
                CachedInputTokens: null,
                ChildSessionCount: 0,
                ChildFailureCount: 0,
                CheckpointCount: 0,
                RestoreEventCount: 0,
                PlanCount: 0,
                ReplanCount: 0,
                ProgressEventCount: 0,
                DurationMilliseconds: 1),
            new HostedEvaluationInfrastructure(
                HostedEvaluationExecutionStatus.Completed,
                ProviderFailureCount: providerFailures,
                PhaseFailureCount: 0,
                PostFaultOutputCaptured: true,
                SemanticQualityStatus:
                    HostedSemanticQualityStatus
                        .NotScoredCalibrationRequired),
            new HostedEvaluationProvenance(
                Repository: "ncosentino/foundry",
                CommitSha: "commit",
                Ref: "ref",
                ProtocolVersion:
                    HostedEvaluationProtocol.Version,
                RequestedModel: "model",
                ObservedModel: observedModel,
                FoundryVersion: "1",
                MafVersion: "1",
                MeaiVersion: "1",
                ConfigurationHash: "hash",
                CaseId: "case",
                TrialId: "trial"));
}
