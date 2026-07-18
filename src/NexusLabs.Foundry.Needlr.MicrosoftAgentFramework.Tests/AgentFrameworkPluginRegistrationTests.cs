using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Budget;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Foundry.Needlr.MicrosoftAgentFramework.Tests;

/// <summary>
/// Proves that <see cref="IServiceCollectionPlugin"/> implementations can register
/// the agent framework via the <see cref="ServiceCollectionAgentFrameworkExtensions"/>
/// <c>AddFoundryAgentFramework</c> overloads and all infrastructure types resolve from
/// a real Syringe-built service provider. This is the end-to-end proof that the
/// plugin-based registration path works with real source generation, and that any
/// configuration supplied by the plugin's configure delegate flows through to the
/// constructed services.
/// </summary>
public sealed class AgentFrameworkPluginRegistrationTests
{
    [Fact]
    public void Plugin_RegistersAgentFramework_AllTypesResolve()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                Generated.TypeRegistry.GetInjectableTypes,
                Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        Assert.NotNull(provider.GetService<IAgentFactory>());
        Assert.NotNull(provider.GetService<IWorkflowFactory>());
        Assert.NotNull(provider.GetService<ITokenBudgetTracker>());
        Assert.NotNull(provider.GetService<IAgentDiagnosticsAccessor>());
        Assert.NotNull(provider.GetService<IToolMetricsAccessor>());
    }

    /// <summary>
    /// Asserts that configuration supplied to <c>AddFoundryAgentFramework(configure)</c>
    /// from inside an <see cref="IServiceCollectionPlugin"/> survives the full
    /// source-gen registration pipeline. Specifically, the configured meter name
    /// supplied by <see cref="AgentFrameworkRegistrationPlugin"/> via
    /// <c>ConfigureMetrics</c> must end up on the resolved <see cref="IAgentMetrics"/>
    /// activity source — proving the configure delegate ran, the
    /// <see cref="AgentFrameworkBuilder"/> it produced was used to build the
    /// <see cref="IAgentMetrics"/>, and the source-gen pipeline did not silently
    /// discard the user-supplied configuration when merging in generated
    /// function/group/agent types.
    /// </summary>
    [Fact]
    public void Plugin_RegistersAgentFrameworkWithConfigure_MetricsConfigurationFlowsThrough()
    {
        var provider = new Syringe()
            .UsingGeneratedComponents(
                Generated.TypeRegistry.GetInjectableTypes,
                Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider();

        var metrics = provider.GetRequiredService<IAgentMetrics>();
        Assert.Equal(
            AgentFrameworkRegistrationPlugin.ConfiguredMeterName,
            metrics.ActivitySource.Name);
    }
}
