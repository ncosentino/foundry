using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp;

internal sealed record HostedBatchArtifact(
    int BatchSequence,
    HostedBatchKey Batch,
    IReadOnlyList<HarnessComparisonArm> ArmOrder,
    IReadOnlyDictionary<HarnessComparisonArm, HostedTrialExecutionResult> Results,
    int AttemptsUsed,
    int ProviderRequestsUsed,
    decimal EstimatedCostUsd);
