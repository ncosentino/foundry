using AutonomousPhasePipelineApp.Core;
using AutonomousPhasePipelineApp.Evaluation;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedDeterministicScorerTests
{
    [Fact]
    public void SynthesisNormalizer_AcceptsFencedJsonAndStoresCanonicalObject()
    {
        bool valid = ReferenceArtifactValidator.TryNormalizeSynthesis(
            $$"""
            ```json
            {{ReferenceSynthesisArtifacts.Valid}}
            ```
            """,
            out string normalized,
            out string error);

        Assert.True(valid, error);
        Assert.StartsWith("{", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("```", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalFailure_RequiresExplicitGapAndAuthorizedEvidence()
    {
        string runId = "optional";
        var artifacts = new ReferenceArtifactStore();
        var delivery = new IdempotentDeliverySink();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                runId,
                HostedEvaluationScenario.OptionalBranchFailure);
        ReferenceArtifactReference synthesis = artifacts.Write(
            runId,
            ReferenceSynthesisArtifacts.Valid);
        var synthesisOutcome = ReferencePhaseArtifact.WithArtifact(
            ReferencePhaseArtifact.Candidate(
                SynthesisPhaseExecutor.Phase,
                200,
                true,
                "{}",
                fixture.Manifest.Artifact!),
            synthesis);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            runId);
        ReferencePipelineResult result = delivery.Publish(
            new ReferencePipelineResult(
                runId,
                ReferencePipelineOutcome.Partial,
                $"delivery/{runId}",
                fixture.Manifest.Artifact!,
                synthesisOutcome,
                manifest.Branches,
                manifest.Gaps));

        var scores = HostedDeterministicScorer.Score(
            artifacts,
            runId,
            HostedEvaluationScenario.OptionalBranchFailure,
            fixture,
            result);

        Assert.True(scores.SchemaValid);
        Assert.True(scores.RequiredCoverage);
        Assert.True(scores.GapCorrect);
        Assert.True(scores.SiblingPreserved);
        Assert.True(scores.EvidenceValid);
    }

    [Fact]
    public void RequiredFailure_TreatsSkippedSynthesisAsSchemaSafe()
    {
        string runId = "required";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                runId,
                HostedEvaluationScenario.RequiredBranchFailure);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            runId);
        ReferencePhaseArtifact synthesis =
            ReferencePhaseArtifact.Skipped(
                SynthesisPhaseExecutor.Phase,
                200,
                true,
                "blocked",
                fixture.Manifest.Artifact!);
        var result = new ReferencePipelineResult(
            runId,
            ReferencePipelineOutcome.Failed,
            $"delivery/{runId}",
            fixture.Manifest.Artifact!,
            synthesis,
            manifest.Branches,
            manifest.Gaps);

        var scores = HostedDeterministicScorer.Score(
            artifacts,
            runId,
            HostedEvaluationScenario.RequiredBranchFailure,
            fixture,
            result);

        Assert.True(scores.SchemaValid);
        Assert.False(scores.RequiredCoverage);
        Assert.True(scores.GapCorrect);
        Assert.True(scores.SiblingPreserved);
        Assert.True(scores.EvidenceValid);
    }
}
