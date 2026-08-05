using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

#pragma warning disable MEAI001

[AgentFunctionGroup("e2e-blank-published-parameter")]
public sealed class E2EBlankPublishedParameterTool
{
    [AgentFunction]
    public string Run(
        [AIParameterName("")] string value) =>
        value;
}

#pragma warning restore MEAI001
