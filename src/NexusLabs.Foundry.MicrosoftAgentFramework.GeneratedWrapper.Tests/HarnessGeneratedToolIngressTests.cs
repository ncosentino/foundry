using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.GeneratedWrapper.Tests;

public sealed class HarnessGeneratedToolIngressTests
{
    [Fact]
    public void Resolve_KnownTypes_ReturnsGeneratedFunctionsInTypeOrder()
    {
        using var services = CreateServices();
        var source = CreateSource(services);

        var result = source.Resolve(
            [typeof(E2EStringTool), typeof(E2EIntTool)]);

        Assert.Equal(HarnessGeneratedToolResolutionStatus.Success, result.Status);
        Assert.Equal(["Record", "SetMax"], result.Functions.Select(function => function.Name));
        Assert.Empty(result.MissingFunctionTypes);
        Assert.Empty(result.DuplicateToolNames);
    }

    [Fact]
    public void Resolve_EmptyTypes_ReturnsSuccessfulEmptyResult()
    {
        using var services = CreateServices();
        var source = CreateSource(services);

        var result = source.Resolve([]);

        Assert.Equal(HarnessGeneratedToolResolutionStatus.Success, result.Status);
        Assert.Empty(result.Functions);
    }

    [Fact]
    public void Resolve_MultiFunctionType_PreservesGeneratedProviderOrder()
    {
        using var services = CreateServices();
        var source = CreateSource(services);

        var result = source.Resolve([typeof(E2EDefaultedTemporalsTool)]);

        Assert.Equal(HarnessGeneratedToolResolutionStatus.Success, result.Status);
        Assert.Equal(
            [
                "RecordGuid",
                "RecordDateTime",
                "RecordDateTimeOffset",
                "RecordTimeSpan",
                "RecordDecimal",
            ],
            result.Functions.Select(function => function.Name));
    }

    [Fact]
    public void Resolve_SuccessfulResult_PopulatesChatOptionsTools()
    {
        using var services = CreateServices();
        var source = CreateSource(services);

        var result = source.Resolve([typeof(E2EStringTool)]);
        var options = new ChatOptions
        {
            Tools = [.. result.Functions],
        };

        var tool = Assert.Single(options.Tools);
        Assert.Same(result.Functions[0], tool);
    }

    [Fact]
    public void Resolve_MissingGeneratedType_FailsClosedWithoutPartialFunctions()
    {
        using var services = CreateServices();
        var source = CreateSource(services);

        var result = source.Resolve(
            [typeof(E2EStringTool), typeof(string)]);

        Assert.Equal(
            HarnessGeneratedToolResolutionStatus.MissingGeneratedFunctionType,
            result.Status);
        Assert.Empty(result.Functions);
        Assert.Equal([typeof(string)], result.MissingFunctionTypes);
    }

    [Fact]
    public void Resolve_DuplicateToolNames_FailsClosed()
    {
        using var services = CreateServices();
        var source = CreateSource(services);

        var result = source.Resolve(
            [typeof(E2EStringTool), typeof(E2EDtoTool)]);

        Assert.Equal(
            HarnessGeneratedToolResolutionStatus.DuplicateToolName,
            result.Status);
        Assert.Empty(result.Functions);
        Assert.Equal(["Record"], result.DuplicateToolNames);
    }

    private static HarnessGeneratedToolSource CreateSource(
        IServiceProvider services)
    {
        Assert.True(
            AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(
                out var provider));
        return new HarnessGeneratedToolSource(provider, services);
    }

    private static ServiceProvider CreateServices() =>
        new ServiceCollection()
            .AddSingleton<E2EStringTool.Capture>()
            .AddTransient<E2EStringTool>()
            .AddSingleton<E2EIntTool.Capture>()
            .AddTransient<E2EIntTool>()
            .AddSingleton<E2EDtoTool.Capture>()
            .AddTransient<E2EDtoTool>()
            .AddSingleton<E2EDefaultedTemporalsTool.Capture>()
            .AddTransient<E2EDefaultedTemporalsTool>()
            .BuildServiceProvider();
}
