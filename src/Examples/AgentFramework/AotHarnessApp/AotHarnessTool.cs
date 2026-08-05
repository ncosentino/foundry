using System.ComponentModel;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace AotHarnessApp;

#pragma warning disable MEAI001

[AgentFunctionGroup("aot-harness")]
internal sealed class AotHarnessTool(
    IAgentExecutionContextAccessor contextAccessor)
{
    [AgentFunction]
    [AIFunctionName("write_workspace")]
    [Description("Writes the supplied value to the scenario workspace.")]
    public string WriteWorkspace(
        [AIParameterName("proof_value")]
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

#pragma warning restore MEAI001
