using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

/// <summary>
/// Shared helpers for exercising the Harness deterministic evaluators from evidence slices.
/// </summary>
internal static class HarnessEvaluatorTestHarness
{
    /// <summary>
    /// Runs an evaluator with a single <see cref="HarnessRunEvaluationContext"/> built from the
    /// supplied evidence.
    /// </summary>
    public static ValueTask<EvaluationResult> RunAsync(
        IEvaluator evaluator,
        HarnessRunEvaluationEvidence evidence,
        CancellationToken cancellationToken)
    {
        return evaluator.EvaluateAsync(
            messages: [],
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: [new HarnessRunEvaluationContext(evidence)],
            cancellationToken: cancellationToken);
    }

    /// <summary>Runs an evaluator with no additional context.</summary>
    public static ValueTask<EvaluationResult> RunWithoutContextAsync(
        IEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        return evaluator.EvaluateAsync(
            messages: [],
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>Runs an evaluator with an explicit additional-context collection.</summary>
    public static ValueTask<EvaluationResult> RunWithContextsAsync(
        IEvaluator evaluator,
        IReadOnlyList<EvaluationContext> contexts,
        CancellationToken cancellationToken)
    {
        return evaluator.EvaluateAsync(
            messages: [],
            modelResponse: new ChatResponse(),
            chatConfiguration: null,
            additionalContext: contexts,
            cancellationToken: cancellationToken);
    }

    public static bool BooleanValue(EvaluationResult result, string name) =>
        Assert.IsType<BooleanMetric>(result.Metrics[name]).Value ?? throw new InvalidOperationException($"'{name}' has no value.");

    public static double NumericValue(EvaluationResult result, string name) =>
        Assert.IsType<NumericMetric>(result.Metrics[name]).Value ?? throw new InvalidOperationException($"'{name}' has no value.");

    public static string StringValue(EvaluationResult result, string name) =>
        Assert.IsType<StringMetric>(result.Metrics[name]).Value ?? throw new InvalidOperationException($"'{name}' has no value.");
}
