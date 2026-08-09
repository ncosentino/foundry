using AutonomousPhasePipelineApp.Evaluation;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedJudgeCalibrationPolicyTests
{
    [Fact]
    public void PassingAttestation_IsAdmitted()
    {
        HostedJudgeCalibrationDecision decision =
            HostedJudgeCalibrationPolicy.Evaluate(
                CreateAttestation());

        Assert.True(decision.Admitted, decision.Reason);
        Assert.Equal(0.90, decision.Sensitivity);
        Assert.Equal(0.90, decision.Specificity);
    }

    [Fact]
    public void PositionBiasAboveThreshold_IsRejected()
    {
        HostedJudgeCalibrationAttestation attestation =
            CreateAttestation() with
            {
                PositionOrderFlipRate = 0.11,
            };

        HostedJudgeCalibrationDecision decision =
            HostedJudgeCalibrationPolicy.Evaluate(attestation);

        Assert.False(decision.Admitted);
    }

    [Fact]
    public void InvalidConfusionMatrix_IsRejected()
    {
        HostedJudgeCalibrationAttestation attestation =
            CreateAttestation() with
            {
                ValidRows = 99,
            };

        HostedJudgeCalibrationDecision decision =
            HostedJudgeCalibrationPolicy.Evaluate(attestation);

        Assert.False(decision.Admitted);
        Assert.Null(decision.Sensitivity);
        Assert.Null(decision.Specificity);
    }

    [Fact]
    public void NegativeCounts_AreRejected()
    {
        HostedJudgeCalibrationAttestation attestation =
            CreateAttestation() with
            {
                TruePositive = -1,
                FalseNegative = 101,
            };

        Assert.False(
            HostedJudgeCalibrationPolicy
                .Evaluate(attestation)
                .Admitted);
    }

    [Fact]
    public void NonFiniteMetrics_AreRejected()
    {
        HostedJudgeCalibrationAttestation attestation =
            CreateAttestation() with
            {
                Kappa = double.NaN,
            };

        Assert.False(
            HostedJudgeCalibrationPolicy
                .Evaluate(attestation)
                .Admitted);
    }

    private static HostedJudgeCalibrationAttestation
        CreateAttestation() =>
        new(
            SchemaVersion: 1,
            JudgeId: "judge-v1",
            PromptVersion: "prompt-v1",
            CorpusVersion: "corpus-v1",
            SplitHash: "split-hash",
            ValidRows: 200,
            TruePositive: 90,
            FalsePositive: 10,
            FalseNegative: 10,
            TrueNegative: 90,
            Kappa: 0.80,
            OrdinalCorrelation: 0.85,
            PositionOrderFlipRate: 0.05);
}
