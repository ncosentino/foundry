using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class SpecialistManifestBarrierExecutor(
    ReferenceArtifactStore artifacts,
    string runId,
    IReadOnlyList<ReferenceBranchDefinition> expectedBranches) :
    Executor<List<ReferencePhaseArtifact>, ReferencePhaseArtifact>(ExecutorId)
{
    internal const string ExecutorId = "pre-synthesis-artifact-gate.v1";
    internal const string Phase = "specialist-manifest";

    public override ValueTask<ReferencePhaseArtifact> HandleAsync(
        List<ReferencePhaseArtifact> message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ReferencePhaseArtifact[] branches = ValidateAndOrderBranches(message);
        ReferenceArtifactReference research = GetResearchReference(branches);
        _ = artifacts.Read(runId, research.Id);

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

    private ReferencePhaseArtifact[] ValidateAndOrderBranches(
        IReadOnlyCollection<ReferencePhaseArtifact> message)
    {
        if (message.Count != expectedBranches.Count)
        {
            throw new InvalidOperationException(
                "Specialist outcomes did not match the expected branch count.");
        }

        var ordered = new List<ReferencePhaseArtifact>(
            expectedBranches.Count);
        foreach (ReferenceBranchDefinition expected in expectedBranches)
        {
            ReferencePhaseArtifact[] matches = message
                .Where(branch => string.Equals(
                    branch.Phase,
                    expected.Phase,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Specialist branch '{expected.Phase}' did not have exactly one outcome.");
            }

            ReferencePhaseArtifact branch = matches[0];
            if (branch.Ordinal != expected.Ordinal ||
                branch.Required != expected.Required)
            {
                throw new InvalidOperationException(
                    $"Specialist branch '{expected.Phase}' metadata did not match its definition.");
            }

            ValidateBranch(branch);
            ordered.Add(branch);
        }

        ordered.Sort(
            static (left, right) =>
                left.Ordinal.CompareTo(right.Ordinal));
        return [.. ordered];
    }

    private void ValidateBranch(
        ReferencePhaseArtifact branch)
    {
        if (branch.Inputs.Length != 1)
        {
            throw new InvalidOperationException(
                $"Specialist branch '{branch.Phase}' did not reference exactly one research artifact.");
        }

        if (branch.CandidateContent is not null)
        {
            throw new InvalidOperationException(
                $"Specialist branch '{branch.Phase}' crossed the manifest boundary with candidate content.");
        }

        if (branch.Gaps.Length != branch.Gaps
            .Distinct(StringComparer.Ordinal)
            .Count())
        {
            throw new InvalidOperationException(
                $"Specialist branch '{branch.Phase}' contained duplicate gaps.");
        }

        switch (branch.Outcome)
        {
            case ReferencePipelineOutcome.Completed:
                if (branch.Artifact is null ||
                    branch.Gaps.Length != 0 ||
                    branch.Error is not null)
                {
                    throw new InvalidOperationException(
                        $"Completed specialist branch '{branch.Phase}' had inconsistent artifact metadata.");
                }

                _ = artifacts.Read(runId, branch.Artifact.Id);
                break;
            case ReferencePipelineOutcome.Partial:
                if (branch.Artifact is null ||
                    branch.Gaps.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Partial specialist branch '{branch.Phase}' had inconsistent artifact metadata.");
                }

                _ = artifacts.Read(runId, branch.Artifact.Id);
                break;
            case ReferencePipelineOutcome.Failed:
            case ReferencePipelineOutcome.Skipped:
                if (branch.Artifact is not null ||
                    branch.Gaps.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Incomplete specialist branch '{branch.Phase}' carried an artifact or omitted its gap.");
                }

                break;
            default:
                throw new InvalidOperationException(
                    $"Specialist branch '{branch.Phase}' had an unknown outcome.");
        }
    }

    private static ReferenceArtifactReference GetResearchReference(
        IReadOnlyCollection<ReferencePhaseArtifact> branches)
    {
        ReferenceArtifactReference[] researchReferences = branches
            .Select(branch => branch.Inputs[0])
            .DistinctBy(reference => reference.Id)
            .ToArray();
        if (researchReferences.Length != 1)
        {
            throw new InvalidOperationException(
                "Specialist outcomes did not reference one shared research artifact.");
        }

        return researchReferences[0];
    }
}
