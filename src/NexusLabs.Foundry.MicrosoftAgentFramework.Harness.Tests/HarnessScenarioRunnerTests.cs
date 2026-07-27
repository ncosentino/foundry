using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// Tests the reusable Harness scenario contract and deterministic session lifecycle.
/// </summary>
public sealed class HarnessScenarioRunnerTests
{
    [Fact]
    public void Constructor_WithoutScopeFactory_ThrowsInvalidOperationException()
    {
        using var services = CreateServices();
        var accessor = services.GetRequiredService<IAgentExecutionContextAccessor>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new HarnessScenarioRunner(
                new HarnessRunnerNoScopeServiceProvider(),
                accessor));

        Assert.Contains("IServiceScopeFactory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IHarnessScenario_ExtendsAgentScenarioWithHarnessLifecycleMembers()
    {
        Assert.Contains(typeof(IAgentScenario), typeof(IHarnessScenario).GetInterfaces());
        var members = typeof(IHarnessScenario)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(members, member => member.Name == "GeneratedFunctionTypes");
        Assert.Contains(members, member => member.Name == "CreateAgent");
        Assert.Contains(members, member => member.Name == "VerifyHarness");
    }

    [Fact]
    public async Task RunAsync_ResolvesGeneratedToolCreatesSessionAndExecutesHarnessLifecycle()
    {
        using var services = CreateServices();
        var accessor = services.GetRequiredService<IAgentExecutionContextAccessor>();
        var runner = new HarnessScenarioRunner(services, accessor);
        var scenario = new HarnessRunnerTestScenario(
            [typeof(HarnessBundleGeneratedTool)],
            failBaseVerification: false,
            failHarnessVerification: false);

        var result = await runner.RunAsync(
            scenario,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Session);
        Assert.Equal("bundle-complete", result.ResponseText);
        Assert.Equal(["Record"], result.ResolvedGeneratedToolNames);
        Assert.Equal(["Record"], result.ExecutedGeneratedToolNames);
        Assert.Equal("generated-value", scenario.ScopedCapture?.Value);
        Assert.True(result.Workspace.FileExists("seed.txt"));
        Assert.Equal(1, scenario.CreateAgentCallCount);
        Assert.NotNull(scenario.AgentContext);
        Assert.Same(result.Workspace, scenario.AgentContext!.Workspace);
        Assert.NotNull(scenario.VerificationContext);
        Assert.Same(result.Session, scenario.VerificationContext!.Session);
        Assert.Equal(result.SessionId, scenario.VerificationContext.SessionId);
    }

    [Fact]
    public async Task RunAsync_MissingGeneratedType_FailsBeforeAgentConstruction()
    {
        using var services = CreateServices();
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessRunnerTestScenario(
            [typeof(string)],
            failBaseVerification: false,
            failHarnessVerification: false);

        var exception = await Assert.ThrowsAsync<HarnessScenarioToolResolutionException>(
            () => runner.RunAsync(scenario, TestContext.Current.CancellationToken));

        Assert.Equal([typeof(string)], exception.MissingFunctionTypes);
        Assert.Empty(exception.DuplicateToolNames);
        Assert.Equal(0, scenario.CreateAgentCallCount);
    }

    [Fact]
    public async Task RunAsync_DuplicateGeneratedToolName_FailsBeforeAgentConstruction()
    {
        using var services = CreateServices();
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessRunnerTestScenario(
            [
                typeof(HarnessBundleGeneratedTool),
                typeof(HarnessBundleDuplicateGeneratedTool),
            ],
            failBaseVerification: false,
            failHarnessVerification: false);

        var exception = await Assert.ThrowsAsync<HarnessScenarioToolResolutionException>(
            () => runner.RunAsync(scenario, TestContext.Current.CancellationToken));

        Assert.Empty(exception.MissingFunctionTypes);
        Assert.Equal(["Record"], exception.DuplicateToolNames);
        Assert.Equal(0, scenario.CreateAgentCallCount);
    }

    [Fact]
    public async Task RunAsync_RepeatedGeneratedType_IsResolvedOnce()
    {
        using var services = CreateServices();
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessRunnerTestScenario(
            [
                typeof(HarnessBundleGeneratedTool),
                typeof(HarnessBundleGeneratedTool),
            ],
            failBaseVerification: false,
            failHarnessVerification: false);

        var result = await runner.RunAsync(
            scenario,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["Record"], result.ResolvedGeneratedToolNames);
        Assert.Equal(["Record"], result.ExecutedGeneratedToolNames);
    }

    [Fact]
    public async Task RunAsync_UsesPerRunServiceScopeForGeneratedTools()
    {
        using var services = CreateServices();
        var rootCapture = services.GetRequiredService<HarnessBundleGeneratedToolCapture>();
        rootCapture.Value = "root-poison";
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessRunnerTestScenario(
            [typeof(HarnessBundleGeneratedTool)],
            failBaseVerification: false,
            failHarnessVerification: false);

        var result = await runner.RunAsync(
            scenario,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("root-poison", rootCapture.Value);
        Assert.NotSame(rootCapture, scenario.ScopedCapture);
        Assert.Equal("generated-value", scenario.ScopedCapture?.Value);
    }

    [Fact]
    public async Task RunAsync_NullGeneratedType_ReportsOriginalIndex()
    {
        using var services = CreateServices();
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessRunnerTestScenario(
            [
                typeof(HarnessBundleGeneratedTool),
                typeof(HarnessBundleGeneratedTool),
                null!,
            ],
            failBaseVerification: false,
            failHarnessVerification: false);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(scenario, TestContext.Current.CancellationToken));

        Assert.Contains("index 2", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, scenario.CreateAgentCallCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RunAsync_VerificationFailure_IsReturned(
        bool failBaseVerification,
        bool failHarnessVerification)
    {
        using var services = CreateServices();
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessRunnerTestScenario(
            [typeof(HarnessBundleGeneratedTool)],
            failBaseVerification,
            failHarnessVerification);

        var result = await runner.RunAsync(
            scenario,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(failBaseVerification, result.VerificationError is not null);
        Assert.Equal(failHarnessVerification, result.HarnessVerificationError is not null);
    }

    private static ServiceProvider CreateServices() =>
        new ServiceCollection()
            .AddFoundryAgentFramework()
            .AddScoped<HarnessBundleGeneratedToolCapture>()
            .AddTransient<HarnessBundleGeneratedTool>()
            .AddTransient<HarnessBundleDuplicateGeneratedTool>()
            .BuildServiceProvider();
}
