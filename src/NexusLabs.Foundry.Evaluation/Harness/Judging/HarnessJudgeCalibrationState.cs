namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Identifies whether judge evidence has met the frozen human-calibration requirements.
/// </summary>
public enum HarnessJudgeCalibrationState
{
    /// <summary>The judge evidence is advisory and cannot rank arms.</summary>
    Uncalibrated,

    /// <summary>The judge evidence has eligible human labels and sufficient observed agreement.</summary>
    Calibrated,
}
