using AutonomousPhasePipelineApp.Evaluation;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedEvaluationStatusTests
{
    [Fact]
    public void CancellationNotObserved_IsFailed()
    {
        HostedEvaluationExecutionStatus status =
            HostedEvaluationArmDriver.ClassifyCancellation(
                new HostedEvaluationRunObservation(
                    Result: null,
                    BeforeSynthesis: null,
                    FailureCode: "cancellation-not-observed",
                    Canceled: false,
                    CheckpointEvents: 0,
                    ProviderCallLimit: 24,
                    ProviderCallsUsed: 1));

        Assert.Equal(
            HostedEvaluationExecutionStatus.Failed,
            status);
    }

    [Fact]
    public void ObservedCancellation_IsCanceled()
    {
        HostedEvaluationExecutionStatus status =
            HostedEvaluationArmDriver.ClassifyCancellation(
                new HostedEvaluationRunObservation(
                    Result: null,
                    BeforeSynthesis: null,
                    FailureCode: "caller-canceled",
                    Canceled: true,
                    CheckpointEvents: 0,
                    ProviderCallLimit: 24,
                    ProviderCallsUsed: 1));

        Assert.Equal(
            HostedEvaluationExecutionStatus.Canceled,
            status);
    }
}
