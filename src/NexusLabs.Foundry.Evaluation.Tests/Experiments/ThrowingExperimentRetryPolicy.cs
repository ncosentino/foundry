using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Tests.Experiments;

internal sealed class ThrowingExperimentRetryPolicy : IExperimentRetryPolicy
{
    public int MaxAttempts => 2;

    public ExperimentRetryDecision Decide(ExperimentRetryContext context) =>
        throw new InvalidOperationException("retry policy failed");
}
