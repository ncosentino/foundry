using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides primary pessimistic and inconclusive paired evidence for one binary dimension.
/// </summary>
public sealed record HarnessBinaryDimensionComparison
{
    internal HarnessBinaryDimensionComparison(
        HarnessEvaluationDimension dimension,
        int incompleteDueToCapCaseCount,
        ExperimentPairedBinaryComparisonEvidence pessimistic,
        ExperimentPairedBinaryComparisonEvidence inconclusive)
    {
        Dimension = dimension;
        IncompleteDueToCapCaseCount = incompleteDueToCapCaseCount;
        Pessimistic = pessimistic;
        Inconclusive = inconclusive;
    }

    /// <summary>Gets the binary evaluation dimension.</summary>
    public HarnessEvaluationDimension Dimension { get; }

    /// <summary>Gets the number of cases excluded because all three paired batches were not scheduled.</summary>
    public int IncompleteDueToCapCaseCount { get; }

    /// <summary>Gets the protocol-primary pessimistic three-trial-majority evidence.</summary>
    public ExperimentPairedBinaryComparisonEvidence Pessimistic { get; }

    /// <summary>Gets the sensitivity evidence that excludes any unscorable case/arm cell.</summary>
    public ExperimentPairedBinaryComparisonEvidence Inconclusive { get; }
}
