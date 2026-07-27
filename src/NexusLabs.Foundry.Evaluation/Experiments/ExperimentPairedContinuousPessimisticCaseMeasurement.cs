namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Describes one case-level paired continuous measurement for the required pessimistic sensitivity.
/// Values for fully scheduled, comparable cases are the arm's observed case-level value after replacing
/// each scheduled failed or unscorable trial with the metric's predeclared pessimistic bound. Cases that
/// were not fully scheduled remain explicit and must not use substitution.
/// </summary>
public sealed record ExperimentPairedContinuousPessimisticCaseMeasurement
{
    /// <summary>
    /// Initializes one pessimistic paired continuous case measurement.
    /// </summary>
    /// <param name="caseId">The stable case identifier.</param>
    /// <param name="xValue">The finite case-level sensitivity value for arm X when available.</param>
    /// <param name="xUsedSubstitution">
    /// <see langword="true"/> when at least one scheduled X trial used its predeclared pessimistic bound.
    /// </param>
    /// <param name="yValue">The finite case-level sensitivity value for arm Y when available.</param>
    /// <param name="yUsedSubstitution">
    /// <see langword="true"/> when at least one scheduled Y trial used its predeclared pessimistic bound.
    /// </param>
    /// <param name="isFullyScheduled">
    /// <see langword="true"/> when all planned trial slots were scheduled for both arms.
    /// </param>
    /// <param name="isComparable">
    /// <see langword="true"/> when the metric is comparable across both arms.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="caseId"/> is blank; a supplied value is non-finite; a fully scheduled,
    /// comparable case omits an arm value; a substitution omits its value; or an unscheduled case
    /// claims a substitution.
    /// </exception>
    public ExperimentPairedContinuousPessimisticCaseMeasurement(
        string caseId,
        double? xValue,
        bool xUsedSubstitution,
        double? yValue,
        bool yUsedSubstitution,
        bool isFullyScheduled,
        bool isComparable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ValidateValue(xValue, nameof(xValue));
        ValidateValue(yValue, nameof(yValue));

        if (!isFullyScheduled && (xUsedSubstitution || yUsedSubstitution))
        {
            throw new ArgumentException(
                "A case that was not fully scheduled cannot use pessimistic substitution.",
                nameof(isFullyScheduled));
        }

        if (xUsedSubstitution && !xValue.HasValue)
        {
            throw new ArgumentException(
                "Arm X must provide its substituted sensitivity value.",
                nameof(xValue));
        }

        if (yUsedSubstitution && !yValue.HasValue)
        {
            throw new ArgumentException(
                "Arm Y must provide its substituted sensitivity value.",
                nameof(yValue));
        }

        if (isFullyScheduled && isComparable && (!xValue.HasValue || !yValue.HasValue))
        {
            throw new ArgumentException(
                "A fully scheduled, comparable sensitivity case must provide finite values for both arms.",
                nameof(isComparable));
        }

        CaseId = caseId;
        XValue = xValue;
        XUsedSubstitution = xUsedSubstitution;
        YValue = yValue;
        YUsedSubstitution = yUsedSubstitution;
        IsFullyScheduled = isFullyScheduled;
        IsComparable = isComparable;
    }

    /// <summary>Gets the stable case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the finite case-level sensitivity value for arm X when available.</summary>
    public double? XValue { get; }

    /// <summary>Gets whether arm X used at least one predeclared pessimistic substitution.</summary>
    public bool XUsedSubstitution { get; }

    /// <summary>Gets the finite case-level sensitivity value for arm Y when available.</summary>
    public double? YValue { get; }

    /// <summary>Gets whether arm Y used at least one predeclared pessimistic substitution.</summary>
    public bool YUsedSubstitution { get; }

    /// <summary>Gets whether all planned trial slots were scheduled for both arms.</summary>
    public bool IsFullyScheduled { get; }

    /// <summary>Gets whether the metric is comparable across both arms.</summary>
    public bool IsComparable { get; }

    private static void ValidateValue(double? value, string parameterName)
    {
        if (value.HasValue && !double.IsFinite(value.Value))
        {
            throw new ArgumentException(
                "A supplied pessimistic sensitivity value must be finite.",
                parameterName);
        }
    }
}
