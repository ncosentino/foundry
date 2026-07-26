using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Proves that source-generated Foundry functions enter the optional upstream Harness bundle
/// through the explicit <see cref="FoundryHarnessAgentConfiguration.Tools"/> boundary.
/// </summary>
public sealed class HarnessBundleGeneratedToolsTests
{
    private static readonly FoundryHarnessAgentFactory Factory = new();

    [Fact]
    public void GeneratedProvider_ResolvesRealAIFunctionWithoutReflectionDiscovery()
    {
        using var services = CreateServices();

        var functions = ResolveFunctions<HarnessBundleGeneratedTool>(services);

        var function = Assert.Single(functions);
        Assert.Equal("Record", function.Name);
    }

    [Fact]
    public async Task Run_GeneratedFunction_EntersBundleAndExecutes()
    {
        using var services = CreateServices();
        var capture = services.GetRequiredService<HarnessBundleGeneratedToolCapture>();
        var functions = ResolveFunctions<HarnessBundleGeneratedTool>(services);
        var chatClient = new HarnessBundleToolCallChatClient(
            "Record",
            new Dictionary<string, object?>
            {
                ["value"] = "generated-value",
            });
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            ChatClient = chatClient,
            Tools = [.. functions],
        };
        var agent = Factory.Create(configuration, services);

        var response = await agent.RunAsync(
            "record the generated value",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("generated-value", capture.Value);
        Assert.Equal(2, chatClient.CallCount);
        Assert.Equal("bundle-complete", response.GetText());
        var ingressTool = Assert.Single(chatClient.FirstCallOptions?.Tools ?? []);
        Assert.Same(functions[0], ingressTool);
    }

    [Fact]
    public void Create_DuplicateGeneratedFunctionNames_ThrowsArgumentException()
    {
        using var services = CreateServices();
        var functions = ResolveFunctions<HarnessBundleGeneratedTool>(services)
            .Concat(ResolveFunctions<HarnessBundleDuplicateGeneratedTool>(services))
            .Cast<AITool>()
            .ToArray();
        var configuration = HarnessBundleTestsHelpers.CreateBaseline() with
        {
            Tools = functions,
        };

        var exception = Assert.Throws<ArgumentException>(() => Factory.Create(configuration));

        Assert.Contains("Record", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_GeneratedFunctionCollidingWithBuiltInTool_ThrowsArgumentException()
    {
        using var services = CreateServices();
        var functions = ResolveFunctions<HarnessBundleWebSearchGeneratedTool>(services);
        var configuration = HarnessBundleTestsHelpers.CreateBaseline(
            HarnessBundleTestsHelpers.AllFeaturesDisabled() with { EnableWebSearch = true }) with
        {
            Tools = [.. functions],
        };

        var exception = Assert.Throws<ArgumentException>(() => Factory.Create(configuration));

        Assert.Contains("web_search", exception.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<AIFunction> ResolveFunctions<TFunction>(
        IServiceProvider services)
    {
        Assert.True(AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider));
        Assert.True(provider.TryGetFunctions(typeof(TFunction), services, out var functions));
        return functions;
    }

    private static ServiceProvider CreateServices() =>
        new ServiceCollection()
            .AddSingleton<HarnessBundleGeneratedToolCapture>()
            .AddTransient<HarnessBundleGeneratedTool>()
            .AddTransient<HarnessBundleDuplicateGeneratedTool>()
            .AddTransient<HarnessBundleWebSearchGeneratedTool>()
            .BuildServiceProvider();
}
