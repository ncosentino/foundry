using System.Reflection;
using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

/// <summary>
/// Proves that the Harness generated-tool ingress preserves generated wrapper identity,
/// metadata, and invocation behavior.
/// </summary>
public sealed class HarnessGeneratedWrapperParityTests
{
    [Fact]
    public void Resolve_MatchesDirectGeneratedProviderMetadataAndOrder()
    {
        var capture = new E2EDefaultedTemporalsTool.Capture();
        using var services = new ServiceCollection()
            .AddSingleton(capture)
            .AddTransient<E2EDefaultedTemporalsTool>()
            .BuildServiceProvider();
        var provider = GetGeneratedProvider();
        Assert.True(provider.TryGetFunctions(
            typeof(E2EDefaultedTemporalsTool),
            services,
            out var directFunctions));
        var source = new HarnessGeneratedToolSource(provider, services);

        var resolution = source.Resolve([typeof(E2EDefaultedTemporalsTool)]);

        Assert.Equal(HarnessGeneratedToolResolutionStatus.Success, resolution.Status);
        Assert.Equal(directFunctions.Count, resolution.Functions.Count);
        for (int index = 0; index < directFunctions.Count; index++)
        {
            Assert.Equal(directFunctions[index].Name, resolution.Functions[index].Name);
            Assert.Equal(directFunctions[index].Description, resolution.Functions[index].Description);
            Assert.True(JsonElement.DeepEquals(
                directFunctions[index].JsonSchema,
                resolution.Functions[index].JsonSchema));
        }
    }

    [Fact]
    public void GeneratedFunction_MatchesReflectionMetadata()
    {
        var generatedCapture = new E2EStringTool.Capture();
        using var services = new ServiceCollection()
            .AddSingleton(generatedCapture)
            .AddTransient<E2EStringTool>()
            .BuildServiceProvider();
        var provider = GetGeneratedProvider();
        var source = new HarnessGeneratedToolSource(provider, services);
        var generated = Assert.Single(
            source.Resolve([typeof(E2EStringTool)]).Functions);
        var method = typeof(E2EStringTool).GetMethod(
            nameof(E2EStringTool.Record),
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("The representative method was unavailable.");
        var reflected = AIFunctionFactory.Create(
            method,
            target: new E2EStringTool(new E2EStringTool.Capture()));

        Assert.Equal(reflected.Name, generated.Name);
        Assert.Equal(reflected.Description, generated.Description);
        Assert.True(JsonElement.DeepEquals(
            reflected.JsonSchema,
            generated.JsonSchema));
    }

    [Fact]
    public async Task HarnessResolution_MatchesDirectGeneratedInvocationBehavior()
    {
        var harnessCapture = new E2EStringTool.Capture();
        using var harnessServices = new ServiceCollection()
            .AddSingleton(harnessCapture)
            .AddTransient<E2EStringTool>()
            .BuildServiceProvider();
        var provider = GetGeneratedProvider();
        var source = new HarnessGeneratedToolSource(provider, harnessServices);
        var harnessFunction = Assert.Single(
            source.Resolve([typeof(E2EStringTool)]).Functions);
        var directCapture = new E2EStringTool.Capture();
        using var directServices = new ServiceCollection()
            .AddSingleton(directCapture)
            .AddTransient<E2EStringTool>()
            .BuildServiceProvider();
        Assert.True(provider.TryGetFunctions(
            typeof(E2EStringTool),
            directServices,
            out var directFunctions));
        var directFunction = Assert.Single(directFunctions);
        var harnessArguments = new AIFunctionArguments
        {
            ["findingsJson"] = Parse("[{\"severity\":\"Warning\"}]"),
        };
        var directArguments = new AIFunctionArguments
        {
            ["findingsJson"] = Parse("[{\"severity\":\"Warning\"}]"),
        };

        var harnessResult = await harnessFunction.InvokeAsync(
            harnessArguments,
            TestContext.Current.CancellationToken);
        var directResult = await directFunction.InvokeAsync(
            directArguments,
            TestContext.Current.CancellationToken);

        Assert.Equal(directResult, harnessResult);
        Assert.Equal(directCapture.Value, harnessCapture.Value);
    }

    private static IAIFunctionProvider GetGeneratedProvider()
    {
        Assert.True(AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var provider));
        return provider;
    }

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();
}
