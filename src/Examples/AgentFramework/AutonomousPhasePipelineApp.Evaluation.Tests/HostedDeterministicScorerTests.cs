using System.Text.Json;

using AutonomousPhasePipelineApp.Core;
using AutonomousPhasePipelineApp.Evaluation;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedDeterministicScorerTests
{
    [Fact]
    public void SynthesisNormalizer_AcceptsFencedManifestGroundedJson()
    {
        const string RunId = "normalizer";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.Success);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            RunId);
        string valid = ReferenceSynthesisArtifacts.Create(
            manifest,
            includeRecommendation: true);

        bool accepted = ReferenceArtifactValidator.TryNormalizeSynthesis(
            $"```json\n{valid}\n```",
            manifest,
            out string normalized,
            out string error);

        Assert.True(accepted, error);
        Assert.StartsWith("{", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("```", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalFailure_RequiresExactManifestContract()
    {
        const string RunId = "optional";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.OptionalBranchFailure);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            RunId);
        ReferencePipelineResult result = CreateResult(
            artifacts,
            RunId,
            fixture,
            ReferenceSynthesisArtifacts.Create(
                manifest,
                includeRecommendation: true));

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            authoritativeDeliveries: 1);

        Assert.True(correctness.ArtifactContractValid);
        Assert.True(correctness.EvidenceExactSet);
        Assert.True(correctness.GapsExactSet);
        Assert.True(correctness.RequiredCoverageMatches);
        Assert.True(correctness.SuccessfulSiblingsPreserved);
        Assert.True(correctness.PipelineOutcomeMatches);
        Assert.True(correctness.SynthesisOutcomeMatches);
        Assert.True(correctness.AuthoritativeDeliveryExact);
        Assert.True(correctness.TaskContractPass);
    }

    [Fact]
    public void EvidenceSubset_IsRejected()
    {
        const string RunId = "subset";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.Success);
        string content = CreateArtifact(
            [fixture.Expected.EvidenceIds[0]],
            fixture.Expected.Gaps);
        ReferencePipelineResult result = CreateResult(
            artifacts,
            RunId,
            fixture,
            content);

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            authoritativeDeliveries: 1);

        Assert.False(correctness.ArtifactContractValid);
        Assert.False(correctness.EvidenceExactSet);
        Assert.False(correctness.TaskContractPass);
        Assert.StartsWith(
            "missing-required-evidence:",
            correctness.ValidationErrorCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceSuperset_IsRejected()
    {
        const string RunId = "superset";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.Success);
        string content = CreateArtifact(
            [
                .. fixture.Expected.EvidenceIds,
                "artifact://sha256/unexpected",
            ],
            fixture.Expected.Gaps);
        ReferencePipelineResult result = CreateResult(
            artifacts,
            RunId,
            fixture,
            content);

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            authoritativeDeliveries: 1);

        Assert.False(correctness.ArtifactContractValid);
        Assert.False(correctness.EvidenceExactSet);
        Assert.False(correctness.TaskContractPass);
        Assert.Equal(
            "unexpected-evidence:artifact://sha256/unexpected",
            correctness.ValidationErrorCode);
    }

    [Fact]
    public void GapMismatch_IsRejected()
    {
        const string RunId = "gap-mismatch";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.OptionalBranchFailure);
        string content = CreateArtifact(
            fixture.Expected.EvidenceIds,
            []);
        ReferencePipelineResult result = CreateResult(
            artifacts,
            RunId,
            fixture,
            content);

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            authoritativeDeliveries: 1);

        Assert.False(correctness.ArtifactContractValid);
        Assert.False(correctness.GapsExactSet);
        Assert.False(correctness.TaskContractPass);
        Assert.StartsWith(
            "missing-gap:",
            correctness.ValidationErrorCode,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SubstitutedSiblingArtifact_IsRejected()
    {
        const string RunId = "substituted-branch";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.Success);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            RunId);
        ReferencePipelineResult result = CreateResult(
            artifacts,
            RunId,
            fixture,
            ReferenceSynthesisArtifacts.Create(
                manifest,
                includeRecommendation: true));
        ReferenceArtifactReference substituted = artifacts.Write(
            RunId,
            """{"summary":"substituted","evidence":["source"]}""");
        ReferencePhaseArtifact[] branches = [.. result.Branches];
        branches[0] = branches[0] with
        {
            Artifact = substituted,
        };
        result = result with
        {
            Branches = branches,
        };

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            authoritativeDeliveries: 1);

        Assert.False(correctness.BranchesExact);
        Assert.False(correctness.TaskContractPass);
    }

    [Fact]
    public async Task RequiredFailure_AcceptsSkippedSynthesis()
    {
        const string RunId = "required";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.RequiredBranchFailure);
        ReferencePhaseArtifact synthesis =
            ReferencePhaseArtifact.Skipped(
                SynthesisPhaseExecutor.Phase,
                200,
                true,
                "blocked-by-required-branch",
                fixture.Manifest.Artifact!);
        var delivery = new IdempotentDeliverySink();
        ReferencePipelineResult result =
            await new DeliveryExecutor(
                artifacts,
                delivery,
                RunId).HandleAsync(
                    synthesis,
                    context: null!,
                    TestContext.Current.CancellationToken);

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            delivery.AuthoritativeCount);

        Assert.True(correctness.ArtifactContractValid);
        Assert.True(correctness.EvidenceExactSet);
        Assert.True(correctness.GapsExactSet);
        Assert.True(correctness.RequiredCoverageMatches);
        Assert.True(correctness.SuccessfulSiblingsPreserved);
        Assert.True(correctness.TaskContractPass);
    }

    [Fact]
    public async Task CorrectionExhausted_UsesDeliveredSynthesisGap()
    {
        const string RunId = "correction-exhausted";
        var artifacts = new ReferenceArtifactStore();
        HostedEvaluationFixtureData fixture =
            HostedEvaluationFixture.Create(
                artifacts,
                RunId,
                HostedEvaluationScenario.CorrectionExhausted);
        ReferencePhaseArtifact synthesis =
            ReferencePhaseArtifact.Failed(
                SynthesisPhaseExecutor.Phase,
                200,
                true,
                "missing-recommendation",
                [fixture.Manifest.Artifact!],
                "synthesis:missing-recommendation");
        var delivery = new IdempotentDeliverySink();
        ReferencePipelineResult result =
            await new DeliveryExecutor(
                artifacts,
                delivery,
                RunId).HandleAsync(
                    synthesis,
                    context: null!,
                    TestContext.Current.CancellationToken);

        var (correctness, _) = HostedDeterministicScorer.Score(
            artifacts,
            RunId,
            fixture,
            result,
            delivery.AuthoritativeCount);

        Assert.True(correctness.GapsExactSet);
        Assert.True(correctness.PipelineOutcomeMatches);
        Assert.True(correctness.SynthesisOutcomeMatches);
        Assert.True(correctness.TaskContractPass);
    }

    private static ReferencePipelineResult CreateResult(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationFixtureData fixture,
        string synthesisContent)
    {
        ReferenceArtifactReference synthesisReference = artifacts.Write(
            runId,
            synthesisContent);
        ReferencePhaseArtifact synthesis =
            ReferencePhaseArtifact.WithArtifact(
                ReferencePhaseArtifact.Candidate(
                    SynthesisPhaseExecutor.Phase,
                    200,
                    true,
                    "{}",
                    fixture.Manifest.Artifact!),
                synthesisReference,
                ReferencePipelineOutcome.Completed,
                fixture.Expected.Gaps);
        ReferenceArtifactManifest manifest = artifacts.GetManifest(
            fixture.Manifest.Artifact!,
            runId);
        return new ReferencePipelineResult(
            runId,
            fixture.Expected.PipelineOutcome ??
                ReferencePipelineOutcome.Completed,
            $"delivery/{runId}",
            fixture.Manifest.Artifact!,
            synthesis,
            manifest.Branches,
            fixture.Expected.Gaps);
    }

    private static string CreateArtifact(
        string[] evidence,
        string[] gaps) =>
        JsonSerializer.Serialize(
            new
            {
                summary = "Synthesis",
                evidence,
                gaps,
                recommendation = "Proceed",
            });
}
