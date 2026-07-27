using System.ComponentModel;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace AotHarnessApp;

[AgentFunctionGroup("aot-harness")]
internal sealed class AotHarnessTool(
    IAgentExecutionContextAccessor contextAccessor)
{
    [AgentFunction]
    [Description("Writes the supplied value to the scenario workspace.")]
    public string WriteWorkspace(
        [Description("The value to persist.")] string value)
    {
        var context = contextAccessor.Current
            ?? throw new InvalidOperationException("No execution context is active.");
        var workspace = context.GetWorkspace()
            ?? throw new InvalidOperationException("No workspace is authorized.");
        var write = workspace.TryWriteFile(
            AotHarnessScenario.OutputPath,
            value);
        if (!write.Success)
        {
            throw new InvalidOperationException(
                "The workspace write failed.",
                write.Exception);
        }

        return $"written:{value}";
    }
}
