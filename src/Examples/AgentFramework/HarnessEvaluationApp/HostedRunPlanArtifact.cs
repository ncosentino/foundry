namespace HarnessEvaluationApp;

internal sealed record HostedRunPlanArtifact(
    string SchemaVersion,
    string ModelId,
    ulong GlobalRunSeed,
    ulong BatchOrderingSeed,
    ulong ArmOrderingSeed,
    ulong BootstrapSeed,
    int MaximumAttempts,
    int MaximumRequests,
    int MaximumRequestsPerAttempt,
    int MaximumOutputTokens,
    int MinimumProviderRequestIntervalMilliseconds,
    int WorkflowTimeoutMinutes,
    int SchedulingDeadlineMinutes,
    int AttemptTimeoutSeconds,
    int MaximumConcurrency,
    decimal CostCapUsd,
    decimal EstimatedCostPerRequest,
    IReadOnlyList<HostedBatchKey> Batches);
