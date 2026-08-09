using NexusLabs.Foundry.Langfuse;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationLangfusePublisher
{
    internal static async Task<HostedEvaluationDatasetPublication>
        PublishDatasetAsync(
            ILangfuseClient langfuse,
            HostedEvaluationProtocol protocol,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(langfuse);
        ArgumentNullException.ThrowIfNull(protocol);
        IReadOnlyList<HostedEvaluationDatasetItem> items =
            HostedEvaluationDatasetCatalog.Create();
        if (!langfuse.IsEnabled)
        {
            return new(
                protocol.DatasetName,
                ExpectedItems: items.Count,
                ObservedItems: 0,
                Version: null,
                ReadBackVerified: false);
        }

        await langfuse.Datasets.EnsureDatasetAsync(
            protocol.DatasetName,
            "Foundry autonomous-phase protocol-v2 cases. Arms are separate experiment runs over identical items.",
            cancellationToken);
        foreach (HostedEvaluationDatasetItem item in items)
        {
            await langfuse.Datasets.UpsertItemAsync(
                new LangfuseDatasetItem
                {
                    DatasetName = protocol.DatasetName,
                    Id = item.Id,
                    Input = item.Input,
                    ExpectedOutput = item.ExpectedOutput,
                    Metadata = item.Metadata,
                },
                cancellationToken);
        }

        LangfuseDatasetSnapshot snapshot =
            await langfuse.Datasets.GetDatasetAsync(
                new LangfuseDatasetSelection
                {
                    Name = protocol.DatasetName,
                },
                cancellationToken);
        string[] expectedIds = items
            .Select(item => item.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] observedIds = snapshot.Items
            .Select(item => item.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        LangfuseDatasetItemSnapshot[] expectedSnapshots = snapshot.Items
            .Where(item => expectedIds.Contains(
                item.Id,
                StringComparer.Ordinal))
            .ToArray();
        DateTimeOffset? version = expectedSnapshots.Length == 0
            ? null
            : expectedSnapshots.Max(item => item.UpdatedAt);
        return new(
            protocol.DatasetName,
            expectedIds.Length,
            observedIds.Length,
            version,
            expectedIds.SequenceEqual(
                observedIds,
                StringComparer.Ordinal));
    }

    internal static async Task RecordItemScoresAsync(
        ILangfuseScenario scenario,
        string scoreIdPrefix,
        HostedEvaluationArmResult result,
        CancellationToken cancellationToken)
    {
        await scenario.RecordScoreAsync(
            "correctness_scenario_contract_pass",
            result.ScenarioContractPass,
            ScoreOptions(
                scoreIdPrefix,
                "correctness_scenario_contract_pass"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "correctness_artifact_contract_valid",
            result.Correctness.ArtifactContractValid,
            ScoreOptions(
                scoreIdPrefix,
                "correctness_artifact_contract_valid"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "correctness_evidence_exact_set",
            result.Correctness.EvidenceExactSet,
            ScoreOptions(
                scoreIdPrefix,
                "correctness_evidence_exact_set"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "correctness_gaps_exact_set",
            result.Correctness.GapsExactSet,
            ScoreOptions(
                scoreIdPrefix,
                "correctness_gaps_exact_set"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "resilience_fault_activated",
            result.Resilience.FaultActivated,
            ScoreOptions(
                scoreIdPrefix,
                "resilience_fault_activated"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "resources_provider_calls",
            result.Resources.ModelCalls,
            ScoreOptions(
                scoreIdPrefix,
                "resources_provider_calls"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "resources_duration_ms",
            result.Resources.DurationMilliseconds,
            ScoreOptions(
                scoreIdPrefix,
                "resources_duration_ms"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "infrastructure_status",
            result.Infrastructure.ExecutionStatus.ToString(),
            ScoreOptions(
                scoreIdPrefix,
                "infrastructure_status"),
            cancellationToken);
        await scenario.RecordScoreAsync(
            "observed_model",
            result.Provenance.ObservedModel ?? "unknown",
            ScoreOptions(
                scoreIdPrefix,
                "observed_model"),
            cancellationToken);
    }

    private static LangfuseScoreOptions ScoreOptions(
        string prefix,
        string name) =>
        new()
        {
            Id = $"{prefix}:{name}",
        };
}
