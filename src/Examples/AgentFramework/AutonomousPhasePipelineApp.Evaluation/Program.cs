using System.Text.Json;

using AutonomousPhasePipelineApp.Evaluation;

using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.Evaluation.Experiments;

HostedEvaluationProtocol protocol =
    HostedEvaluationProtocol.FromEnvironment();
Directory.CreateDirectory(protocol.OutputDirectory);
var incrementalWriter = new HostedIncrementalResultWriter(
    protocol.OutputDirectory,
    protocol.RunId);

ProviderProbeResult providerProbe = await CopilotProviderProbe.RunAsync(
    protocol.Model,
    protocol.CommitSha,
    CancellationToken.None);
await using (FileStream stream = File.Create(
    Path.Combine(
        protocol.OutputDirectory,
        "provider-probe.json")))
{
    await JsonSerializer.SerializeAsync(
        stream,
        providerProbe,
        ProviderProbeJsonContext.Default.ProviderProbeResult);
}

if (!providerProbe.Succeeded)
{
    await incrementalWriter.WriteStatusAsync(
        state: "BlockedProvider",
        completedBlocks: 0,
        totalBlocks: 0,
        CancellationToken.None);
    Console.Error.WriteLine(
        $"AutonomousPhaseEvaluation:blocked-provider:{providerProbe.Stage}:{providerProbe.ErrorType}:{providerProbe.ErrorMessage}");
    return 1;
}

IReadOnlyList<ExperimentCase<HostedEvaluationCase>> cases =
    HostedEvaluationCaseCatalog.Create(protocol.TrialCount);
int totalBlocks = cases.Sum(@case => @case.TrialCount);
int completedBlocks = 0;
await incrementalWriter.WriteStatusAsync(
    state: "Running",
    completedBlocks,
    totalBlocks,
    CancellationToken.None);

var definition = new ExperimentDefinition<
    HostedEvaluationCase,
    HostedEvaluationBlockResult>
{
    Name = HostedEvaluationProtocol.Version,
    CaseSource = new LocalExperimentCaseSource<HostedEvaluationCase>(
        $"autonomous-phase-cases@{protocol.CommitSha}",
        cases),
    Task = async (context, cancellationToken) =>
    {
        HostedEvaluationArm[] armOrder =
            HostedEvaluationSchedule.GetOrder(
                context.Case.Value.ScenarioOrdinal,
                context.TrialIndex);
        string blockId =
            $"{context.Case.Value.CaseId}:{context.Case.Value.Scenario}:r{context.TrialIndex:D2}";
        var results = new List<HostedEvaluationArmResult>(
            armOrder.Length);
        foreach (HostedEvaluationArm arm in armOrder)
        {
            results.Add(
                await HostedEvaluationArmDriver.RunAsync(
                    protocol,
                    context.Case.Value,
                    context.TrialIndex,
                    arm,
                    cancellationToken));
        }

        var block = new HostedEvaluationBlockResult(
            blockId,
            context.Case.Value.CaseId,
            context.Case.Value.Scenario,
            context.TrialIndex,
            armOrder,
            [.. results]);
        await incrementalWriter.WriteBlockAsync(
            block,
            cancellationToken);
        int completed = Interlocked.Increment(
            ref completedBlocks);
        await incrementalWriter.WriteStatusAsync(
            state: "Running",
            completed,
            totalBlocks,
            cancellationToken);
        return block;
    },
    ItemEvaluator = (context, _) =>
    {
        HostedEvaluationArmResult[] applicable = context.Output.Arms
            .Where(arm => arm.Applicable)
            .ToArray();
        double passRate = applicable.Length == 0
            ? 0
            : (double)applicable.Count(arm => arm.ScenarioContractPass) /
              applicable.Length;
        return ValueTask.FromResult(
            new EvaluationResult(
                new NumericMetric(
                    "scenario_contract_pass_rate",
                    passRate),
                new NumericMetric(
                    "model_calls",
                    applicable.Sum(arm => arm.ModelCalls)),
                new NumericMetric(
                    "total_tokens",
                    applicable.Sum(arm =>
                        arm.InputTokens + arm.OutputTokens)),
                new BooleanMetric(
                    "all_applicable_arms_reported",
                    applicable.Length > 0)));
    },
    RunEvaluators =
    [
        new ExperimentRunEvaluator<
            HostedEvaluationCase,
            HostedEvaluationBlockResult>(
            "diagnostic-summary",
            (context, _) =>
            {
                HostedEvaluationArmResult[] arms = context.Items
                    .Where(item =>
                        item.HasOutput &&
                        item.Output is not null)
                    .SelectMany(item => item.Output!.Arms)
                    .Where(arm => arm.Applicable)
                    .ToArray();
                double passRate = arms.Length == 0
                    ? 0
                    : (double)arms.Count(arm => arm.ScenarioContractPass) /
                      arms.Length;
                return ValueTask.FromResult(
                    new EvaluationResult(
                        new NumericMetric(
                            "applicable_arm_runs",
                            arms.Length),
                        new NumericMetric(
                            "scenario_contract_pass_rate",
                            passRate),
                        new NumericMetric(
                            "provider_failures",
                            arms.Sum(arm => arm.PhaseFailureCount))));
            }),
    ],
};

ExperimentRunOutcome<
    HostedEvaluationCase,
    HostedEvaluationBlockResult> outcome =
    await new ExperimentRunner().RunAsync(
        definition,
        new ExperimentRunOptions
        {
            RunId = protocol.RunId,
            MaxConcurrency = 1,
            AttemptTimeout = TimeSpan.FromMinutes(45),
        },
        CancellationToken.None);

await using (FileStream stream = File.Create(
    Path.Combine(
        protocol.OutputDirectory,
        "experiment.json")))
{
    await new ExperimentJsonArtifactWriter().WriteAsync(
        stream,
        outcome,
        ProviderProbeJsonContext.Default.HostedEvaluationCase,
        ProviderProbeJsonContext.Default.HostedEvaluationBlockResult,
        CancellationToken.None);
}

HostedEvaluationReport report =
    await HostedEvaluationReportWriter.WriteAsync(
        protocol,
        providerProbe,
        outcome,
        CancellationToken.None);
await incrementalWriter.WriteStatusAsync(
    state: "Completed",
    completedBlocks,
    totalBlocks,
    CancellationToken.None);

Console.WriteLine(
    $"AutonomousPhaseEvaluation:provider-probe:passed");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:blocks:{report.Blocks.Length}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:recommendation:{report.Recommendation}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:artifact:{Path.GetFullPath(protocol.OutputDirectory)}");
return 0;
