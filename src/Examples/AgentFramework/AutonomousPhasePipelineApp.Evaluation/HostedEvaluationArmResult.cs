using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationArmResult(
    HostedEvaluationArm Arm,
    HostedEvaluationScenario Scenario,
    bool Applicable,
    bool ScenarioContractPass,
    ReferencePipelineOutcome? PipelineOutcome,
    ReferencePipelineOutcome? SynthesisOutcome,
    string TraceId,
    string? ArtifactDigest,
    string? OutputText,
    HostedEvaluationCorrectness Correctness,
    HostedEvaluationResilience Resilience,
    HostedEvaluationResources Resources,
    HostedEvaluationInfrastructure Infrastructure,
    HostedEvaluationProvenance Provenance);
