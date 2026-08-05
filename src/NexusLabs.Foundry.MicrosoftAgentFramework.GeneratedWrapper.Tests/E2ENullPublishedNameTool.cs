using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

#pragma warning disable CS8625
#pragma warning disable MEAI001

[AgentFunctionGroup("e2e-null-published-name")]
public sealed class E2ENullPublishedNameTool
{
    [AgentFunction]
    [AIFunctionName(null)]
    public string Run(string value) => value;
}

#pragma warning restore MEAI001
#pragma warning restore CS8625
