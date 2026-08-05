using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

#pragma warning disable MEAI001

[AgentFunctionGroup("e2e-duplicate-published-parameter")]
public sealed class E2EDuplicatePublishedParameterTool
{
    [AgentFunction]
    public string Run(
        [AIParameterName("same")] string first,
        [AIParameterName("same")] string second) =>
        first + second;
}

#pragma warning restore MEAI001
