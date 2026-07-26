using System.ComponentModel;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace HarnessHybridApp;

[AgentFunctionGroup("harness-hybrid")]
internal sealed class HybridWorkspaceTool(
    IAgentExecutionContextAccessor contextAccessor)
{
    internal const string OutputPath = "selected-provider/proof.txt";
    internal const string ExpectedContent = "hybrid-proof";

    [AgentFunction]
    [Description("Writes a deterministic proof value to the authorized workspace.")]
    public string WriteProof()
    {
        var context = contextAccessor.Current
            ?? throw new InvalidOperationException("No trusted execution context is active.");
        var workspace = context.GetWorkspace()
            ?? throw new InvalidOperationException("No authorized workspace is active.");
        var write = workspace.TryWriteFile(OutputPath, ExpectedContent);
        if (!write.Success)
        {
            throw new InvalidOperationException(
                "The selected-provider workspace write failed.",
                write.Exception);
        }

        return $"stored:{ExpectedContent}";
    }
}
