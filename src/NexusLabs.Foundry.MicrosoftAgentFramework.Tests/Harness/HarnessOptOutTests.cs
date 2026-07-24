using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessOptOutTests
{
    [Fact]
    public async Task OrdinaryAgentFactoryPath_RemainsRunnableWithoutHarnessComposition()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(client => client.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "ordinary")));
        var config = new ConfigurationBuilder().Build();
        var services = new Syringe()
            .UsingReflection()
            .UsingAgentFramework(builder =>
                builder.Configure(options =>
                    options.ChatClientFactory = _ => chatClient.Object))
            .BuildServiceProvider(config);
        var factory = services.GetRequiredService<IAgentFactory>();

        var agent = factory.CreateAgent(options => options.Name = "ordinary");
        var response = await agent.RunAsync(
                "hello",
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ordinary", response.GetText());
    }
}
