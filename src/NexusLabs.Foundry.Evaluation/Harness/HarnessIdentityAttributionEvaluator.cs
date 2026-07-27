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

        var expectedAgentValid = !string.IsNullOrWhiteSpace(attribution.ExpectedAgentId);
        var workflowValid = !string.IsNullOrWhiteSpace(attribution.WorkflowId);
        var observedValid =
            attribution.ObservedAgentIds is not null &&
            attribution.ObservedAgentIds.All(id => !string.IsNullOrWhiteSpace(id));
        var countValid = attribution.UnattributedRecordCount >= 0;
        var observed = attribution.ObservedAgentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray() ?? [];
        var foreignOwner =
            !expectedAgentValid ||
            observed.Any(id => !string.Equals(id, attribution.ExpectedAgentId, StringComparison.Ordinal));
        var distinctOwners = observed
            .Distinct(StringComparer.Ordinal)
            .Count();

        var attributed =
            workflowValid &&
            expectedAgentValid &&
            observedValid &&
            countValid &&
            attribution.UnattributedRecordCount == 0 &&
            !foreignOwner;
        var singleOwner = observedValid && expectedAgentValid && distinctOwners <= 1;
        var unattributedRecordCount = Math.Max(0, attribution.UnattributedRecordCount);

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
            value: unattributedRecordCount,
            reason: countValid
                ? $"{unattributedRecordCount} record(s) were unattributed."
                : "The unattributed-record count was invalid.");

        var expectedAgentMetric = new StringMetric(
            ExpectedAgentMetricName,
            value: expectedAgentValid ? attribution.ExpectedAgentId : "(invalid)",
            reason: expectedAgentValid
                ? $"The expected owning agent identity was '{attribution.ExpectedAgentId}'."
                : "The expected owning agent identity was blank.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            attributedMetric,
            singleOwnerMetric,
            unattributedMetric,
            expectedAgentMetric));
    }
}
