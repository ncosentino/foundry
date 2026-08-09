using NexusLabs.Foundry.Langfuse;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedEvaluationLangfuseRunSet
{
    private readonly Dictionary<
        HostedEvaluationArm,
        ILangfuseExperimentRun> _runs;
    private readonly IReadOnlyDictionary<
        string,
        HostedEvaluationDatasetItem> _items;
    private readonly HostedEvaluationProtocol _protocol;
    private readonly string _runId;

    internal HostedEvaluationLangfuseRunSet(
        ILangfuseClient langfuse,
        HostedEvaluationProtocol protocol,
        DateTimeOffset? datasetVersion)
    {
        ArgumentNullException.ThrowIfNull(langfuse);
        ArgumentNullException.ThrowIfNull(protocol);
        _protocol = protocol;
        _runId = protocol.RunId;
        _items = HostedEvaluationDatasetCatalog.Create()
            .ToDictionary(
                item => item.Id,
                StringComparer.Ordinal);
        _runs = Enum.GetValues<HostedEvaluationArm>()
            .ToDictionary(
                arm => arm,
                arm => langfuse.BeginExperimentRun(
                    protocol.DatasetName,
                    $"{protocol.RunId}:{arm}",
                    new LangfuseExperimentRunOptions
                    {
                        Description =
                            $"Foundry autonomous-phase protocol-v2 {arm} run.",
                        DatasetVersion = datasetVersion,
                        Metadata = new
                        {
                            protocolVersion =
                                HostedEvaluationProtocol.Version,
                            protocol.CommitSha,
                            requestedModel = protocol.Model,
                            arm = arm.ToString(),
                            maxProviderCalls =
                                HostedSynthesisArmFactory
                                    .MaxProviderCalls,
                            maxArtifactAttempts =
                                HostedSynthesisArmFactory
                                    .MaxArtifactAttempts,
                        },
                    }));
    }

    internal async Task<HostedEvaluationArmResult> RunArmAsync(
        HostedEvaluationCase @case,
        int trialIndex,
        HostedEvaluationArm arm,
        Func<CancellationToken, Task<HostedEvaluationArmResult>> callback,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        string itemId = HostedEvaluationDatasetCatalog.GetItemId(
            @case.CaseId,
            @case.Scenario);
        HostedEvaluationDatasetItem datasetItem = _items[itemId];
        HostedFaultPlan faultPlan =
            HostedEvaluationFaultCatalog.GetPlan(
                @case.Scenario,
                arm);
        ILangfuseExperimentRun run = _runs[arm];
        LangfuseExperimentItemResult<HostedEvaluationArmResult> result =
            await run.RunItemAsync(
                itemId,
                async (scenario, itemCancellationToken) =>
                {
                    scenario.SetVersion(_protocol.CommitSha);
                    scenario.SetInput(datasetItem.Input);
                    HostedEvaluationArmResult armResult =
                        await callback(itemCancellationToken);
                    scenario.SetOutput(
                        new
                        {
                            armResult.ScenarioContractPass,
                            armResult.ArtifactDigest,
                            pipelineOutcome =
                                armResult.PipelineOutcome?.ToString(),
                            synthesisOutcome =
                                armResult.SynthesisOutcome?.ToString(),
                            observedModel =
                                armResult.Provenance.ObservedModel,
                            failure =
                                armResult.Resilience.FailureCode,
                        });
                    await HostedEvaluationLangfusePublisher
                        .RecordItemScoresAsync(
                            scenario,
                            $"{_runId}:{itemId}:r{trialIndex:D2}:{arm}",
                            armResult,
                            itemCancellationToken);
                    return armResult;
                },
                new LangfuseExperimentItemOptions
                {
                    ScenarioName =
                        "autonomous-phase.synthesis",
                    Tags =
                    [
                        "foundry",
                        "autonomous-phase",
                        arm.ToString(),
                        @case.Scenario.ToString(),
                    ],
                    Metadata = new Dictionary<string, string>
                    {
                        ["protocolVersion"] =
                            HostedEvaluationProtocol.Version,
                        ["commitSha"] = _protocol.CommitSha,
                        ["requestedModel"] = _protocol.Model,
                        ["caseId"] = @case.CaseId,
                        ["scenario"] = @case.Scenario.ToString(),
                        ["arm"] = arm.ToString(),
                        ["fixtureDigest"] =
                            datasetItem.Metadata.FixtureDigest,
                        ["faultMode"] = faultPlan.Mode.ToString(),
                        ["faultActivationPoint"] =
                            faultPlan.ActivationPoint.ToString(),
                        ["faultTargetRole"] =
                            faultPlan.TargetRole.ToString(),
                        ["maxProviderCalls"] =
                            HostedSynthesisArmFactory
                                .MaxProviderCalls.ToString(
                                    System.Globalization
                                        .CultureInfo.InvariantCulture),
                        ["maxArtifactAttempts"] =
                            HostedSynthesisArmFactory
                                .MaxArtifactAttempts.ToString(
                                    System.Globalization
                                        .CultureInfo.InvariantCulture),
                        ["trial"] = trialIndex.ToString(
                            System.Globalization
                                .CultureInfo.InvariantCulture),
                    },
                    LinkFailureMode =
                        LangfuseExperimentItemLinkFailureMode.Strict,
                },
                cancellationToken);
        return result.Value with
        {
            TraceId = result.TraceId ?? result.Value.TraceId,
        };
    }

    internal async Task RecordRunScoresAsync(
        HostedEvaluationReport report,
        CancellationToken cancellationToken)
    {
        foreach (HostedEvaluationArmSummary summary in report.ArmSummaries)
        {
            ILangfuseExperimentRun run = _runs[summary.Arm];
            await run.RecordScoreAsync(
                "run_pass_rate",
                summary.PassRate ?? 0,
                ScoreOptions(
                    report.RunId,
                    summary.Arm,
                    "run_pass_rate"),
                cancellationToken);
            await run.RecordScoreAsync(
                "run_admissible_results",
                summary.AdmissibleResults,
                ScoreOptions(
                    report.RunId,
                    summary.Arm,
                    "run_admissible_results"),
                cancellationToken);
            await run.RecordScoreAsync(
                "run_recommendation_status",
                report.Recommendation.Status.ToString(),
                ScoreOptions(
                    report.RunId,
                    summary.Arm,
                    "run_recommendation_status"),
                cancellationToken);
        }
    }

    private static LangfuseScoreOptions ScoreOptions(
        string runId,
        HostedEvaluationArm arm,
        string name) =>
        new()
        {
            Id = $"{runId}:{arm}:{name}",
        };
}
