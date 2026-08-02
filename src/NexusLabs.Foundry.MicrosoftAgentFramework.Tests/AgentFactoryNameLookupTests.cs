using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// Covers name-based agent lookup, which had no coverage and whose implementation contradicted its
/// documented contract: the map was keyed only on <see cref="Type.FullName"/> while the interface
/// promised simple class names.
/// </summary>
public sealed class AgentFactoryNameLookupTests
{
    [Fact]
    public void CreateAgent_SimpleClassName_Resolves()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<LookupWriterAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(nameof(LookupWriterAgent));

        Assert.Equal(nameof(LookupWriterAgent), agent.Name);
    }

    [Fact]
    public void CreateAgent_FullyQualifiedName_Resolves()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<LookupWriterAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(typeof(LookupWriterAgent).FullName!);

        Assert.Equal(nameof(LookupWriterAgent), agent.Name);
    }

    [Fact]
    public void CreateAgent_SimpleNameWithConfigureOverload_Resolves()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<LookupWriterAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(
            nameof(LookupWriterAgent),
            options => options.Instructions = "overridden");

        Assert.Equal(nameof(LookupWriterAgent), agent.Name);
    }

    [Fact]
    public void CreateAgent_UnregisteredName_ReportsHowToRegister()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<LookupWriterAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateAgent("NotRegisteredAgent"));

        Assert.Contains("NotRegisteredAgent", exception.Message, StringComparison.Ordinal);
        Assert.Contains("FoundryAgent", exception.Message, StringComparison.Ordinal);
    }

    /// <remarks>
    /// Two agents sharing a simple class name make that name ambiguous. Resolving it to whichever
    /// registered first, or dropping the alias so the name silently stops working, would both let a
    /// caller keep addressing an agent nobody intended, so composition fails instead.
    /// </remarks>
    [Fact]
    public void BuildAgentFactory_TwoAgentsShareASimpleName_FailsClosed()
    {
        using var provider = BuildProvider(builder => builder
            .AddAgent<LookupWriterAgent>()
            .AddAgent<Collisions.LookupWriterAgent>());

        var exception = Assert.Throws<InvalidOperationException>(
            provider.GetRequiredService<IAgentFactory>);

        Assert.Contains(nameof(LookupWriterAgent), exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            typeof(Collisions.LookupWriterAgent).FullName!,
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(
        Func<AgentFrameworkBuilder, AgentFrameworkBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddFoundryAgentFramework(builder =>
            configure(builder.UsingChatClient(new NameLookupChatClient())));
        return services.BuildServiceProvider();
    }

    private sealed class NameLookupChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Streaming is not required by these tests.");

        public object? GetService(Type serviceType, object? key) => null;

        public void Dispose()
        {
        }
    }
}
