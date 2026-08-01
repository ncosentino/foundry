using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

public sealed class DeclarativeWorkflowAgentRegistryTests
{
    [Fact]
    public void TryGetAgent_NameDifferingOnlyByCase_Resolves()
    {
        var registry = new DeclarativeWorkflowAgentRegistry(
            new Dictionary<string, AIAgent> { ["Writer"] = CreateAgent() });

        Assert.True(registry.TryGetAgent("wRiTeR", out var agent));
        Assert.NotNull(agent);
    }

    [Fact]
    public void TryGetAgent_UnregisteredName_ReturnsFalse()
    {
        var registry = new DeclarativeWorkflowAgentRegistry(
            new Dictionary<string, AIAgent> { ["Writer"] = CreateAgent() });

        Assert.False(registry.TryGetAgent("Reviewer", out var agent));
        Assert.Null(agent);
    }

    [Fact]
    public void Constructor_NamesDifferingOnlyByCase_FailsClosed()
    {
        var agents = new Dictionary<string, AIAgent>(StringComparer.Ordinal)
        {
            ["Writer"] = CreateAgent(),
            ["writer"] = CreateAgent(),
        };

        var exception = Assert.Throws<ArgumentException>(
            () => new DeclarativeWorkflowAgentRegistry(agents));
        Assert.Contains("case", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_EmptyName_FailsClosed()
    {
        var agents = new Dictionary<string, AIAgent> { ["  "] = CreateAgent() };

        Assert.Throws<ArgumentException>(() => new DeclarativeWorkflowAgentRegistry(agents));
    }

    private static AIAgent CreateAgent() =>
        new ChatClientAgent(new ScriptedDeclarativeChatClient("x:"), name: "agent");
}
