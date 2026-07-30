using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp;

internal sealed record HostedCaseDefinition(
    string Id,
    string Prompt,
    string OutputPath,
    string ExpectedOutput,
    IReadOnlyList<string> RequiredTools,
    IReadOnlyList<string> ForbiddenTools,
    IReadOnlyList<HarnessEvaluationDimension> BinaryDimensions,
    IReadOnlyList<HarnessEvaluationDimension> ContinuousDimensions,
    bool ExpectsTimeout);
