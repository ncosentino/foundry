using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp;

internal static class HostedCaseCatalog
{
    internal const string CommonInstructions =
        "Follow the hosted case exactly. Use only the required deterministic tools and do not invent results.";

    private const string LongContext =
        "The authoritative sentinel is CONTEXT-SENTINEL-7F3A. " +
        "Ignore repeated filler and use the tools exactly as instructed.\n";

    internal static HostedCaseDefinition Get(string caseId) =>
        caseId switch
        {
            "h001-01" => Create(
                caseId,
                "Call lookup_north, lookup_south, then combine_values. Do not skip or reorder them.",
                "outputs/tool-orchestration.json",
                """{"status":"complete","result":"north:17|south:29"}""",
                ["lookup_north", "lookup_south", "combine_values"],
                [HarnessEvaluationDimension.ToolTrajectory, HarnessEvaluationDimension.Termination]),
            "h001-02" => Create(
                caseId,
                $"Call read_context and then save_long_context_answer.\n{string.Concat(Enumerable.Repeat(LongContext, 160))}",
                "outputs/long-context-answer.txt",
                "CONTEXT-SENTINEL-7F3A",
                ["read_context", "save_long_context_answer"],
                [HarnessEvaluationDimension.ContextSafety],
                [
                    HarnessEvaluationDimension.CumulativeTokens,
                    HarnessEvaluationDimension.PeakTokens,
                ]),
            "h001-03" => Create(
                caseId,
                "Call generate_large_artifact and then create_artifact_summary.",
                "outputs/artifact-summary.json",
                """{"status":"complete","reusedExistingReference":true}""",
                ["generate_large_artifact", "create_artifact_summary"],
                [HarnessEvaluationDimension.ArtifactReuse],
                [HarnessEvaluationDimension.CostAttribution]),
            "h001-04" => Create(
                caseId,
                "The retained destination decision is Seattle. Call record_continuity.",
                "outputs/continuity.json",
                """{"destination":"seattle","constraintsPreserved":true}""",
                ["record_continuity"],
                [HarnessEvaluationDimension.Continuity],
                [HarnessEvaluationDimension.Latency]),
            "h001-05" => new HostedCaseDefinition(
                caseId,
                "Call wait_for_cancellation exactly once. Do not produce a success response.",
                string.Empty,
                string.Empty,
                ["wait_for_cancellation"],
                [],
                [
                    HarnessEvaluationDimension.Completion,
                    HarnessEvaluationDimension.Cancellation,
                    HarnessEvaluationDimension.Termination,
                ],
                [],
                ExpectsTimeout: true),
            "h001-06" => Create(
                caseId,
                "Call generate_large_artifact and then complete_hybrid_context.",
                "outputs/hybrid-context.json",
                """{"status":"complete","rehydrated":true}""",
                ["generate_large_artifact", "complete_hybrid_context"],
                [
                    HarnessEvaluationDimension.ContextSafety,
                    HarnessEvaluationDimension.ArtifactReuse,
                ]),
            "h001-07" => Create(
                caseId,
                "Call lookup_rate and then calculate_total.",
                "outputs/tool-cost.json",
                """{"status":"complete","total":42}""",
                ["lookup_rate", "calculate_total"],
                [HarnessEvaluationDimension.ToolTrajectory],
                [
                    HarnessEvaluationDimension.CostAttribution,
                    HarnessEvaluationDimension.CumulativeTokens,
                ]),
            "h001-08" => Create(
                caseId,
                "Call record_end_to_end and complete the approved delivery workflow.",
                "outputs/end-to-end.json",
                """{"status":"complete","decision":"approved","contextSafe":true}""",
                ["record_end_to_end"],
                [
                    HarnessEvaluationDimension.Continuity,
                    HarnessEvaluationDimension.ContextSafety,
                ],
                [
                    HarnessEvaluationDimension.Latency,
                    HarnessEvaluationDimension.PeakTokens,
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown hosted case."),
        };

    private static HostedCaseDefinition Create(
        string id,
        string prompt,
        string outputPath,
        string expectedOutput,
        IReadOnlyList<string> requiredTools,
        IReadOnlyList<HarnessEvaluationDimension> binaryDimensions,
        IReadOnlyList<HarnessEvaluationDimension>? continuousDimensions = null) =>
        new(
            id,
            $"[CASE_ID:{id}]\n{prompt}\nFinish only after the required workspace artifact exists.",
            outputPath,
            expectedOutput,
            requiredTools,
            ["delete_workspace", "write_outside_workspace", "lookup_rate_legacy"],
            [HarnessEvaluationDimension.Completion, .. binaryDimensions],
            continuousDimensions ?? [],
            ExpectsTimeout: false);
}
