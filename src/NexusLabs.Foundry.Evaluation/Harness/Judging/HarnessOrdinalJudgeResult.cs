namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Provides one structured advisory ordinal judge result with complete rubric and calibration
/// attribution.
/// </summary>
public sealed record HarnessOrdinalJudgeResult
{
    internal HarnessOrdinalJudgeResult(
        int score,
        string reason,
        string judgeModelId,
        string judgeModelFamily,
        bool usesDifferentModelFamily,
        HarnessJudgeCalibrationState calibrationState,
        string rubricId,
        string rubricVersion,
        string rubricSha256)
    {
        Score = score;
        Reason = reason;
        JudgeModelId = judgeModelId;
        JudgeModelFamily = judgeModelFamily;
        UsesDifferentModelFamily = usesDifferentModelFamily;
        CalibrationState = calibrationState;
        RubricId = rubricId;
        RubricVersion = rubricVersion;
        RubricSha256 = rubricSha256;
    }

    /// <summary>Gets the ordinal response-quality score.</summary>
    public int Score { get; }

    /// <summary>Gets the short evidence-grounded reason.</summary>
    public string Reason { get; }

    /// <summary>Gets the judge model identifier.</summary>
    public string JudgeModelId { get; }

    /// <summary>Gets the judge model family.</summary>
    public string JudgeModelFamily { get; }

    /// <summary>Gets whether the judge and generator use different model families.</summary>
    public bool UsesDifferentModelFamily { get; }

    /// <summary>Gets the calibration state.</summary>
    public HarnessJudgeCalibrationState CalibrationState { get; }

    /// <summary>Gets the rubric identifier.</summary>
    public string RubricId { get; }

    /// <summary>Gets the rubric version.</summary>
    public string RubricVersion { get; }

    /// <summary>Gets the rubric SHA-256 digest.</summary>
    public string RubricSha256 { get; }

    /// <summary>Gets whether this result may rank arms advisorially.</summary>
    public bool UsableForArmRanking => CalibrationState == HarnessJudgeCalibrationState.Calibrated;
}
