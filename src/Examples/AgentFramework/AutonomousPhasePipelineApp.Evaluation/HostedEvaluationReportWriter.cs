using System.Text;
using System.Text.Json;

using NexusLabs.Foundry.Evaluation.Experiments;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationReportWriter
{
    internal static async Task<HostedEvaluationReport> WriteAsync(
        HostedEvaluationProtocol protocol,
        ExperimentRunOutcome<
            HostedEvaluationCase,
            HostedEvaluationBlockResult> outcome,
        CancellationToken cancellationToken)
    {
        HostedEvaluationBlockResult[] blocks = outcome.Result.Items
            .Where(item => item.HasOutput && item.Output is not null)
            .Select(item => item.Output!)
            .ToArray();
        HostedEvaluationItemFailure[] itemFailures = outcome.Result.Items
            .Where(item => !item.HasOutput)
            .Select(item => new HostedEvaluationItemFailure(
                item.Case.Id,
                item.TrialIndex,
                item.Status.ToString(),
                item.Failure?.Code.ToString(),
                item.Failure?.Message))
            .ToArray();
        HostedEvaluationArmResult[] results = blocks
            .SelectMany(block => block.Arms)
            .ToArray();
        int scenarioFailureCount = results.Count(result =>
            result.Applicable &&
            HostedEvaluationRecommendationGate.IsAdmissible(
                result) &&
            !result.ScenarioContractPass);
        int infrastructureFailureCount =
            itemFailures.Length +
            results.Count(result =>
                result.Applicable &&
                (result.Infrastructure.ProviderFailureCount > 0 ||
                 result.Infrastructure.PhaseFailureCount > 0));
        HostedEvaluationRecommendationDecision recommendation =
            HostedEvaluationRecommendationGate.Evaluate(
                protocol,
                blocks,
                itemFailures.Length);
        HostedEvaluationArmSummary[] summaries =
            Enum.GetValues<HostedEvaluationArm>()
                .Select(arm => BuildSummary(results, arm))
                .ToArray();
        HostedEvaluationScenarioSummary[] scenarioSummaries =
            Enum.GetValues<HostedEvaluationArm>()
                .SelectMany(arm =>
                    Enum.GetValues<HostedEvaluationScenario>()
                        .Select(scenario =>
                            BuildScenarioSummary(
                                results,
                                arm,
                                scenario)))
                .ToArray();
        string runState = recommendation.Status switch
        {
            HostedEvaluationRecommendationStatus.ProtocolInvalid =>
                "ProtocolInvalid",
            HostedEvaluationRecommendationStatus
                .InfrastructureUnreliable =>
                "InfrastructureUnreliable",
            _ when scenarioFailureCount > 0 =>
                "CompletedWithScenarioFailures",
            _ => "Completed",
        };
        var report = new HostedEvaluationReport(
            SchemaVersion: 2,
            ProtocolVersion: HostedEvaluationProtocol.Version,
            RunId: protocol.RunId,
            CommitSha: protocol.CommitSha,
            Model: protocol.Model,
            TrialCount: protocol.TrialCount,
            RunState: runState,
            TotalItems: outcome.Result.Items.Count,
            CompletedBlocks: blocks.Length,
            ScenarioFailureCount: scenarioFailureCount,
            InfrastructureFailureCount: infrastructureFailureCount,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Recommendation: recommendation,
            ArmSummaries: summaries,
            ScenarioSummaries: scenarioSummaries,
            Blocks: blocks,
            ItemFailures: itemFailures);
        string reportPath = Path.Combine(
            protocol.OutputDirectory,
            "report.json");
        await using (FileStream stream = File.Create(reportPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                report,
                HostedEvaluationJsonContext.Default.HostedEvaluationReport,
                cancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(
                protocol.OutputDirectory,
                "report.md"),
            BuildMarkdown(report),
            cancellationToken);
        return report;
    }

    private static HostedEvaluationArmSummary BuildSummary(
        IReadOnlyList<HostedEvaluationArmResult> results,
        HostedEvaluationArm arm)
    {
        HostedEvaluationArmResult[] applicable = results
            .Where(result =>
                result.Arm == arm &&
                result.Applicable)
            .ToArray();
        HostedEvaluationArmResult[] pooled = applicable
            .Where(result =>
                HostedEvaluationDatasetCatalog.IsPoolable(
                    result.Scenario))
            .ToArray();
        HostedEvaluationArmResult[] admissible = pooled
            .Where(HostedEvaluationRecommendationGate.IsAdmissible)
            .ToArray();
        int passed = admissible.Count(result =>
            result.ScenarioContractPass);
        double? rate = admissible.Length == 0
            ? null
            : (double)passed / admissible.Length;
        return new(
            arm,
            applicable.Length,
            applicable.Length - pooled.Length,
            admissible.Length,
            passed,
            rate,
            applicable.Sum(result =>
                result.Resources.ProviderCallsUsed),
            applicable.Sum(result =>
                result.Resources.InputTokens),
            applicable.Sum(result =>
                result.Resources.OutputTokens),
            applicable.Sum(result =>
                result.Resources.DurationMilliseconds));
    }

    private static HostedEvaluationScenarioSummary BuildScenarioSummary(
        IReadOnlyList<HostedEvaluationArmResult> results,
        HostedEvaluationArm arm,
        HostedEvaluationScenario scenario)
    {
        HostedEvaluationArmResult[] admissible = results
            .Where(result =>
                result.Arm == arm &&
                result.Scenario == scenario &&
                result.Applicable &&
                HostedEvaluationRecommendationGate.IsAdmissible(
                    result))
            .ToArray();
        int passed = admissible.Count(result =>
            result.ScenarioContractPass);
        return new(
            arm,
            scenario,
            HostedEvaluationDatasetCatalog.IsPoolable(scenario),
            admissible.Length,
            passed,
            admissible.Length == 0
                ? null
                : (double)passed / admissible.Length);
    }

    private static string BuildMarkdown(
        HostedEvaluationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Autonomous Phase Evaluation Protocol v2");
        builder.AppendLine();
        builder.AppendLine($"- Run: `{report.RunId}`");
        builder.AppendLine($"- Commit: `{report.CommitSha}`");
        builder.AppendLine($"- Requested model: `{report.Model}`");
        builder.AppendLine($"- Trials per scenario: {report.TrialCount}");
        builder.AppendLine($"- Run state: **{report.RunState}**");
        builder.AppendLine(
            $"- Completed blocks: {report.CompletedBlocks}/{report.TotalItems}");
        builder.AppendLine(
            $"- Scenario failures: {report.ScenarioFailureCount}");
        builder.AppendLine(
            $"- Infrastructure failures: {report.InfrastructureFailureCount}");
        builder.AppendLine(
            $"- Recommendation status: **{report.Recommendation.Status}**");
        builder.AppendLine(
            $"- Recommendation reason: {report.Recommendation.Reason}");
        builder.AppendLine();
        builder.AppendLine(
            "| Arm | Pooled admissible | Non-poolable controls | Passed | Descriptive pass rate | Provider calls | Input tokens | Output tokens |");
        builder.AppendLine(
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (HostedEvaluationArmSummary summary in report.ArmSummaries)
        {
            string rate = summary.PassRate is null
                ? "n/a"
                : $"{summary.PassRate:P1}";
            builder.AppendLine(
                $"| {summary.Arm} | {summary.AdmissibleResults} | " +
                $"{summary.NonPoolableResults} | " +
                $"{summary.PassedResults} | {rate} | " +
                $"{summary.ProviderCalls} | {summary.InputTokens} | " +
                $"{summary.OutputTokens} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            "Pass rates are scenario-stratified descriptive summaries, not IID confidence claims. Confirmatory comparisons require paired scenario-level inference.");
        builder.AppendLine();
        builder.AppendLine(
            "| Arm | Scenario | Poolable | Admissible | Passed | Pass rate |");
        builder.AppendLine(
            "| --- | --- | --- | ---: | ---: | ---: |");
        foreach (
            HostedEvaluationScenarioSummary summary in
            report.ScenarioSummaries)
        {
            string rate = summary.PassRate is null
                ? "n/a"
                : $"{summary.PassRate:P1}";
            builder.AppendLine(
                $"| {summary.Arm} | {summary.Scenario} | " +
                $"{summary.Poolable} | {summary.AdmissibleResults} | " +
                $"{summary.PassedResults} | {rate} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            "Correctness and resilience are scored independently. Provider calls, tokens, latency, child sessions, and Magentic topology are descriptive resource evidence unless a scenario explicitly targets that behavior.");
        builder.AppendLine();
        builder.AppendLine(
            "Semantic quality is calibration-required and cannot influence this recommendation status.");
        if (report.ItemFailures.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Infrastructure failures");
            foreach (HostedEvaluationItemFailure failure in report.ItemFailures)
            {
                builder.AppendLine(
                    $"- `{failure.CaseId}` trial {failure.TrialIndex}: " +
                    $"{failure.Status} / {failure.FailureCode} / " +
                    $"{failure.Message}");
            }
        }

        return builder.ToString();
    }
}
