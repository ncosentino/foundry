namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Describes one paired case outcome for a binary X-minus-Y comparison.
/// </summary>
public sealed record ExperimentPairedBinaryCaseOutcome
{
    /// <summary>
    /// Initializes one paired binary case outcome.
    /// </summary>
    /// <param name="caseId">The stable case identifier.</param>
    /// <param name="xOutcome">
    /// The observed binary outcome for arm X when <paramref name="xStatus"/> is
    /// <see cref="ExperimentItemStatus.Succeeded"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="xStatus">The case-level status for arm X.</param>
    /// <param name="yOutcome">
    /// The observed binary outcome for arm Y when <paramref name="yStatus"/> is
    /// <see cref="ExperimentItemStatus.Succeeded"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="yStatus">The case-level status for arm Y.</param>
    /// <param name="isComparable">
    /// <see langword="true"/> when the paired metric is comparable across both arms; otherwise
    /// <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="caseId"/> is blank, a succeeded arm omits its observed outcome, or a
    /// non-succeeded arm supplies an observed outcome.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="xStatus"/> or <paramref name="yStatus"/> is not defined.
    /// </exception>
    public ExperimentPairedBinaryCaseOutcome(
        string caseId,
        bool? xOutcome,
        ExperimentItemStatus xStatus,
        bool? yOutcome,
        ExperimentItemStatus yStatus,
        bool isComparable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ValidateStatus(xStatus, nameof(xStatus));
        ValidateStatus(yStatus, nameof(yStatus));
        ValidateOutcome(xOutcome, xStatus, nameof(xOutcome));
        ValidateOutcome(yOutcome, yStatus, nameof(yOutcome));
        CaseId = caseId;
        XOutcome = xOutcome;
        XStatus = xStatus;
        YOutcome = yOutcome;
        YStatus = yStatus;
        IsComparable = isComparable;
    }

    /// <summary>Gets the stable case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the observed binary outcome for arm X when available.</summary>
    public bool? XOutcome { get; }

    /// <summary>Gets the case-level status for arm X.</summary>
    public ExperimentItemStatus XStatus { get; }

    /// <summary>Gets the observed binary outcome for arm Y when available.</summary>
    public bool? YOutcome { get; }

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

    private static void ValidateOutcome(
        bool? outcome,
        ExperimentItemStatus status,
        string outcomeParameterName)
    {
        if (status == ExperimentItemStatus.Succeeded)
        {
            if (!outcome.HasValue)
            {
                throw new ArgumentException(
                    "A succeeded arm must provide an observed binary outcome.",
                    outcomeParameterName);
            }
        }
        else if (outcome.HasValue)
        {
            throw new ArgumentException(
                "Only succeeded arms may provide an observed binary outcome.",
                outcomeParameterName);
        }
    }
}
