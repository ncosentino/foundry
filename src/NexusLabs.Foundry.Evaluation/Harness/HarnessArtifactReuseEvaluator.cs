using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores artifact offload and reuse behaviour for one Harness
/// run from its <see cref="HarnessArtifactDecisionEvidence"/> offload decisions: each decision's
/// reference identity is consistent with its outcome, and reused/committed references produce byte
/// savings.
/// </summary>
/// <remarks>
/// Reads the artifact-decisions slice from the <see cref="HarnessRunEvaluationContext"/> and considers
/// only <see cref="HarnessArtifactOperationCategory.Offload"/> decisions. A <see langword="null"/> slice
/// returns an empty result; a present slice is scored, with any inconsistent decision driving the
/// boolean metric to <see langword="false"/>.
/// </remarks>
public sealed class HarnessArtifactReuseEvaluator : IEvaluator
{
    /// <summary>Metric name for the number of offload decisions observed.</summary>
    public const string OffloadCountMetricName = "Harness Artifact Offload Count";

    /// <summary>Metric name for the number of reused (existing-reference) offload decisions.</summary>
    public const string ReuseCountMetricName = "Harness Artifact Reuse Count";

    /// <summary>Metric name for the offload-consistency rollup.</summary>
    public const string OffloadConsistentMetricName = "Harness Artifact Offload Consistent";

    /// <summary>Metric name for the total UTF-8 byte savings from committing/reusing references.</summary>
    public const string ByteSavingsMetricName = "Harness Artifact Offloaded Byte Savings";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        OffloadCountMetricName,
        ReuseCountMetricName,
        OffloadConsistentMetricName,
        ByteSavingsMetricName,
    ];

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var decisions = additionalContext?
            .OfType<HarnessRunEvaluationContext>()
            .FirstOrDefault()?
            .Evidence.ArtifactDecisions;

        if (decisions is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var offloadCount = 0;
        var reuseCount = 0;
        var consistent = true;
        long byteSavings = 0;

        foreach (var decision in decisions)
        {
            if (decision.Operation != HarnessArtifactOperationCategory.Offload)
            {
                continue;
            }

            offloadCount++;
            var committedReference =
                decision.Outcome is HarnessArtifactOutcomeCategory.Offloaded
                    or HarnessArtifactOutcomeCategory.ExistingReference;

            if (committedReference)
            {
                if (string.IsNullOrWhiteSpace(decision.ReferenceId) || decision.OutputUtf8Bytes is null)
                {
                    consistent = false;
                }
                else
                {
                    byteSavings += Math.Max(0, decision.InputUtf8Bytes - decision.OutputUtf8Bytes.Value);
                }

                if (decision.Outcome == HarnessArtifactOutcomeCategory.ExistingReference)
                {
                    reuseCount++;
                }
            }
            else if (decision.ReferenceId is not null)
            {
                // Inline/Failed/RecoveryRequired must never carry a committed reference identity.
                consistent = false;
            }
        }

        var offloadCountMetric = new NumericMetric(
            OffloadCountMetricName,
            value: offloadCount,
            reason: $"{offloadCount} offload decision(s) were observed.");

        var reuseCountMetric = new NumericMetric(
            ReuseCountMetricName,
            value: reuseCount,
            reason: $"{reuseCount} offload decision(s) reused an existing content-addressed reference.");

        var consistentMetric = new BooleanMetric(
            OffloadConsistentMetricName,
            value: consistent,
            reason: consistent
                ? "Every offload decision's reference identity matched its outcome."
                : "At least one offload decision carried a reference identity inconsistent with its outcome.");

        var byteSavingsMetric = new NumericMetric(
            ByteSavingsMetricName,
            value: byteSavings,
            reason: $"{byteSavings} UTF-8 byte(s) were saved by replacing content with references.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            offloadCountMetric,
            reuseCountMetric,
            consistentMetric,
            byteSavingsMetric));
    }
}
