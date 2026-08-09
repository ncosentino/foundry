namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferencePipelineResult(
    string RunId,
    ReferencePipelineOutcome Outcome,
    string DeliveryId,
    ReferenceArtifactReference Manifest,
    ReferencePhaseArtifact Synthesis,
    ReferencePhaseArtifact[] Branches,
    string[] Gaps);
