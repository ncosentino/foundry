using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Deterministic, per-item evaluator that scores artifact rehydration behaviour for one Harness run
/// from its <see cref="HarnessArtifactDecisionEvidence"/> rehydration decisions: every decision carries
/// the reference it resolved, resolved decisions produce a body and verify their digest, and
/// non-resolved decisions commit no artifact-derived output.
/// </summary>
/// <remarks>
/// Reads the artifact-decisions slice from the <see cref="HarnessRunEvaluationContext"/> and considers
/// only <see cref="HarnessArtifactOperationCategory.Rehydration"/> decisions. A <see langword="null"/>
/// slice returns an empty result; a present slice is scored, with any inconsistent decision driving the
/// boolean metrics to <see langword="false"/>.
/// </remarks>
public sealed class HarnessArtifactRehydrationEvaluator : IEvaluator
{
    /// <summary>Metric name for the number of rehydration decisions observed.</summary>
    public const string RehydrationCountMetricName = "Harness Artifact Rehydration Count";

    /// <summary>Metric name for the number of successfully resolved rehydration decisions.</summary>
    public const string ResolvedCountMetricName = "Harness Artifact Resolved Count";

    /// <summary>Metric name for the rehydration-consistency rollup.</summary>
    public const string RehydrationConsistentMetricName = "Harness Artifact Rehydration Consistent";

    /// <summary>Metric name for the digest-verification rollup over resolved decisions.</summary>
    public const string DigestVerifiedMetricName = "Harness Artifact Digest Verified";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
    [
        RehydrationCountMetricName,
        ResolvedCountMetricName,
        RehydrationConsistentMetricName,
        DigestVerifiedMetricName,
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

        var rehydrationCount = 0;
        var resolvedCount = 0;
        var consistent = true;
        var digestVerified = true;

        foreach (var decision in decisions)
        {
            if (decision.Operation != HarnessArtifactOperationCategory.Rehydration)
            {
                continue;
            }

            rehydrationCount++;

            // The reference being resolved is always known upfront for a rehydration decision.
            if (string.IsNullOrWhiteSpace(decision.ReferenceId))
            {
                consistent = false;
            }

            if (decision.Outcome == HarnessArtifactOutcomeCategory.Resolved)
            {
                resolvedCount++;
                if (decision.OutputUtf8Bytes is null)
                {
                    consistent = false;
                }

                if (decision.Reason != HarnessArtifactDecisionReason.DigestVerified)
                {
                    digestVerified = false;
                }
            }
            else if (decision.OutputUtf8Bytes is not null)
            {
                // A non-resolved rehydration commits no artifact-derived output.
                consistent = false;
            }
        }

        var rehydrationCountMetric = new NumericMetric(
            RehydrationCountMetricName,
            value: rehydrationCount,
            reason: $"{rehydrationCount} rehydration decision(s) were observed.");

        var resolvedCountMetric = new NumericMetric(
            ResolvedCountMetricName,
            value: resolvedCount,
            reason: $"{resolvedCount} rehydration decision(s) resolved successfully.");

        var consistentMetric = new BooleanMetric(
            RehydrationConsistentMetricName,
            value: consistent,
            reason: consistent
                ? "Every rehydration decision's output matched its outcome."
                : "At least one rehydration decision's output was inconsistent with its outcome.");

        var digestVerifiedMetric = new BooleanMetric(
            DigestVerifiedMetricName,
            value: digestVerified,
            reason: digestVerified
                ? "Every resolved rehydration decision verified its recorded digest."
                : "At least one resolved rehydration decision did not report a verified digest.");

        return new ValueTask<EvaluationResult>(new EvaluationResult(
            rehydrationCountMetric,
            resolvedCountMetric,
            consistentMetric,
            digestVerifiedMetric));
    }
}
