using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides the conditional dual-success estimate and required pessimistic sensitivity for one
/// continuous dimension.
/// </summary>
public sealed record HarnessContinuousDimensionComparison
{
    internal HarnessContinuousDimensionComparison(
        HarnessEvaluationDimension dimension,
        int incompleteDueToCapCaseCount,
        ExperimentPairedContinuousComparisonEvidence conditional,
        ExperimentPairedContinuousPessimisticSensitivityEvidence pessimisticSensitivity)
    {
        Dimension = dimension;
        IncompleteDueToCapCaseCount = incompleteDueToCapCaseCount;
        Conditional = conditional;
        PessimisticSensitivity = pessimisticSensitivity;
    }

    /// <summary>Gets the continuous evaluation dimension.</summary>
    public HarnessEvaluationDimension Dimension { get; }

    /// <summary>Gets the number of cases excluded because all three paired batches were not scheduled.</summary>
    public int IncompleteDueToCapCaseCount { get; }

    /// <summary>Gets the conditional-on-dual-full-success paired evidence.</summary>
    public ExperimentPairedContinuousComparisonEvidence Conditional { get; }

    /// <summary>Gets the required scheduled-failure pessimistic sensitivity.</summary>
    public ExperimentPairedContinuousPessimisticSensitivityEvidence PessimisticSensitivity { get; }
}
