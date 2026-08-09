namespace AutonomousPhasePipelineApp.Core;

internal sealed record ReferenceBranchDefinition(
    string Phase,
    int Ordinal,
    bool Required);
