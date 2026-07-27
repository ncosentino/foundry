using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores compaction-decision validity for one Harness run: each
/// attempt's reported outcome is internally consistent with its sizes, stages, and attribution, and
/// every size-reducing outcome is actually monotonic (final size never exceeds original size).
/// </summary>
/// <remarks>
/// Reads the context-compaction slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result; a present slice is scored, with any invalid
/// attempt driving the boolean metrics to <see langword="false"/>.
/// </remarks>
public sealed class HarnessCompactionValidityEvaluator : IEvaluator
{
    /// <summary>Metric name for the outcome-consistency rollup.</summary>
    public const string OutcomeConsistentMetricName = "Harness Compaction Outcome Consistent";

    /// <summary>Metric name for the reduced-monotonicity rollup.</summary>
    public const string ReducedMonotonicMetricName = "Harness Compaction Reduced Monotonic";

    /// <summary>Metric name for the number of terminating attempts.</summary>
    public const string TerminationCountMetricName = "Harness Compaction Termination Count";

    /// <summary>Metric name for the total number of bounded recompaction attempts consumed.</summary>
    public const string TotalAttemptsMetricName = "Harness Compaction Total Attempts";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        OutcomeConsistentMetricName,
        ReducedMonotonicMetricName,
        TerminationCountMetricName,
        TotalAttemptsMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var compactions = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.ContextCompactions;

        if (compactions is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var outcomeConsistent = true;
        var reducedMonotonic = true;
        var terminationCount = 0;
        var totalAttempts = 0;

        foreach (var attempt in compactions)
        {
            totalAttempts += Math.Max(0, attempt.AttemptCount);

            if (attempt.OriginalSize < 0 || attempt.FinalSize < 0 ||
                attempt.HardLimit < 0 || attempt.TriggerThreshold < 0 || attempt.AttemptCount < 0)
            {
                outcomeConsistent = false;
            }

            if (HarnessCompactionClassification.IsSuccess(attempt.Outcome))
            {
                if (attempt.FinalSize > attempt.HardLimit ||
                    attempt.FinalSequenceValid != true ||
                    attempt.CategoryContributionSizeSum != attempt.FinalSize)
                {
                    outcomeConsistent = false;
                }

                if (attempt.Outcome == HarnessContextCompactionOutcome.Reduced &&
                    attempt.FinalSize > attempt.OriginalSize)
                {
                    reducedMonotonic = false;
                }
            }
            else
            {
                terminationCount++;
                if (attempt.FinalSequenceValid is not null || attempt.CategoryContributionCount != 0)
                {
                    outcomeConsistent = false;
                }
            }
        }

        var outcomeConsistentMetric = new BooleanMetric(
            OutcomeConsistentMetricName,
            value: outcomeConsistent,
            reason: outcomeConsistent
                ? "Every compaction outcome was internally consistent with its sizes and attribution."
                : "At least one compaction outcome was internally inconsistent.");

        var reducedMonotonicMetric = new BooleanMetric(
            ReducedMonotonicMetricName,
            value: reducedMonotonic,
            reason: reducedMonotonic
                ? "Every reduced outcome shrank the assembled size."
                : "At least one reduced outcome did not shrink the assembled size.");

        var terminationCountMetric = new NumericMetric(
            TerminationCountMetricName,
            value: terminationCount,
            reason: $"{terminationCount} compaction attempt(s) terminated without a dispatchable context.");

        var totalAttemptsMetric = new NumericMetric(
            TotalAttemptsMetricName,
            value: totalAttempts,
            reason: $"{totalAttempts} bounded recompaction attempt(s) were consumed in total.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            outcomeConsistentMetric,
            reducedMonotonicMetric,
            terminationCountMetric,
            totalAttemptsMetric));
    }
}
