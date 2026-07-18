using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Extension methods that register the small set of Foundry Agent Framework accessors
/// that tools and test harnesses depend on, without registering the full
/// <see cref="IAgentFactory"/> / <see cref="WorkflowFactory"/> / iterative-loop infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// Use this when you need the Foundry accessors (<see cref="IAgentExecutionContextAccessor"/>,
/// <see cref="IAgentDiagnosticsAccessor"/>, <see cref="IInFlightAgentDiagnosticsAccessor"/>,
/// <see cref="IAgentDiagnosticsWriter"/>) but do not want the rest of the Agent Framework
/// wiring — typically because you are constructing a minimal service provider for a
/// tool-level test.
/// </para>
/// <para>
/// <see cref="ServiceCollectionAgentFrameworkExtensions.AddFoundryAgentFramework(IServiceCollection)"/>
/// already registers these accessors
/// as part of its broader infrastructure setup. Calling
/// <see cref="AddAgentFrameworkAccessors"/> afterward is a no-op because every registration uses
/// <see cref="ServiceCollectionDescriptorExtensions.TryAdd(IServiceCollection, ServiceDescriptor)"/>.
/// </para>
/// </remarks>
public static class AgentFrameworkAccessorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Foundry Agent Framework accessor singletons:
    /// <see cref="IAgentExecutionContextAccessor"/>, <see cref="IAgentDiagnosticsAccessor"/>,
    /// <see cref="IInFlightAgentDiagnosticsAccessor"/>, and <see cref="IAgentDiagnosticsWriter"/>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>
    /// All registrations use <c>TryAddSingleton</c>, so calling this method is safe even if the
    /// accessors have already been registered by <c>UsingAgentFramework()</c> or another path.
    /// </remarks>
    public static IServiceCollection AddAgentFrameworkAccessors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IAgentExecutionContextAccessor, AgentExecutionContextAccessor>();
        services.TryAddSingleton<AgentDiagnosticsAccessor>(sp =>
            new AgentDiagnosticsAccessor(
                sp.GetService<ChatCompletionCollectorHolder>(),
                sp.GetService<ToolCallCollectorHolder>()));
        services.TryAddSingleton<IAgentDiagnosticsAccessor>(sp =>
            sp.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<IAgentDiagnosticsWriter>(sp =>
            sp.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<IInFlightAgentDiagnosticsAccessor, InFlightAgentDiagnosticsAccessor>();

        return services;
    }
}
