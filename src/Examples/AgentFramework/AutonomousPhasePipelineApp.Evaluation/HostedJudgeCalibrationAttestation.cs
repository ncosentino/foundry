namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedJudgeCalibrationAttestation(
    int SchemaVersion,
    string JudgeId,
    string PromptVersion,
    string CorpusVersion,
    string SplitHash,
    int ValidRows,
    int TruePositive,
    int FalsePositive,
    int FalseNegative,
    int TrueNegative,
    double Kappa,
    double? OrdinalCorrelation,
    double PositionOrderFlipRate);
