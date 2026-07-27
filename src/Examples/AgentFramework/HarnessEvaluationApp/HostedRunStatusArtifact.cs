using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp;

internal sealed record HostedRunStatusArtifact(
    string SchemaVersion,
    HarnessHostedRunState State,
    string Reason,
    int ScheduledBatchCount,
    int TotalBatchCount,
    int AttemptsUsed,
    int ProviderRequestsUsed,
    decimal EstimatedCostUsd,
    bool AdvisoryOnly);
