namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedJudgeCalibrationDecision(
    bool Admitted,
    string Reason,
    double? Sensitivity,
    double? Specificity);
