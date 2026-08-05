using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

/// <summary>
/// Covers MEAI's published function contract on Foundry's reflection path and the runtime guard
/// that checks the actual set of tools an agent resolves.
/// </summary>
public sealed class AgentFunctionPublishedNameTests
{
    [Fact]
    public void ResolveTools_ReflectionPath_HonorsPublishedFunctionAndParameterNames()
    {
        using var provider = BuildProvider(
            typeof(PublishedSupportSearchTools));
        var factory = provider.GetRequiredService<IAgentFactory>();

        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(factory.ResolveTools()));

        Assert.Equal("shared_search", function.Name);
        Assert.True(function.JsonSchema
            .GetProperty("properties")
            .TryGetProperty("search_query", out _));
        Assert.False(function.JsonSchema
            .GetProperty("properties")
            .TryGetProperty("query", out _));
    }

    /// <remarks>
    /// Same-named tools in separate types are valid while an agent resolves only one of those types.
    /// A compilation-wide analyzer would reject this legitimate scoping, so cross-type collisions
    /// are checked against the actual resolved set instead.
    /// </remarks>
    [Fact]
    public void ResolveTools_OneOfTwoSameNamedFunctionTypes_Resolves()
    {
        var factory = CreateFactoryWithDuplicateGeneratedNames();

        var tools = factory.ResolveTools(options =>
            options.FunctionTypes = [typeof(PublishedSupportSearchTools)]);

        Assert.Equal(
            "shared_search",
            Assert.IsAssignableFrom<AIFunction>(Assert.Single(tools)).Name);
    }

    [Fact]
    public void ResolveTools_TwoTypesPublishTheSameName_FailsClosed()
    {
        var factory = CreateFactoryWithDuplicateGeneratedNames();

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.ResolveTools());

        Assert.Contains("shared_search", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            typeof(PublishedSupportSearchTools).FullName!,
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            typeof(BillingFunctionType).FullName!,
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(params Type[] functionTypes)
    {
        var services = new ServiceCollection();
        services.AddFoundryAgentFramework(builder => builder
            .UsingChatClient(new NoOpChatClient())
            .AddAgentFunctions(functionTypes));
        return services.BuildServiceProvider();
    }

    private static IAgentFactory CreateFactoryWithDuplicateGeneratedNames()
    {
        IReadOnlyList<AIFunction>? supportFunctions =
        [
            AIFunctionFactory.Create(() => "support", name: "shared_search"),
        ];
        IReadOnlyList<AIFunction>? billingFunctions =
        [
            AIFunctionFactory.Create(() => "billing", name: "shared_search"),
        ];

        var generatedProvider = new Mock<IAIFunctionProvider>();
        generatedProvider
            .Setup(provider => provider.TryGetFunctions(
                typeof(PublishedSupportSearchTools),
                It.IsAny<IServiceProvider>(),
                out supportFunctions))
            .Returns(true);
        generatedProvider
            .Setup(provider => provider.TryGetFunctions(
                typeof(BillingFunctionType),
                It.IsAny<IServiceProvider>(),
                out billingFunctions))
            .Returns(true);

        return new AgentFactory(
            new ServiceCollection().BuildServiceProvider(),
            [],
            [typeof(PublishedSupportSearchTools), typeof(BillingFunctionType)],
            generatedProvider: generatedProvider.Object);
    }

    private sealed class NoOpChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? key) => null;

        public void Dispose()
        {
        }
    }

    private sealed class BillingFunctionType
    {
    }
}
