namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Describes one paired case measurement for a continuous X-minus-Y comparison.
/// </summary>
public sealed record ExperimentPairedContinuousCaseMeasurement
{
    /// <summary>
    /// Initializes one paired continuous case measurement.
    /// </summary>
    /// <param name="caseId">The stable case identifier.</param>
    /// <param name="xValue">
    /// The case-level continuous value for arm X when <paramref name="xStatus"/> is
    /// <see cref="ExperimentItemStatus.Succeeded"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="xStatus">The case-level status for arm X.</param>
    /// <param name="yValue">
    /// The case-level continuous value for arm Y when <paramref name="yStatus"/> is
    /// <see cref="ExperimentItemStatus.Succeeded"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="yStatus">The case-level status for arm Y.</param>
    /// <param name="isComparable">
    /// <see langword="true"/> when the paired metric is comparable across both arms; otherwise
    /// <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="caseId"/> is blank, a succeeded arm omits its continuous value, or a
    /// non-succeeded arm supplies a continuous value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="xStatus"/> or <paramref name="yStatus"/> is not defined.
    /// </exception>
    public ExperimentPairedContinuousCaseMeasurement(
        string caseId,
        double? xValue,
        ExperimentItemStatus xStatus,
        double? yValue,
        ExperimentItemStatus yStatus,
        bool isComparable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ValidateStatus(xStatus, nameof(xStatus));
        ValidateStatus(yStatus, nameof(yStatus));
        ValidateValue(xValue, xStatus, nameof(xValue));
        ValidateValue(yValue, yStatus, nameof(yValue));
        CaseId = caseId;
        XValue = xValue;
        XStatus = xStatus;
        YValue = yValue;
        YStatus = yStatus;
        IsComparable = isComparable;
    }

    /// <summary>Gets the stable case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the case-level continuous value for arm X when available.</summary>
    public double? XValue { get; }

    /// <summary>Gets the case-level status for arm X.</summary>
    public ExperimentItemStatus XStatus { get; }

    /// <summary>Gets the case-level continuous value for arm Y when available.</summary>
    public double? YValue { get; }

    /// <summary>Gets the case-level status for arm Y.</summary>
    public ExperimentItemStatus YStatus { get; }

    /// <summary>Gets whether the paired metric is comparable across both arms.</summary>
    public bool IsComparable { get; }

    private static void ValidateStatus(ExperimentItemStatus status, string parameterName)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                status,
                "The experiment item status is not defined.");
        }
    }

    private static void ValidateValue(
        double? value,
        ExperimentItemStatus status,
        string valueParameterName)
    {
        if (status == ExperimentItemStatus.Succeeded)
        {
            if (!value.HasValue)
            {
                throw new ArgumentException(
                    "A succeeded arm must provide a continuous value.",
                    valueParameterName);
            }
        }
        else if (value.HasValue)
        {
            throw new ArgumentException(
                "Only succeeded arms may provide a continuous value.",
                valueParameterName);
        }
    }
}
