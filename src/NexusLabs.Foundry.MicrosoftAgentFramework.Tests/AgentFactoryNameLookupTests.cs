using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// Covers the name an agent is published under: the key <see cref="IAgentFactory.CreateAgent(string)"/>
/// resolves and the value of <see cref="AIAgent.Name"/>. The lookup once had no coverage and
/// contradicted its documented contract, keying only on <see cref="Type.FullName"/> while the
/// interface promised simple class names.
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

    [Fact]
    public void CreateAgent_DeclaredName_Resolves()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<NamedEditorAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent("PublishedEditor");

        Assert.Equal("PublishedEditor", agent.Name);
    }

    /// <remarks>
    /// The published name is the agent's identity, not an additional alias beside the class name.
    /// Keeping the class name addressable would leave the rename that declaring a name exists to
    /// survive still able to break a caller, so it stops resolving.
    /// </remarks>
    [Fact]
    public void CreateAgent_ClassNameOfAnAgentThatDeclaresAName_DoesNotResolve()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<NamedEditorAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateAgent(nameof(NamedEditorAgent)));

        Assert.Contains(nameof(NamedEditorAgent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("PublishedEditor", exception.Message, StringComparison.Ordinal);
    }

    /// <remarks>
    /// The fully-qualified name is the caller's fallback, so declaring a name must not take it away.
    /// </remarks>
    [Fact]
    public void CreateAgent_FullyQualifiedNameOfAnAgentThatDeclaresAName_StillResolves()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<NamedEditorAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(typeof(NamedEditorAgent).FullName!);

        Assert.Equal("PublishedEditor", agent.Name);
    }

    /// <remarks>
    /// <see cref="AIAgent.Name"/> becomes the author of every message the agent produces and the
    /// <c>gen_ai.agent.name</c> telemetry dimension, so it has to follow the declared name rather
    /// than staying on the class name.
    /// </remarks>
    [Fact]
    public void CreateAgent_DeclaredName_BecomesTheAgentName()
    {
        using var provider = BuildProvider(builder => builder.AddAgent<NamedEditorAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent<NamedEditorAgent>();

        Assert.Equal("PublishedEditor", agent.Name);
    }

    [Fact]
    public void BuildAgentFactory_TwoAgentsDeclareTheSameName_FailsClosed()
    {
        using var provider = BuildProvider(builder => builder
            .AddAgent<NamedEditorAgent>()
            .AddAgent<RivalEditorAgent>());

        var exception = Assert.Throws<InvalidOperationException>(
            provider.GetRequiredService<IAgentFactory>);

        Assert.Contains("PublishedEditor", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            typeof(RivalEditorAgent).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            typeof(NamedEditorAgent).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <remarks>
    /// Only one of the two declarations mentions the shared name here, so this is the collision a
    /// reader is least likely to spot. The message has to say which side declared it.
    /// </remarks>
    [Fact]
    public void BuildAgentFactory_DeclaredNameCollidesWithAnotherClassName_FailsClosed()
    {
        using var provider = BuildProvider(builder => builder
            .AddAgent<LookupWriterAgent>()
            .AddAgent<ImpersonatingWriterAgent>());

        var exception = Assert.Throws<InvalidOperationException>(
            provider.GetRequiredService<IAgentFactory>);

        Assert.Contains(nameof(LookupWriterAgent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("declared as", exception.Message, StringComparison.Ordinal);
        Assert.Contains("from its class name", exception.Message, StringComparison.Ordinal);
    }

    /// <remarks>
    /// A failed lookup is most often a spelling or a registration problem, and both are answered by
    /// showing what is actually registered.
    /// </remarks>
    [Fact]
    public void CreateAgent_UnregisteredName_ListsTheRegisteredNames()
    {
        using var provider = BuildProvider(builder => builder
            .AddAgent<LookupWriterAgent>()
            .AddAgent<NamedEditorAgent>());
        var factory = provider.GetRequiredService<IAgentFactory>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateAgent("NotRegisteredAgent"));

        Assert.Contains(nameof(LookupWriterAgent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("PublishedEditor", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(LookupWriterAgent).FullName!, exception.Message, StringComparison.Ordinal);
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
