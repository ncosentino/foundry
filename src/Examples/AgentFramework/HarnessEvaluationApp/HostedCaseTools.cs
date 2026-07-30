using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace HarnessEvaluationApp;

internal static class HostedCaseTools
{
    internal static IReadOnlyList<AIFunction> Create(
        IWorkspace workspace,
        HostedToolCallRecorder recorder,
        TimeSpan cancellationDelay)
    {
        return
        [
            Create("lookup_north", () => "17", recorder),
            Create("lookup_south", () => "29", recorder),
            Create(
                "combine_values",
                () => Write(
                    workspace,
                    "outputs/tool-orchestration.json",
                    """{"status":"complete","result":"north:17|south:29"}"""),
                recorder),
            Create("read_context", () => "CONTEXT-SENTINEL-7F3A", recorder),
            Create(
                "save_long_context_answer",
                () => Write(workspace, "outputs/long-context-answer.txt", "CONTEXT-SENTINEL-7F3A"),
                recorder),
            Create("generate_large_artifact", () => new string('x', 12_000), recorder),
            Create(
                "create_artifact_summary",
                () => Write(
                    workspace,
                    "outputs/artifact-summary.json",
                    """{"status":"complete","reusedExistingReference":true}"""),
                recorder),
            Create(
                "record_continuity",
                () => Write(
                    workspace,
                    "outputs/continuity.json",
                    """{"destination":"seattle","constraintsPreserved":true}"""),
                recorder),
            Create(
                "complete_hybrid_context",
                () => Write(
                    workspace,
                    "outputs/hybrid-context.json",
                    """{"status":"complete","rehydrated":true}"""),
                recorder),
            Create("lookup_rate", () => "6", recorder),
            Create(
                "calculate_total",
                () => Write(
                    workspace,
                    "outputs/tool-cost.json",
                    """{"status":"complete","total":42}"""),
                recorder),
            Create(
                "record_end_to_end",
                () => Write(
                    workspace,
                    "outputs/end-to-end.json",
                    """{"status":"complete","decision":"approved","contextSafe":true}"""),
                recorder),
            AIFunctionFactory.Create(
                async (CancellationToken cancellationToken) =>
                {
                    recorder.Record("wait_for_cancellation");
                    await Task.Delay(cancellationDelay, cancellationToken).ConfigureAwait(false);
                    return "unexpected-completion";
                },
                new AIFunctionFactoryOptions
                {
                    Name = "wait_for_cancellation",
                    Description = "Waits until the attempt cancellation token terminates the call.",
                }),
        ];
    }

    private static AIFunction Create(
        string name,
        Func<string> callback,
        HostedToolCallRecorder recorder) =>
        AIFunctionFactory.Create(
            () =>
            {
                recorder.Record(name);
                return callback();
            },
            new AIFunctionFactoryOptions
            {
                Name = name,
                Description = $"Deterministic hosted evaluation tool '{name}'.",
            });

    private static string Write(IWorkspace workspace, string path, string content)
    {
        var result = workspace.TryWriteFile(path, content);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Exception?.Message ?? $"Workspace write failed for '{path}'.");
        }

        return path;
    }
}
