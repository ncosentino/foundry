namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferenceArtifactReference(
    string Id,
    string Digest,
    int Utf8Bytes,
    string MediaType);
