namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Provides the validated judge rubrics and calibration governance loaded for one case-set version.
/// </summary>
public sealed record HarnessJudgeAssets
{
    internal HarnessJudgeAssets(
        HarnessJudgeRubric nominalRubric,
        HarnessJudgeRubric ordinalRubric,
        HarnessJudgeCalibrationState calibrationState,
        double minimumKappa,
        double? observedKappa,
        int eligibleCalibrationItemCount,
        int provisionalCalibrationItemCount)
    {
        NominalRubric = nominalRubric;
        OrdinalRubric = ordinalRubric;
        CalibrationState = calibrationState;
        MinimumKappa = minimumKappa;
        ObservedKappa = observedKappa;
        EligibleCalibrationItemCount = eligibleCalibrationItemCount;
        ProvisionalCalibrationItemCount = provisionalCalibrationItemCount;
    }

    /// <summary>Gets the nominal pairwise rubric.</summary>
    public HarnessJudgeRubric NominalRubric { get; }

    /// <summary>Gets the ordinal response-quality rubric.</summary>
    public HarnessJudgeRubric OrdinalRubric { get; }

    /// <summary>Gets the calibration state.</summary>
    public HarnessJudgeCalibrationState CalibrationState { get; }

    /// <summary>Gets the minimum acceptable Cohen kappa.</summary>
    public double MinimumKappa { get; }

    /// <summary>Gets the observed calibration kappa, or <see langword="null"/> when unavailable.</summary>
    public double? ObservedKappa { get; }

    /// <summary>Gets the number of human-attested calibration items eligible for agreement statistics.</summary>
    public int EligibleCalibrationItemCount { get; }

    /// <summary>Gets the number of provisional calibration items excluded from agreement statistics.</summary>
    public int ProvisionalCalibrationItemCount { get; }

    /// <summary>Gets whether judge evidence may rank arms advisorially.</summary>
    public bool UsableForArmRanking => CalibrationState == HarnessJudgeCalibrationState.Calibrated;
}
