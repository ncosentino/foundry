namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Provides deterministic-versus-judge disagreement counts and the underlying observations.
/// </summary>
public sealed record HarnessJudgeDisagreementReport
{
    private HarnessJudgeDisagreementReport(
        IReadOnlyList<HarnessJudgeComparisonObservation> observations)
    {
        Observations = observations;
        TotalObservationCount = observations.Count;
        DisagreementCount = observations.Count(observation => observation.IsDisagreement);
        UsableForArmRanking =
            observations.Count > 0 &&
            observations.All(observation =>
                observation.IsOrderConsistent &&
                observation.JudgePreference != HarnessPairwisePreference.Abstain &&
                observation.CalibrationState == HarnessJudgeCalibrationState.Calibrated);
    }

    /// <summary>Gets a defensive snapshot of judge comparison observations.</summary>
    public IReadOnlyList<HarnessJudgeComparisonObservation> Observations { get; }

    /// <summary>Gets the number of retained judge observations.</summary>
    public int TotalObservationCount { get; }

    /// <summary>Gets the number of deterministic or presentation-order disagreements.</summary>
    public int DisagreementCount { get; }

    /// <summary>Gets whether the observations may rank arms advisorially.</summary>
    public bool UsableForArmRanking { get; }

    internal static HarnessJudgeDisagreementReport Create(
        IReadOnlyList<HarnessJudgeComparisonObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var snapshot = new HarnessJudgeComparisonObservation[observations.Count];
        for (var index = 0; index < observations.Count; index++)
        {
            var observation = observations[index];
            ArgumentNullException.ThrowIfNull(observation);
            snapshot[index] = new HarnessJudgeComparisonObservation(
                observation.CaseId,
                observation.Dimension,
                observation.XArm,
                observation.YArm,
                observation.DeterministicPreference,
                observation.JudgePreference,
                observation.IsOrderConsistent,
                observation.CalibrationState);
        }

        return new HarnessJudgeDisagreementReport(Array.AsReadOnly(snapshot));
    }
}
