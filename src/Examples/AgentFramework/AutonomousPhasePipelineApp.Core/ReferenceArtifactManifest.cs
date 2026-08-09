namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferenceArtifactManifest(
    string RunId,
    ReferenceArtifactReference Research,
    ReferencePhaseArtifact[] Branches,
    ReferencePipelineOutcome Outcome,
    string[] Gaps);
