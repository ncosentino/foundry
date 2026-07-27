using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores identity attribution for one Harness run: every
/// attributed record carries the expected owning agent identity and no record that should have been
/// attributed was left unattributed.
/// </summary>
/// <remarks>
/// Reads the identity-attribution slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result; a present slice is scored, with any foreign or
/// missing attribution driving the boolean metrics to <see langword="false"/>.
/// </remarks>
public sealed class HarnessIdentityAttributionEvaluator : IEvaluator
{
    /// <summary>Metric name for the attribution-correctness rollup.</summary>
    public const string AttributedMetricName = "Harness Identity Attributed";

    /// <summary>Metric name for the single-owner rollup.</summary>
    public const string SingleOwnerMetricName = "Harness Identity Single Owner";

    /// <summary>Metric name for the count of unattributed records.</summary>
    public const string UnattributedCountMetricName = "Harness Unattributed Record Count";

    /// <summary>Metric name for the expected owning agent identity.</summary>
    public const string ExpectedAgentMetricName = "Harness Expected Agent";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        AttributedMetricName,
        SingleOwnerMetricName,
        UnattributedCountMetricName,
        ExpectedAgentMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var attribution = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.IdentityAttribution;

        if (attribution is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var observed = attribution.ObservedAgentIds;
        var foreignOwner = observed.Any(id => !string.Equals(id, attribution.ExpectedAgentId, StringComparison.Ordinal));
        var distinctOwners = observed
            .Distinct(StringComparer.Ordinal)
            .Count();

        var attributed = attribution.UnattributedRecordCount == 0 && !foreignOwner;
        var singleOwner = distinctOwners <= 1;

        var attributedMetric = new BooleanMetric(
            AttributedMetricName,
            value: attributed,
            reason: attributed
                ? "Every attributed record carried the expected owning agent identity."
                : foreignOwner
                    ? "At least one record was attributed to a foreign agent identity."
                    : $"{attribution.UnattributedRecordCount} record(s) that required attribution were unattributed.");

        var singleOwnerMetric = new BooleanMetric(
            SingleOwnerMetricName,
            value: singleOwner,
            reason: singleOwner
                ? "All attributed records shared a single owning agent identity."
                : $"{distinctOwners} distinct owning agent identities were observed.");

        var unattributedMetric = new NumericMetric(
            UnattributedCountMetricName,
            value: attribution.UnattributedRecordCount,
            reason: $"{attribution.UnattributedRecordCount} record(s) were unattributed.");

        var expectedAgentMetric = new StringMetric(
            ExpectedAgentMetricName,
            value: attribution.ExpectedAgentId,
            reason: $"The expected owning agent identity was '{attribution.ExpectedAgentId}'.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            attributedMetric,
            singleOwnerMetric,
            unattributedMetric,
            expectedAgentMetric));
    }
}
