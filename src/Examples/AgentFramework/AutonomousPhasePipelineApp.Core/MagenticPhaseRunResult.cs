namespace AutonomousPhasePipelineApp.Core;

internal sealed record MagenticPhaseRunResult(
    bool Succeeded,
    string? FinalText,
    string? FailureCode);
