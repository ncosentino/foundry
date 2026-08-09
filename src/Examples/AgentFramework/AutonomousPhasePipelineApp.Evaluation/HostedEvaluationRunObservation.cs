using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationRunObservation(
    ReferencePipelineResult? Result,
    CheckpointInfo? BeforeSynthesis,
    string? FailureCode,
    bool Canceled,
    int CheckpointEvents,
    int ProviderCallLimit,
    int ProviderCallsUsed);
