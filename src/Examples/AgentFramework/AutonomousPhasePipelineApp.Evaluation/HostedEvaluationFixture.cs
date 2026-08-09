using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationFixture
{
    internal static HostedEvaluationFixtureData Create(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationScenario scenario)
    {
        ReferenceArtifactReference research = artifacts.Write(
            runId,
            """
            {
              "topic": "Synthetic release readiness",
              "evidence": ["research"]
            }
            """);
        ReferencePhaseArtifact risk = CreateBranch(
            artifacts,
            runId,
            research,
            phase: "risk",
            ordinal: 0,
            required: true,
            fail: scenario == HostedEvaluationScenario.RequiredBranchFailure,
            evidenceId: "required-specialist");
        ReferencePhaseArtifact operations = CreateBranch(
            artifacts,
            runId,
            research,
            phase: "operations",
            ordinal: 1,
            required: false,
            fail: scenario == HostedEvaluationScenario.OptionalBranchFailure,
            evidenceId: "optional-specialist");
        ReferencePhaseArtifact[] branches = [risk, operations];
        bool requiredFailure = risk.Outcome == ReferencePipelineOutcome.Failed;
        bool optionalFailure =
            operations.Outcome == ReferencePipelineOutcome.Failed;
        ReferencePipelineOutcome outcome = requiredFailure
            ? ReferencePipelineOutcome.Failed
            : optionalFailure
                ? ReferencePipelineOutcome.Partial
                : ReferencePipelineOutcome.Completed;
        string[] gaps = branches
            .SelectMany(branch => branch.Gaps)
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
            SpecialistManifestBarrierExecutor.Phase,
            ordinal: 100,
            required: true,
            content: "{}",
            inputs);
        ReferencePhaseArtifact manifestArtifact =
            ReferencePhaseArtifact.WithArtifact(
                candidate,
                manifestReference,
                outcome,
                gaps);
        string[] evidenceIds = branches
            .Where(branch => branch.Outcome == ReferencePipelineOutcome.Completed)
            .Select(branch =>
                branch.Phase == "risk"
                    ? "required-specialist"
                    : "optional-specialist")
            .Prepend("research")
            .ToArray();
        return new HostedEvaluationFixtureData(
            manifestArtifact,
            evidenceIds,
            gaps);
    }

    private static ReferencePhaseArtifact CreateBranch(
        ReferenceArtifactStore artifacts,
        string runId,
        ReferenceArtifactReference research,
        string phase,
        int ordinal,
        bool required,
        bool fail,
        string evidenceId)
    {
        if (fail)
        {
            return ReferencePhaseArtifact.Failed(
                phase,
                ordinal,
                required,
                "injected-branch-failure",
                [research]);
        }

        ReferenceArtifactReference artifact = artifacts.Write(
            runId,
            $$"""
            {
              "summary": "{{phase}} accepted",
              "evidence": ["{{evidenceId}}"]
            }
            """);
        return ReferencePhaseArtifact.WithArtifact(
            ReferencePhaseArtifact.Candidate(
                phase,
                ordinal,
                required,
                "{}",
                research),
            artifact);
    }
}
