using System.Text.Json;

using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Tests;

public sealed class ReferenceArtifactValidatorTests
{
    private const string RunId = "artifact-contract-run";
    private const string ResearchId = "artifact://sha256/research";
    private const string RiskId = "artifact://sha256/risk";
    private const string UnacceptedOperationsId =
        "artifact://sha256/operations-failed";
    private const string OperationsGap = "operations:timeout";

    public static TheoryData<string, bool, string> SynthesisArtifactCases =>
        new()
        {
            {
                CreateSynthesis(
                    [ResearchId, RiskId],
                    [OperationsGap]),
                true,
                string.Empty
            },
            {
                CreateSynthesis(
                    [RiskId, ResearchId],
                    [OperationsGap]),
                true,
                string.Empty
            },
            {
                CreateSynthesis(
                    [RiskId],
                    [OperationsGap]),
                false,
                $"missing-required-evidence:{ResearchId}"
            },
            {
                CreateSynthesis(
                    [ResearchId, RiskId, "artifact://sha256/unknown"],
                    [OperationsGap]),
                false,
                "unexpected-evidence:artifact://sha256/unknown"
            },
            {
                CreateSynthesis(
                    ["research", RiskId],
                    [OperationsGap]),
                false,
                "unexpected-evidence:research"
            },
            {
                CreateSynthesis(
                    [ResearchId, RiskId, UnacceptedOperationsId],
                    [OperationsGap]),
                false,
                $"unexpected-evidence:{UnacceptedOperationsId}"
            },
            {
                CreateSynthesis(
                    [ResearchId, ResearchId, RiskId],
                    [OperationsGap]),
                false,
                $"duplicate-evidence:{ResearchId}"
            },
            {
                CreateSynthesis(
                    [ResearchId, RiskId],
                    []),
                false,
                $"missing-gap:{OperationsGap}"
            },
            {
                CreateSynthesis(
                    [ResearchId, RiskId],
                    [OperationsGap, "invented-gap"]),
                false,
                "unexpected-gap:invented-gap"
            },
            {
                CreateSynthesis(
                    [ResearchId, RiskId],
                    [OperationsGap, OperationsGap]),
                false,
                $"duplicate-gap:{OperationsGap}"
            },
            {
                CreateSynthesis(
                    [ResearchId, RiskId],
                    [OperationsGap],
                    includeRecommendation: false),
                false,
                "missing-recommendation"
            },
            {
                CreateSynthesisWithoutGaps(
                    [ResearchId, RiskId]),
                false,
                "missing-gaps"
            },
            {
                "{",
                false,
                "invalid-json"
            },
        };

    [Theory]
    [MemberData(nameof(SynthesisArtifactCases))]
    public void SynthesisArtifactContract_UsesExactManifestEvidence(
        string content,
        bool expectedAccepted,
        string expectedError)
    {
        ReferenceArtifactManifest manifest = CreateManifest();

        bool accepted = ReferenceArtifactValidator.TryValidateSynthesis(
            content,
            manifest,
            out string error);

        Assert.Equal(expectedAccepted, accepted);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void SynthesisArtifactContract_IgnoresExecutionTopologyMetadata()
    {
        ReferenceArtifactManifest manifest = CreateManifest();
        string direct = CreateSynthesis(
            [ResearchId, RiskId],
            [OperationsGap],
            topology: new
            {
                delegatedChildren = 0,
                toolOrder = Array.Empty<string>(),
            });
        string delegated = CreateSynthesis(
            [RiskId, ResearchId],
            [OperationsGap],
            topology: new
            {
                delegatedChildren = 7,
                toolOrder = new[] { "retrieve", "delegate", "wait" },
            });

        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                direct,
                manifest,
                out string directError),
            directError);
        Assert.True(
            ReferenceArtifactValidator.TryValidateSynthesis(
                delegated,
                manifest,
                out string delegatedError),
            delegatedError);
    }

    [Fact]
    public async Task ManifestBarrier_RejectsArtifactFromFailedBranch()
    {
        var artifacts = new ReferenceArtifactStore();
        ReferenceArtifactReference research = artifacts.Write(
            RunId,
            """{"topic":"release","evidence":["source"]}""");
        ReferenceArtifactReference riskArtifact = artifacts.Write(
            RunId,
            """{"summary":"risk","evidence":["source"]}""");
        ReferenceArtifactReference failedArtifact = artifacts.Write(
            RunId,
            """{"summary":"failed","evidence":["source"]}""");
        var riskDefinition = new ReferenceBranchDefinition(
            "risk",
            0,
            true);
        var operationsDefinition = new ReferenceBranchDefinition(
            "operations",
            1,
            false);
        ReferencePhaseArtifact risk = ReferencePhaseArtifact.WithArtifact(
            ReferencePhaseArtifact.Candidate(
                "risk",
                0,
                true,
                "{}",
                research),
            riskArtifact);
        ReferencePhaseArtifact operations = ReferencePhaseArtifact.Failed(
            "operations",
            1,
            false,
            "timeout",
            [research]) with
        {
            Artifact = failedArtifact,
        };
        var barrier = new SpecialistManifestBarrierExecutor(
            artifacts,
            RunId,
            [riskDefinition, operationsDefinition]);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => barrier.HandleAsync(
                    [risk, operations],
                    null!,
                    TestContext.Current.CancellationToken).AsTask());

        Assert.Contains(
            "carried an artifact",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ReferenceArtifactManifest CreateManifest()
    {
        ReferenceArtifactReference research = CreateReference(ResearchId);
        ReferenceArtifactReference riskReference = CreateReference(RiskId);
        ReferencePhaseArtifact risk = ReferencePhaseArtifact.WithArtifact(
            ReferencePhaseArtifact.Candidate(
                "risk",
                0,
                true,
                "{}",
                research),
            riskReference);
        ReferencePhaseArtifact operations = ReferencePhaseArtifact.Failed(
            "operations",
            1,
            false,
            "timeout",
            [research],
            OperationsGap);
        return new ReferenceArtifactManifest(
            RunId,
            research,
            [risk, operations],
            ReferencePipelineOutcome.Partial,
            [OperationsGap]);
    }

    private static ReferenceArtifactReference CreateReference(
        string id) =>
        new(
            id,
            id["artifact://sha256/".Length..],
            1,
            "application/json");

    private static string CreateSynthesis(
        string[] evidence,
        string[] gaps,
        bool includeRecommendation = true,
        object? topology = null)
    {
        var artifact = new Dictionary<string, object?>
        {
            ["summary"] = "Synthesis",
            ["evidence"] = evidence,
            ["gaps"] = gaps,
        };
        if (includeRecommendation)
        {
            artifact["recommendation"] = "Proceed";
        }

        if (topology is not null)
        {
            artifact["executionTopology"] = topology;
        }

        return JsonSerializer.Serialize(artifact);
    }

    private static string CreateSynthesisWithoutGaps(
        string[] evidence) =>
        JsonSerializer.Serialize(
            new
            {
                summary = "Synthesis",
                recommendation = "Proceed",
                evidence,
            });
}
