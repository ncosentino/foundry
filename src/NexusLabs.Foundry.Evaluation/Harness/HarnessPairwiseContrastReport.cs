namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Provides every deterministic paired dimension comparison for one ordered X-minus-Y arm contrast.
/// </summary>
public sealed record HarnessPairwiseContrastReport
{
    internal HarnessPairwiseContrastReport(
        HarnessComparisonArm xArm,
        HarnessComparisonArm yArm,
        HarnessDiagnosticsParityComparison diagnosticsParity,
        IReadOnlyList<HarnessBinaryDimensionComparison> binaryDimensions,
        IReadOnlyList<HarnessContinuousDimensionComparison> continuousDimensions)
    {
        XArm = xArm;
        YArm = yArm;
        DiagnosticsParity = diagnosticsParity;
        BinaryDimensions = Array.AsReadOnly(binaryDimensions.ToArray());
        ContinuousDimensions = Array.AsReadOnly(continuousDimensions.ToArray());
    }

    /// <summary>Gets arm X in the X-minus-Y orientation.</summary>
    public HarnessComparisonArm XArm { get; }

    /// <summary>Gets arm Y in the X-minus-Y orientation.</summary>
    public HarnessComparisonArm YArm { get; }

    /// <summary>Gets the diagnostics comparability precondition report.</summary>
    public HarnessDiagnosticsParityComparison DiagnosticsParity { get; }

    /// <summary>Gets binary dimension comparisons in stable dimension order.</summary>
    public IReadOnlyList<HarnessBinaryDimensionComparison> BinaryDimensions { get; }

    /// <summary>Gets continuous dimension comparisons in stable dimension order.</summary>
    public IReadOnlyList<HarnessContinuousDimensionComparison> ContinuousDimensions { get; }
}
