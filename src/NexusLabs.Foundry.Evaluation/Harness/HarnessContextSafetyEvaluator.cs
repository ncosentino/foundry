using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores context-window safety for one Harness run from its
/// <see cref="HarnessContextCompactionEvidence"/> attempts: no successful assembly exceeds the hard
/// limit, and every admitted assembly is structurally valid (verified sequence and consistent
/// per-category attribution).
/// </summary>
/// <remarks>
/// Reads the context-compaction slice from the <see cref="HarnessRunEvaluationContext"/>. A
/// <see langword="null"/> slice returns an empty result ("not applicable"); a present slice — even an
/// empty one — is scored. Invalid attempts drive the boolean metrics to <see langword="false"/>.
/// </remarks>
public sealed class HarnessContextSafetyEvaluator : IEvaluator
{
    /// <summary>Metric name for the no-overflow safety rollup.</summary>
    public const string NoOverflowMetricName = "Harness Context No Overflow";

    /// <summary>Metric name for the structural-validity rollup.</summary>
    public const string StructurallyValidMetricName = "Harness Context Structurally Valid";

    /// <summary>Metric name for the count of successful assemblies that exceeded the hard limit.</summary>
    public const string OverflowCountMetricName = "Harness Context Overflow Count";

    /// <summary>Metric name for the number of compaction attempts observed.</summary>
    public const string CompactionCountMetricName = "Harness Context Compaction Count";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        NoOverflowMetricName,
        StructurallyValidMetricName,
        OverflowCountMetricName,
        CompactionCountMetricName,
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

        var overflowCount = 0;
        var structurallyValid = true;
        foreach (var attempt in compactions)
        {
            if (attempt is null)
            {
                structurallyValid = false;
                continue;
            }

            var outcomeDefined = Enum.IsDefined(attempt.Outcome);
            if (!outcomeDefined ||
                !Enum.IsDefined(attempt.MeasurementUnit) ||
                attempt.Stages is null ||
                attempt.Stages.Any(stage => !Enum.IsDefined(stage)) ||
                attempt.OriginalSize < 0 ||
                attempt.FinalSize < 0 ||
                attempt.TriggerThreshold < 0 ||
                attempt.HardLimit < 0 ||
                attempt.AttemptCount < 0 ||
                attempt.CategoryContributionSizeSum < 0 ||
                attempt.CategoryContributionCount < 0)
            {
                structurallyValid = false;
            }

            if (!outcomeDefined)
            {
                continue;
            }

            var isSuccess = HarnessCompactionClassification.IsSuccess(attempt.Outcome);

            if (isSuccess && attempt.FinalSize > attempt.HardLimit)
            {
                overflowCount++;
            }

            if (isSuccess)
            {
                if (attempt.FinalSequenceValid != true ||
                    attempt.CategoryContributionSizeSum != attempt.FinalSize ||
                    attempt.CategoryContributionCount <= 0)
                {
                    structurallyValid = false;
                }
            }
            else
            {
                // A termination never dispatches final entries: no verified sequence, no attribution.
                if (attempt.FinalSequenceValid is not null ||
                    attempt.CategoryContributionSizeSum != 0 ||
                    attempt.CategoryContributionCount != 0)
                {
                    structurallyValid = false;
                }
            }
        }

        var noOverflow = overflowCount == 0;

        var noOverflowMetric = new BooleanMetric(
            NoOverflowMetricName,
            value: noOverflow,
            reason: noOverflow
                ? "No admitted context assembly exceeded its hard limit."
                : $"{overflowCount} admitted context assembly(ies) exceeded the hard limit.");

        var structurallyValidMetric = new BooleanMetric(
            StructurallyValidMetricName,
            value: structurallyValid,
            reason: structurallyValid
                ? "Every compaction attempt was structurally consistent for its outcome family."
                : "At least one compaction attempt was structurally inconsistent for its outcome family.");

        var overflowCountMetric = new NumericMetric(
            OverflowCountMetricName,
            value: overflowCount,
            reason: $"{overflowCount} successful assembly(ies) exceeded the hard limit.");

        var compactionCountMetric = new NumericMetric(
            CompactionCountMetricName,
            value: compactions.Count,
            reason: $"{compactions.Count} compaction attempt(s) were observed.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            noOverflowMetric,
            structurallyValidMetric,
            overflowCountMetric,
            compactionCountMetric));
    }
}
