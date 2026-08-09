namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferencePipelineRequest(
    string RunId,
    string Topic);
