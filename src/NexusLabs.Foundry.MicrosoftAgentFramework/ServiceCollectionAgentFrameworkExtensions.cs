using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Budget;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Iterative;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

/// <summary>
/// Registers Foundry's Microsoft Agent Framework runtime with a service collection.
/// </summary>
public static class ServiceCollectionAgentFrameworkExtensions
{
    /// <summary>
    /// Registers the Microsoft Agent Framework runtime with default configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundryAgentFramework(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterAgentFrameworkCore(services, configure: null);
        return services;
    }

    /// <summary>
    /// Registers the Microsoft Agent Framework runtime with explicit builder configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">
    /// A function that receives and returns the runtime builder.
    /// </param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundryAgentFramework(
        this IServiceCollection services,
        Func<AgentFrameworkBuilder, AgentFrameworkBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        RegisterAgentFrameworkCore(services, configure);
        return services;
    }

    /// <summary>
    /// Registers the Microsoft Agent Framework runtime using a caller-created builder.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">A function that creates the configured builder.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundryAgentFramework(
        this IServiceCollection services,
        Func<AgentFrameworkBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        return services.AddFoundryAgentFramework(_ => configure());
    }

    private static void RegisterAgentFrameworkCore(
        IServiceCollection services,
        Func<AgentFrameworkBuilder, AgentFrameworkBuilder>? configure)
    {
        RegisterAgentFrameworkInfrastructure(services);
        configure ??= static builder => builder;

        services.TryAddSingleton<BuiltAgentFrameworkBuilder>(serviceProvider =>
        {
            var builder = configure(new AgentFrameworkBuilder
            {
                ServiceProvider = serviceProvider,
            });

            if ((builder.FunctionTypes is null || builder.FunctionTypes.Count == 0)
                && AgentFrameworkGeneratedBootstrap.TryGetFunctionTypes(out var functionProvider))
            {
                builder = builder with { FunctionTypes = functionProvider().ToList() };
            }

            if ((builder.FunctionGroupMap is null || builder.FunctionGroupMap.Count == 0)
                && AgentFrameworkGeneratedBootstrap.TryGetGroupTypes(out var groupProvider))
            {
                builder = builder with { FunctionGroupMap = groupProvider() };
            }

            if ((builder.AgentTypes is null || builder.AgentTypes.Count == 0)
                && AgentFrameworkGeneratedBootstrap.TryGetAgentTypes(out var agentProvider))
            {
                builder = builder with { AgentTypes = agentProvider().ToList() };
            }

            return new BuiltAgentFrameworkBuilder(builder);
        });

        services.TryAddSingleton<IProgressReporterFactory>(serviceProvider =>
        {
            var defaultSinks = serviceProvider.GetServices<IProgressSink>();
            return new ProgressReporterFactory(
                defaultSinks,
                serviceProvider.GetRequiredService<IProgressSequence>(),
                serviceProvider.GetRequiredService<IProgressReporterErrorHandler>());
        });

        services.TryAddSingleton<IAgentFactory>(serviceProvider =>
            serviceProvider
                .GetRequiredService<BuiltAgentFrameworkBuilder>()
                .Value
                .BuildAgentFactory());

        services.TryAddSingleton<IChatClientAccessor>(serviceProvider =>
        {
            var builder = serviceProvider
                .GetRequiredService<BuiltAgentFrameworkBuilder>()
                .Value;
            return new ChatClientAccessor(
                serviceProvider,
                builder.ConfigureAgentFactory ?? []);
        });

        services.TryAddSingleton<IIterativeAgentLoop>(serviceProvider =>
            new IterativeAgentLoop(
                serviceProvider.GetRequiredService<IChatClientAccessor>(),
                serviceProvider.GetService<IAgentDiagnosticsWriter>(),
                serviceProvider.GetService<IAgentExecutionContextAccessor>(),
                serviceProvider.GetService<IProgressReporterAccessor>(),
                serviceProvider.GetService<ITokenBudgetTracker>(),
                serviceProvider.GetService<IAgentMetrics>(),
                genAiTokenMetrics:
                    serviceProvider.GetService<IGenAiTokenMetrics>()));

        services.TryAddSingleton<IWorkflowFactory>(serviceProvider =>
            new WorkflowFactory(serviceProvider.GetRequiredService<IAgentFactory>()));
    }

    private static void RegisterAgentFrameworkInfrastructure(
        IServiceCollection services)
    {
        services.TryAddSingleton<ITokenBudgetTracker, TokenBudgetTracker>();
        services.TryAddSingleton<
            IAgentExecutionContextAccessor,
            AgentExecutionContextAccessor>();
        services.TryAddSingleton<AgentDiagnosticsAccessor>(serviceProvider =>
            new AgentDiagnosticsAccessor(
                serviceProvider.GetService<ChatCompletionCollectorHolder>(),
                serviceProvider.GetService<ToolCallCollectorHolder>()));
        services.TryAddSingleton<IAgentDiagnosticsAccessor>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<IAgentDiagnosticsWriter>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentDiagnosticsAccessor>());
        services.TryAddSingleton<
            IInFlightAgentDiagnosticsAccessor,
            InFlightAgentDiagnosticsAccessor>();
        services.TryAddSingleton<IToolMetricsAccessor, ToolMetricsAccessor>();
        services.TryAddSingleton<AgentFrameworkMetricsOptions>(serviceProvider =>
            serviceProvider.GetService<BuiltAgentFrameworkBuilder>()?
                .Value
                .MetricsOptions
            ?? new AgentFrameworkMetricsOptions());
        services.TryAddSingleton<IAgentMetrics>(serviceProvider =>
        {
            var options = serviceProvider.GetService<BuiltAgentFrameworkBuilder>()?
                .Value
                .MetricsOptions
                ?? new AgentFrameworkMetricsOptions();
            return new AgentMetrics(options);
        });
        services.TryAddSingleton<IGenAiTokenMetrics>(serviceProvider =>
        {
            var options = serviceProvider.GetService<BuiltAgentFrameworkBuilder>()?
                .Value
                .MetricsOptions
                ?? new AgentFrameworkMetricsOptions();
            return new GenAiTokenMetrics(options);
        });
        services.TryAddSingleton<IPipelineMetrics>(serviceProvider =>
        {
            var options = serviceProvider.GetService<BuiltAgentFrameworkBuilder>()?
                .Value
                .PipelineMetricsOptions;
            return options is null
                ? new NoOpPipelineMetrics()
                : new PipelineMetrics(options);
        });
        services.TryAddSingleton<ChatCompletionCollectorHolder>();
        services.TryAddSingleton<IChatCompletionCollector>(serviceProvider =>
            serviceProvider.GetRequiredService<ChatCompletionCollectorHolder>());
        services.TryAddSingleton<ToolCallCollectorHolder>();
        services.TryAddSingleton<IToolCallCollector>(serviceProvider =>
            serviceProvider.GetRequiredService<ToolCallCollectorHolder>());
        services.TryAddSingleton<IProgressSequence, ProgressSequenceProvider>();
        services.TryAddSingleton<
            IProgressReporterAccessor,
            ProgressReporterAccessor>();
        services.TryAddSingleton<
            IProgressReporterErrorHandler,
            NullProgressReporterErrorHandler>();
    }
}
