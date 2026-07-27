namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Creates paired X-minus-Y comparison evidence for binary and continuous case-level analyses.
/// </summary>
public static class ExperimentPairedComparisonEvidence
{
    /// <summary>
    /// Creates paired binary evidence using the X-minus-Y orientation from the supplied case pairs.
    /// </summary>
    /// <param name="xLabel">The stable label for arm X.</param>
    /// <param name="yLabel">The stable label for arm Y.</param>
    /// <param name="cases">The paired case outcomes to analyze.</param>
    /// <param name="unknownSampleTreatment">
    /// The treatment for comparable case pairs whose binary outcome is unscorable for one or both
    /// arms.
    /// </param>
    /// <param name="confidenceLevel">The two-sided interval confidence level.</param>
    /// <returns>Validated paired binary comparison evidence.</returns>
    public static ExperimentPairedBinaryComparisonEvidence CreateBinary(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedBinaryCaseOutcome> cases,
        ExperimentUnknownSampleTreatment unknownSampleTreatment,
        double confidenceLevel) =>
        ExperimentPairedBinaryComparisonEvidence.Create(
            xLabel,
            yLabel,
            cases,
            unknownSampleTreatment,
            confidenceLevel);

    /// <summary>
    /// Creates paired continuous evidence using the X-minus-Y orientation from the supplied case
    /// pairs.
    /// </summary>
    /// <param name="xLabel">The stable label for arm X.</param>
    /// <param name="yLabel">The stable label for arm Y.</param>
    /// <param name="cases">The paired continuous case measurements to analyze.</param>
    /// <param name="bootstrapSeed">
    /// The deterministic seed for the internal 10,000-resample case-level bootstrap.
    /// </param>
    /// <param name="confidenceLevel">The two-sided interval confidence level.</param>
    /// <returns>Validated paired continuous comparison evidence.</returns>
    public static ExperimentPairedContinuousComparisonEvidence CreateContinuous(
        string xLabel,
        string yLabel,
        IReadOnlyList<ExperimentPairedContinuousCaseMeasurement> cases,
        ulong bootstrapSeed,
        double confidenceLevel) =>
        ExperimentPairedContinuousComparisonEvidence.Create(
            xLabel,
            yLabel,
            cases,
            bootstrapSeed,
            confidenceLevel);
}
