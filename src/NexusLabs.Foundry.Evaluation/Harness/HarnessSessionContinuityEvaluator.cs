using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores conversation/decision continuity for one Harness run:
/// every required decision reference and required structured-state key was retained across the run.
/// </summary>
/// <remarks>
/// Reads the session-continuity slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result; a present slice is scored, with any missing
/// required reference or key driving the boolean metric to <see langword="false"/>.
/// </remarks>
public sealed class HarnessSessionContinuityEvaluator : IEvaluator
{
    /// <summary>Metric name for the continuity-preserved rollup.</summary>
    public const string ContinuityPreservedMetricName = "Harness Continuity Preserved";

    /// <summary>Metric name for the count of missing required decision references.</summary>
    public const string MissingDecisionReferencesMetricName = "Harness Missing Decision References";

    /// <summary>Metric name for the count of missing required structured-state keys.</summary>
    public const string MissingStateKeysMetricName = "Harness Missing State Keys";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        ContinuityPreservedMetricName,
        MissingDecisionReferencesMetricName,
        MissingStateKeysMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var continuity = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.SessionContinuity;

        if (continuity is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var collectionsValid =
            IsValidCollection(continuity.RequiredDecisionReferences) &&
            IsValidCollection(continuity.PresentDecisionReferences) &&
            IsValidCollection(continuity.RequiredStateKeys) &&
            IsValidCollection(continuity.PresentStateKeys);
        var presentDecisions = new HashSet<string>(
            continuity.PresentDecisionReferences?
                .Where(value => !string.IsNullOrWhiteSpace(value)) ?? [],
            StringComparer.Ordinal);
        var presentKeys = new HashSet<string>(
            continuity.PresentStateKeys?
                .Where(value => !string.IsNullOrWhiteSpace(value)) ?? [],
            StringComparer.Ordinal);

        var missingDecisions = CountMissing(continuity.RequiredDecisionReferences, presentDecisions);
        var missingKeys = CountMissing(continuity.RequiredStateKeys, presentKeys);

        var preserved = collectionsValid && missingDecisions == 0 && missingKeys == 0;

        var preservedMetric = new BooleanMetric(
            ContinuityPreservedMetricName,
            value: preserved,
            reason: preserved
                ? "Every required decision reference and structured-state key was retained."
                : $"{missingDecisions} required decision reference(s) and {missingKeys} required state key(s) were missing.");

        var missingDecisionsMetric = new NumericMetric(
            MissingDecisionReferencesMetricName,
            value: missingDecisions,
            reason: $"{missingDecisions} required decision reference(s) were missing from the retained state.");

        var missingKeysMetric = new NumericMetric(
            MissingStateKeysMetricName,
            value: missingKeys,
            reason: $"{missingKeys} required structured-state key(s) were missing from the retained state.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            preservedMetric,
            missingDecisionsMetric,
            missingKeysMetric));
    }

    private static bool IsValidCollection(IReadOnlyList<string>? values) =>
        values is not null &&
        values.All(value => !string.IsNullOrWhiteSpace(value));

    private static int CountMissing(IReadOnlyList<string>? required, HashSet<string> present)
    {
        if (required is null)
        {
            return 1;
        }

        return required.Count(value =>
            string.IsNullOrWhiteSpace(value) ||
            !present.Contains(value));
    }
}
