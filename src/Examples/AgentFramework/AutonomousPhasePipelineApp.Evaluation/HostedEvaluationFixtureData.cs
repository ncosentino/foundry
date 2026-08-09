using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationFixtureData(
    ReferencePhaseArtifact Manifest,
    HostedEvaluationExpectedOutput Expected);
