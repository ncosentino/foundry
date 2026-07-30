namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Describes one trial's deterministic binary value for a Harness evaluation dimension.
/// </summary>
public sealed record HarnessComparisonBinaryTrialValue
{
    /// <summary>
    /// Initializes one binary trial value.
    /// </summary>
    /// <param name="dimension">The binary evaluation dimension.</param>
    /// <param name="value">The deterministic value, or <see langword="null"/> when unscorable.</param>
    /// <param name="isComparable">Whether the required diagnostics schema is comparable across arms.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimension"/> is not a binary Harness dimension.
    /// </exception>
    public HarnessComparisonBinaryTrialValue(
        HarnessEvaluationDimension dimension,
        bool? value,
        bool isComparable)
    {
        if (!HarnessComparisonDimensionClassification.IsBinary(dimension))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "The dimension is not a binary Harness comparison dimension.");
        }

        Dimension = dimension;
        Value = value;
        IsComparable = isComparable;
    }

    /// <summary>Gets the binary evaluation dimension.</summary>
    public HarnessEvaluationDimension Dimension { get; }

    /// <summary>Gets the deterministic value, or <see langword="null"/> when unscorable.</summary>
    public bool? Value { get; }

    /// <summary>Gets whether the dimension is comparable across arms for this trial.</summary>
    public bool IsComparable { get; }
}
