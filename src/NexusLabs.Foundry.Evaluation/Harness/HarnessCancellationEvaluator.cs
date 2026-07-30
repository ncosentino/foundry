using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores cancellation/timeout appropriateness for one Harness
/// run: the observed terminal category matches the category the case expected, and the run did not
/// produce a success-shaped output despite being canceled or timed out.
/// </summary>
/// <remarks>
/// Reads the cancellation slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result; a present slice is scored, with a mismatched
/// category or a success-shaped output driving the boolean metrics to <see langword="false"/>.
/// </remarks>
public sealed class HarnessCancellationEvaluator : IEvaluator
{
    /// <summary>Metric name for the overall cancellation-appropriateness rollup.</summary>
    public const string AppropriateMetricName = "Harness Cancellation Appropriate";

    /// <summary>Metric name for the terminal-category match.</summary>
    public const string CategoryMatchMetricName = "Harness Cancellation Category Match";

    /// <summary>Metric name for the no-success-shaped-output check.</summary>
    public const string NoSuccessShapedOutputMetricName = "Harness No Success Shaped Output";

    /// <summary>Metric name for the observed terminal category.</summary>
    public const string ObservedCategoryMetricName = "Harness Observed Terminal Category";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        AppropriateMetricName,
        CategoryMatchMetricName,
        NoSuccessShapedOutputMetricName,
        ObservedCategoryMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var cancellation = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.Cancellation;

        if (cancellation is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var categoriesDefined =
            Enum.IsDefined(cancellation.ExpectedCategory) &&
            Enum.IsDefined(cancellation.ObservedCategory);
        var categoryMatch =
            categoriesDefined &&
            cancellation.ObservedCategory == cancellation.ExpectedCategory;
        var noSuccessShaped = !cancellation.ProducedSuccessShapedOutput;
        var appropriate = categoriesDefined && categoryMatch && noSuccessShaped;
        var observedCategory = Enum.IsDefined(cancellation.ObservedCategory)
            ? cancellation.ObservedCategory.ToString()
            : "(invalid)";

        var appropriateMetric = new BooleanMetric(
            AppropriateMetricName,
            value: appropriate,
            reason: appropriate
                ? "The observed terminal category matched the expectation with no success-shaped output."
                : "The cancellation/timeout behaviour did not match the expectation.");

        var categoryMatchMetric = new BooleanMetric(
            CategoryMatchMetricName,
            value: categoryMatch,
            reason: categoryMatch
                ? $"The observed terminal category '{cancellation.ObservedCategory}' matched the expectation."
                : $"The observed terminal category '{cancellation.ObservedCategory}' did not match the expected '{cancellation.ExpectedCategory}'.");

        var noSuccessShapedMetric = new BooleanMetric(
            NoSuccessShapedOutputMetricName,
            value: noSuccessShaped,
            reason: noSuccessShaped
                ? "No success-shaped output was produced for the canceled/timed-out run."
                : "A success-shaped output was produced despite cancellation/timeout.");

        var observedCategoryMetric = new StringMetric(
            ObservedCategoryMetricName,
            value: observedCategory,
            reason: categoriesDefined
                ? $"The observed terminal category was '{observedCategory}'."
                : "The expected or observed terminal category was undefined.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            appropriateMetric,
            categoryMatchMetric,
            noSuccessShapedMetric,
            observedCategoryMetric));
    }
}
