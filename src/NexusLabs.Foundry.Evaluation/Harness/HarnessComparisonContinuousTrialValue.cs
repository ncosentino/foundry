namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Describes one trial's continuous measurement and predeclared pessimistic substitution value.
/// </summary>
public sealed record HarnessComparisonContinuousTrialValue
{
    /// <summary>
    /// Initializes one continuous trial value.
    /// </summary>
    /// <param name="dimension">The continuous evaluation dimension.</param>
    /// <param name="value">The finite observed value, or <see langword="null"/> when unscorable.</param>
    /// <param name="pessimisticScheduledFailureValue">
    /// The finite predeclared value substituted when a scheduled trial is failed or unscorable.
    /// </param>
    /// <param name="isComparable">Whether the required diagnostics schema is comparable across arms.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> or <paramref name="pessimisticScheduledFailureValue"/> is non-finite.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimension"/> is not a continuous Harness dimension.
    /// </exception>
    public HarnessComparisonContinuousTrialValue(
        HarnessEvaluationDimension dimension,
        double? value,
        double pessimisticScheduledFailureValue,
        bool isComparable)
    {
        if (!HarnessComparisonDimensionClassification.IsContinuous(dimension))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "The dimension is not a continuous Harness comparison dimension.");
        }

        if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0))
        {
            throw new ArgumentException(
                "The observed continuous value must be finite and non-negative.",
                nameof(value));
        }

        if (!double.IsFinite(pessimisticScheduledFailureValue) ||
            pessimisticScheduledFailureValue < 0)
        {
            throw new ArgumentException(
                "The pessimistic scheduled-failure value must be finite and non-negative.",
                nameof(pessimisticScheduledFailureValue));
        }

        Dimension = dimension;
        Value = value;
        PessimisticScheduledFailureValue = pessimisticScheduledFailureValue;
        IsComparable = isComparable;
    }

    /// <summary>Gets the continuous evaluation dimension.</summary>
    public HarnessEvaluationDimension Dimension { get; }

    /// <summary>Gets the finite observed value, or <see langword="null"/> when unscorable.</summary>
    public double? Value { get; }

    /// <summary>Gets the predeclared value used for scheduled failed or unscorable trials.</summary>
    public double PessimisticScheduledFailureValue { get; }

    /// <summary>Gets whether the dimension is comparable across arms for this trial.</summary>
    public bool IsComparable { get; }
}
