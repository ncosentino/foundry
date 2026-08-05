using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

#pragma warning disable MEAI001

[AgentFunctionGroup("e2e-blank-published-name")]
public sealed class E2EBlankPublishedNameTool
{
    [AgentFunction]
    [AIFunctionName("")]
    public string Run(string value) => value;
}

#pragma warning restore MEAI001
