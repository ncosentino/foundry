using System.Text;
using System.Text.Json;

using NexusLabs.Foundry.Evaluation.Experiments;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationReportWriter
{
    internal static async Task<HostedEvaluationReport> WriteAsync(
        HostedEvaluationProtocol protocol,
        ProviderProbeResult providerProbe,
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
        int contractFailureCount = blocks
            .SelectMany(block => block.Arms)
            .Count(arm =>
                arm.Applicable &&
                !arm.ScenarioContractPass);
        int infrastructureFailureCount =
            itemFailures.Length +
            Math.Max(
                0,
                outcome.Result.Items.Count - blocks.Length - itemFailures.Length);
        string runState = infrastructureFailureCount > 0
            ? "FailedInfrastructure"
            : contractFailureCount > 0
                ? "CompletedWithContractFailures"
                : "Completed";
        var report = new HostedEvaluationReport(
            SchemaVersion: 1,
            ProtocolVersion: HostedEvaluationProtocol.Version,
            RunId: protocol.RunId,
            CommitSha: protocol.CommitSha,
            Model: protocol.Model,
            TrialCount: protocol.TrialCount,
            RunState: runState,
            TotalItems: outcome.Result.Items.Count,
            CompletedBlocks: blocks.Length,
            ContractFailureCount: contractFailureCount,
            InfrastructureFailureCount: infrastructureFailureCount,
            EvidenceStrength: "INSUFFICIENTLY_POWERED",
            Recommendation: "NO_SUPPORTED_RECOMMENDATION_YET",
            ExtractionRecommendation:
                "Keep all synthesis-arm integration and evaluation code example-local until a fixed-size hosted study justifies extraction.",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            ProviderProbe: providerProbe,
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
                ProviderProbeJsonContext.Default.HostedEvaluationReport,
                cancellationToken);
        }

        string markdown = BuildMarkdown(report);
        await File.WriteAllTextAsync(
            Path.Combine(
                protocol.OutputDirectory,
                "report.md"),
            markdown,
            cancellationToken);
        return report;
    }

    private static string BuildMarkdown(
        HostedEvaluationReport report)
    {
        HostedEvaluationArmResult[] arms = report.Blocks
            .SelectMany(block => block.Arms)
            .ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("# Autonomous Phase Hosted Evaluation");
        builder.AppendLine();
        builder.AppendLine($"- Run: `{report.RunId}`");
        builder.AppendLine($"- Commit: `{report.CommitSha}`");
        builder.AppendLine($"- Model: `{report.Model}`");
        builder.AppendLine($"- Trials per scenario: {report.TrialCount}");
        builder.AppendLine($"- Run state: **{report.RunState}**");
        builder.AppendLine($"- Completed blocks: {report.CompletedBlocks}/{report.TotalItems}");
        builder.AppendLine($"- Contract failures: {report.ContractFailureCount}");
        builder.AppendLine($"- Infrastructure failures: {report.InfrastructureFailureCount}");
        builder.AppendLine($"- Evidence strength: **{report.EvidenceStrength}**");
        builder.AppendLine($"- Recommendation: **{report.Recommendation}**");
        builder.AppendLine();
        builder.AppendLine("| Arm | Applicable | Contract passes | Model calls | Input tokens | Output tokens |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (HostedEvaluationArm arm in Enum.GetValues<HostedEvaluationArm>())
        {
            HostedEvaluationArmResult[] armResults = arms
                .Where(result =>
                    result.Arm == arm &&
                    result.Applicable)
                .ToArray();
            builder.AppendLine(
                $"| {arm} | {armResults.Length} | " +
                $"{armResults.Count(result => result.ScenarioContractPass)} | " +
                $"{armResults.Sum(result => result.ModelCalls)} | " +
                $"{armResults.Sum(result => result.InputTokens)} | " +
                $"{armResults.Sum(result => result.OutputTokens)} |");
        }

        builder.AppendLine();
        builder.AppendLine(
            "This diagnostic run has no calibrated model-based quality score and is not powered for superiority or non-inferiority claims.");
        if (report.ItemFailures.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Infrastructure failures");
            foreach (HostedEvaluationItemFailure failure in report.ItemFailures)
            {
                builder.AppendLine(
                    $"- `{failure.CaseId}` trial {failure.TrialIndex}: " +
                    $"{failure.Status} / {failure.FailureCode} / {failure.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Extraction: {report.ExtractionRecommendation}");
        return builder.ToString();
    }
}
