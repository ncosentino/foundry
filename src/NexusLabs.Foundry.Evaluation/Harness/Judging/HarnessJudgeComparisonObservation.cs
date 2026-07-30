namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Describes one deterministic-versus-judge pairwise observation retained for disagreement reporting.
/// </summary>
public sealed record HarnessJudgeComparisonObservation
{
    /// <summary>
    /// Initializes one judge comparison observation.
    /// </summary>
    /// <param name="caseId">The hosted case identifier.</param>
    /// <param name="dimension">The deterministic evaluation dimension.</param>
    /// <param name="xArm">Arm X in the X-minus-Y contrast.</param>
    /// <param name="yArm">Arm Y in the X-minus-Y contrast.</param>
    /// <param name="deterministicPreference">The preference implied by deterministic evidence.</param>
    /// <param name="judgePreference">The advisory judge preference.</param>
    /// <param name="isOrderConsistent">Whether both presentation orders produced equivalent preference.</param>
    /// <param name="calibrationState">The judge calibration state.</param>
    /// <exception cref="ArgumentException">The case ID is blank or the arms are equal.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is not defined.</exception>
    public HarnessJudgeComparisonObservation(
        string caseId,
        HarnessEvaluationDimension dimension,
        HarnessComparisonArm xArm,
        HarnessComparisonArm yArm,
        HarnessPairwisePreference deterministicPreference,
        HarnessPairwisePreference judgePreference,
        bool isOrderConsistent,
        HarnessJudgeCalibrationState calibrationState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ValidateEnum(dimension, nameof(dimension));
        ValidateEnum(xArm, nameof(xArm));
        ValidateEnum(yArm, nameof(yArm));
        ValidateEnum(deterministicPreference, nameof(deterministicPreference));
        ValidateEnum(judgePreference, nameof(judgePreference));
        ValidateEnum(calibrationState, nameof(calibrationState));
        if (xArm == yArm)
        {
            throw new ArgumentException("Judge comparison arms must be distinct.", nameof(yArm));
        }

        if (deterministicPreference == HarnessPairwisePreference.Abstain)
        {
            throw new ArgumentException(
                "Deterministic evidence cannot use the advisory Abstain preference.",
                nameof(deterministicPreference));
        }

        CaseId = caseId;
        Dimension = dimension;
        XArm = xArm;
        YArm = yArm;
        DeterministicPreference = deterministicPreference;
        JudgePreference = judgePreference;
        IsOrderConsistent = isOrderConsistent;
        CalibrationState = calibrationState;
    }

    /// <summary>Gets the hosted case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the deterministic evaluation dimension.</summary>
    public HarnessEvaluationDimension Dimension { get; }

    /// <summary>Gets arm X in the X-minus-Y contrast.</summary>
    public HarnessComparisonArm XArm { get; }

    /// <summary>Gets arm Y in the X-minus-Y contrast.</summary>
    public HarnessComparisonArm YArm { get; }

    /// <summary>Gets the preference implied by deterministic evidence.</summary>
    public HarnessPairwisePreference DeterministicPreference { get; }

    /// <summary>Gets the advisory judge preference.</summary>
    public HarnessPairwisePreference JudgePreference { get; }

    /// <summary>Gets whether both pair presentation orders were consistent.</summary>
    public bool IsOrderConsistent { get; }

    /// <summary>Gets the judge calibration state.</summary>
    public HarnessJudgeCalibrationState CalibrationState { get; }

    /// <summary>Gets whether the judge conflicts with deterministic evidence or presentation order.</summary>
    public bool IsDisagreement =>
        !IsOrderConsistent ||
        DeterministicPreference != JudgePreference;

    /// <summary>Gets a value indicating that deterministic evidence governs every conflict.</summary>
    public bool DeterministicGoverns => true;

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The enum value is not defined.");
        }
    }
}
