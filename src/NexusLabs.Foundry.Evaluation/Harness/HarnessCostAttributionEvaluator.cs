using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores artifact/context cost attribution for one Harness run:
/// the attribution counters are internally valid (non-negative), and the attributed token cost,
/// artifact byte flows, and admitted context size are surfaced as numeric metrics for downstream paired
/// comparison.
/// </summary>
/// <remarks>
/// Reads the cost-attribution slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result; a present slice is scored, with any negative
/// counter driving the validity metric to <see langword="false"/>.
/// </remarks>
public sealed class HarnessCostAttributionEvaluator : IEvaluator
{
    /// <summary>Metric name for the attribution-validity rollup.</summary>
    public const string AttributionValidMetricName = "Harness Cost Attribution Valid";

    /// <summary>Metric name for the attributed cumulative token cost.</summary>
    public const string AttributedTokenCostMetricName = "Harness Attributed Token Cost";

    /// <summary>Metric name for the attributed artifact input bytes.</summary>
    public const string ArtifactInputBytesMetricName = "Harness Attributed Artifact Input Bytes";

    /// <summary>Metric name for the attributed artifact-derived output bytes.</summary>
    public const string ArtifactOutputBytesMetricName = "Harness Attributed Artifact Output Bytes";

    /// <summary>Metric name for the final admitted context size.</summary>
    public const string ContextFinalSizeMetricName = "Harness Attributed Context Final Size";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        AttributionValidMetricName,
        AttributedTokenCostMetricName,
        ArtifactInputBytesMetricName,
        ArtifactOutputBytesMetricName,
        ContextFinalSizeMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var cost = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.CostAttribution;

        if (cost is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var valid =
            cost.ArtifactInputUtf8Bytes >= 0 &&
            cost.ArtifactOutputUtf8Bytes >= 0 &&
            cost.ContextOriginalSize >= 0 &&
            cost.ContextFinalSize >= 0 &&
            cost.AttributedTokenCost >= 0;

        var validMetric = new BooleanMetric(
            AttributionValidMetricName,
            value: valid,
            reason: valid
                ? "All cost-attribution counters were non-negative and internally valid."
                : "At least one cost-attribution counter was negative.");

        var tokenCostMetric = new NumericMetric(
            AttributedTokenCostMetricName,
            value: cost.AttributedTokenCost,
            reason: $"Attributed cumulative token cost: {cost.AttributedTokenCost}.");

        var artifactInputMetric = new NumericMetric(
            ArtifactInputBytesMetricName,
            value: cost.ArtifactInputUtf8Bytes,
            reason: $"Attributed artifact input bytes: {cost.ArtifactInputUtf8Bytes}.");

        var artifactOutputMetric = new NumericMetric(
            ArtifactOutputBytesMetricName,
            value: cost.ArtifactOutputUtf8Bytes,
            reason: $"Attributed artifact-derived output bytes: {cost.ArtifactOutputUtf8Bytes}.");

        var contextFinalMetric = new NumericMetric(
            ContextFinalSizeMetricName,
            value: cost.ContextFinalSize,
            reason: $"Final admitted context size ({cost.MeasurementUnit}): {cost.ContextFinalSize}.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            validMetric,
            tokenCostMetric,
            artifactInputMetric,
            artifactOutputMetric,
            contextFinalMetric));
    }
}
