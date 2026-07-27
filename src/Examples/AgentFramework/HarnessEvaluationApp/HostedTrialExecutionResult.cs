using NexusLabs.Foundry.Evaluation.Experiments;

namespace HarnessEvaluationApp;

internal sealed record HostedTrialExecutionResult(
    ExperimentItemStatus Status,
    HostedTrialOutput? Output,
    IReadOnlyList<ExperimentAttemptResult> Attempts,
    ExperimentFailure? Failure);
