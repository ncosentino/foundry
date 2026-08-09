namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedJudgeCalibrationPolicy
{
    internal static HostedJudgeCalibrationDecision Evaluate(
        HostedJudgeCalibrationAttestation attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        bool countsValid =
            attestation.ValidRows >= 200 &&
            attestation.TruePositive >= 0 &&
            attestation.FalsePositive >= 0 &&
            attestation.FalseNegative >= 0 &&
            attestation.TrueNegative >= 0;
        int matrixTotal =
            attestation.TruePositive +
            attestation.FalsePositive +
            attestation.FalseNegative +
            attestation.TrueNegative;
        bool metricsValid =
            double.IsFinite(attestation.Kappa) &&
            attestation.Kappa is >= -1 and <= 1 &&
            attestation.OrdinalCorrelation is { } correlation &&
            double.IsFinite(correlation) &&
            correlation is >= -1 and <= 1 &&
            double.IsFinite(attestation.PositionOrderFlipRate) &&
            attestation.PositionOrderFlipRate is >= 0 and <= 1;
        if (!countsValid ||
            !metricsValid ||
            matrixTotal != attestation.ValidRows)
        {
            return new(
                Admitted: false,
                "Calibration counts or metric domains are invalid, the corpus has fewer than 200 valid rows, or the confusion matrix does not match.",
                Sensitivity: null,
                Specificity: null);
        }

        double? sensitivity = Divide(
            attestation.TruePositive,
            attestation.TruePositive +
                attestation.FalseNegative);
        double? specificity = Divide(
            attestation.TrueNegative,
            attestation.TrueNegative +
                attestation.FalsePositive);
        bool admitted =
            attestation.SchemaVersion == 1 &&
            !string.IsNullOrWhiteSpace(attestation.JudgeId) &&
            !string.IsNullOrWhiteSpace(attestation.PromptVersion) &&
            !string.IsNullOrWhiteSpace(attestation.CorpusVersion) &&
            !string.IsNullOrWhiteSpace(attestation.SplitHash) &&
            attestation.Kappa >= 0.60 &&
            attestation.OrdinalCorrelation is >= 0.80 &&
            sensitivity is >= 0.75 &&
            specificity is >= 0.75 &&
            attestation.PositionOrderFlipRate <= 0.10;
        return new(
            admitted,
            admitted
                ? "The judge calibration met every preregistered admission threshold."
                : "The judge calibration did not meet every preregistered admission threshold.",
            sensitivity,
            specificity);
    }

    private static double? Divide(
        int numerator,
        int denominator) =>
        denominator == 0
            ? null
            : (double)numerator / denominator;
}
