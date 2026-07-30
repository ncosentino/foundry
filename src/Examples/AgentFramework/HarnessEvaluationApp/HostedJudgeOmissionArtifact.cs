using NexusLabs.Foundry.Evaluation.Harness.Judging;

namespace HarnessEvaluationApp;

internal sealed record HostedJudgeOmissionArtifact(
    string SchemaVersion,
    HarnessJudgeCalibrationState CalibrationState,
    int EligibleCalibrationItemCount,
    int ProvisionalCalibrationItemCount,
    bool UsableForArmRanking,
    string Reason);
