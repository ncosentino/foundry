using System.Security.Cryptography;
using System.Text.Json;

using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationDatasetCatalog
{
    internal static string GetItemId(
        string caseId,
        HostedEvaluationScenario scenario) =>
        $"foundry::autonomous-phase-synthesis::{caseId}::{scenario}::v2";

    internal static bool IsPoolable(
        HostedEvaluationScenario scenario) =>
        scenario is not (
            HostedEvaluationScenario.RequiredBranchFailure or
            HostedEvaluationScenario.DeliveryReplay or
            HostedEvaluationScenario.IneffectiveProgress);

    internal static IReadOnlyList<HostedEvaluationDatasetItem> Create()
    {
        var items = new List<HostedEvaluationDatasetItem>();
        foreach (
            (HostedEvaluationScenario scenario, int ordinal) in
            Enum.GetValues<HostedEvaluationScenario>()
                .Select((scenario, ordinal) => (scenario, ordinal)))
        {
            string caseId = "release-readiness";
            var @case = new HostedEvaluationCase(
                caseId,
                scenario,
                ordinal);
            var artifacts = new ReferenceArtifactStore();
            HostedEvaluationFixtureData fixture =
                HostedEvaluationFixture.Create(
                    artifacts,
                    $"dataset::{caseId}::{scenario}",
                    scenario);
            var input = new HostedEvaluationDatasetInput(
                SchemaVersion: 2,
                HostedEvaluationProtocol.Version,
                caseId,
                scenario,
                HostedSynthesisArmFactory.MaxProviderCalls,
                HostedSynthesisArmFactory.MaxArtifactAttempts);
            string digest = ComputeDigest(
                input,
                fixture.Expected);
            items.Add(
                new HostedEvaluationDatasetItem(
                    GetItemId(caseId, scenario),
                    @case,
                    input,
                    fixture.Expected,
                    new HostedEvaluationDatasetMetadata(
                        Split: "validation",
                        ScenarioFamily:
                            GetScenarioFamily(scenario),
                        PoolableForArmComparison:
                            IsPoolable(scenario),
                        FixtureDigest: digest)));
        }

        return items;
    }

    private static string GetScenarioFamily(
        HostedEvaluationScenario scenario) =>
        scenario switch
        {
            HostedEvaluationScenario.OptionalBranchFailure or
            HostedEvaluationScenario.RequiredBranchFailure =>
                "branch-failure",
            HostedEvaluationScenario.CorrectionSucceeds or
            HostedEvaluationScenario.CorrectionExhausted or
            HostedEvaluationScenario.IneffectiveProgress =>
                "correction",
            HostedEvaluationScenario.Cancellation =>
                "cancellation",
            HostedEvaluationScenario.CheckpointRestore =>
                "same-run-restore",
            HostedEvaluationScenario.DeliveryReplay =>
                "delivery",
            _ => "success",
        };

    private static string ComputeDigest(
        HostedEvaluationDatasetInput input,
        HostedEvaluationExpectedOutput expected)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                input,
                expected,
            });
        return Convert
            .ToHexString(SHA256.HashData(payload))
            .ToLowerInvariant();
    }
}
