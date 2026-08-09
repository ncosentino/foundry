using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SpecialistManifestBarrierExecutor(
    ReferenceArtifactStore artifacts,
    string runId) :
    Executor<List<ReferencePhaseArtifact>, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "pre-synthesis-artifact-gate.v1";
    internal const string Phase = "specialist-manifest";

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        List<ReferencePhaseArtifact> message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ReferenceArtifactReference[] researchReferences = message
            .SelectMany(branch => branch.Inputs)
            .DistinctBy(reference => reference.Id)
            .ToArray();
        if (researchReferences.Length != 1)
        {
            throw new InvalidOperationException(
                "Specialist outcomes did not reference the research artifact.");
        }

        ReferenceArtifactReference research = researchReferences[0];
        ReferencePhaseArtifact[] branches = [.. message.OrderBy(
            branch => branch.Ordinal)];
        bool requiredFailure = branches.Any(
            branch =>
                branch.Required &&
                branch.Outcome != ReferencePipelineOutcome.Completed);
        bool optionalGap = branches.Any(
            branch =>
                !branch.Required &&
                branch.Outcome != ReferencePipelineOutcome.Completed);
        ReferencePipelineOutcome outcome = requiredFailure
            ? ReferencePipelineOutcome.Failed
            : optionalGap
                ? ReferencePipelineOutcome.Partial
                : ReferencePipelineOutcome.Completed;
        string[] gaps = branches
            .SelectMany(branch => branch.Gaps)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var manifest = new ReferenceArtifactManifest(
            runId,
            research,
            branches,
            outcome,
            gaps);
        ReferenceArtifactReference manifestReference =
            artifacts.WriteManifest(manifest);
        ReferenceArtifactReference[] inputs =
        [
            research,
            .. branches
                .Where(branch => branch.Artifact is not null)
                .Select(branch => branch.Artifact!),
        ];
        var candidate = ReferencePhaseArtifact.Candidate(
            Phase,
            ordinal: 100,
            required: true,
            content: "{}",
            inputs);
        return ValueTask.FromResult(
            ReferencePhaseArtifact.WithArtifact(
                candidate,
                manifestReference,
                outcome,
                gaps));
    }
}
