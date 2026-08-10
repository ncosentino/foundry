using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

[SendsMessage(typeof(List<ReferencePhaseArtifact>))]
internal sealed class AllSettledJoinExecutor(
    IReadOnlyList<ReferenceBranchDefinition> expectedBranches) :
    Executor<ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "all-settled-specialist-join.v1";

    private readonly List<ReferencePhaseArtifact> _batch = [];

    protected override ValueTask OnMessageDeliveryStartingAsync(
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _batch.Clear();
        return ValueTask.CompletedTask;
    }

    public override ValueTask HandleAsync(
        ReferencePhaseArtifact message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        _batch.Add(message);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnMessageDeliveryFinishedAsync(
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ReferencePhaseArtifact? unexpected = _batch.FirstOrDefault(
            outcome => !expectedBranches.Any(
                branch => string.Equals(
                    branch.Phase,
                    outcome.Phase,
                    StringComparison.Ordinal)));
        if (unexpected is not null)
        {
            throw new InvalidOperationException(
                $"Unexpected specialist outcome '{unexpected.Phase}'.");
        }

        ReferenceArtifactReference[] sharedInputs = _batch
            .SelectMany(outcome => outcome.Inputs)
            .DistinctBy(reference => reference.Id)
            .ToArray();
        var settled = new List<ReferencePhaseArtifact>(
            expectedBranches.Count);

        foreach (ReferenceBranchDefinition branch in expectedBranches)
        {
            ReferencePhaseArtifact[] matches = _batch
                .Where(outcome => string.Equals(
                    outcome.Phase,
                    branch.Phase,
                    StringComparison.Ordinal))
                .ToArray();
            settled.Add(
                matches.Length switch
                {
                    1 => matches[0],
                    0 => ReferencePhaseArtifact.Failed(
                        branch.Phase,
                        branch.Ordinal,
                        branch.Required,
                        "missing-outcome",
                        sharedInputs),
                    _ => ReferencePhaseArtifact.Failed(
                        branch.Phase,
                        branch.Ordinal,
                        branch.Required,
                        "duplicate-outcome",
                        sharedInputs),
                });
        }

        settled.Sort(
            static (left, right) =>
                left.Ordinal.CompareTo(right.Ordinal));
        return context.SendMessageAsync(
            settled,
            cancellationToken: cancellationToken);
    }
}
