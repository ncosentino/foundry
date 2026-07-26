using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Constructs an <see cref="AIAgent"/> through the official upstream
/// <c>Microsoft.Agents.AI.Harness</c> complete-bundle pipeline from an explicit
/// <see cref="FoundryHarnessAgentConfiguration"/>, and reports the requested-versus-effective
/// disposition of every bundle default.
/// </summary>
/// <remarks>
/// This factory always builds through <c>Microsoft.Extensions.AI.ChatClientHarnessExtensions.AsHarnessAgent</c>.
/// It never falls back to, or otherwise composes with, the selected-provider composition
/// surface in <c>NexusLabs.Foundry.MicrosoftAgentFramework</c>; the two lanes are
/// intentionally separate.
/// </remarks>
public sealed class FoundryHarnessAgentFactory
{
    private readonly FoundryHarnessBundleDefaultsInspector _inspector = new();

    /// <summary>
    /// Builds an <see cref="AIAgent"/> for the given configuration using the upstream bundle's
    /// default logging and service resolution.
    /// </summary>
    /// <param name="configuration">The explicit bundle configuration.</param>
    /// <returns>An <see cref="AIAgent"/> backed by the official upstream <c>HarnessAgent</c>.</returns>
    public AIAgent Create(FoundryHarnessAgentConfiguration configuration) =>
        CreateCore(configuration, loggerFactory: null, services: null);

    /// <summary>
    /// Builds an <see cref="AIAgent"/> for the given configuration, using
    /// <paramref name="loggerFactory"/> for the bundle's internal logging.
    /// </summary>
    /// <param name="configuration">The explicit bundle configuration.</param>
    /// <param name="loggerFactory">The logger factory used by the agent and its components.</param>
    /// <returns>An <see cref="AIAgent"/> backed by the official upstream <c>HarnessAgent</c>.</returns>
    public AIAgent Create(
        FoundryHarnessAgentConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        return CreateCore(configuration, loggerFactory, services: null);
    }

    /// <summary>
    /// Builds an <see cref="AIAgent"/> for the given configuration, resolving dependencies
    /// required by tools and other agent components from <paramref name="services"/>.
    /// </summary>
    /// <param name="configuration">The explicit bundle configuration.</param>
    /// <param name="services">The service provider used to resolve tool and component dependencies.</param>
    /// <returns>An <see cref="AIAgent"/> backed by the official upstream <c>HarnessAgent</c>.</returns>
    public AIAgent Create(
        FoundryHarnessAgentConfiguration configuration,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return CreateCore(configuration, loggerFactory: null, services);
    }

    /// <summary>
    /// Builds an <see cref="AIAgent"/> for the given configuration, using
    /// <paramref name="loggerFactory"/> for internal logging and resolving dependencies from
    /// <paramref name="services"/>.
    /// </summary>
    /// <param name="configuration">The explicit bundle configuration.</param>
    /// <param name="loggerFactory">The logger factory used by the agent and its components.</param>
    /// <param name="services">The service provider used to resolve tool and component dependencies.</param>
    /// <returns>An <see cref="AIAgent"/> backed by the official upstream <c>HarnessAgent</c>.</returns>
    public AIAgent Create(
        FoundryHarnessAgentConfiguration configuration,
        ILoggerFactory loggerFactory,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(services);
        return CreateCore(configuration, loggerFactory, services);
    }

    /// <summary>
    /// Computes the requested-versus-effective disposition of every upstream bundle dimension
    /// for <paramref name="configuration"/>, without constructing an agent.
    /// </summary>
    /// <param name="configuration">The explicit bundle configuration to inspect.</param>
    /// <returns>A complete <see cref="FoundryHarnessEffectiveDefaults"/> report.</returns>
    /// <remarks>
    /// <para>
    /// This method runs the same configuration validation as every <c>Create</c> overload.
    /// An invalid configuration that would cause <c>Create</c> to throw will also cause
    /// <c>DescribeEffectiveDefaults</c> to throw with the same error. This guarantees that any
    /// report returned here describes a configuration that can actually be constructed.
    /// </para>
    /// <para>
    /// This is a pure function of <paramref name="configuration"/>. Call it before construction
    /// to preview what a configuration will do, or after construction (with the same
    /// configuration instance) to explain what was actually built.
    /// </para>
    /// </remarks>
    public FoundryHarnessEffectiveDefaults DescribeEffectiveDefaults(
        FoundryHarnessAgentConfiguration configuration)
    {
        Validate(configuration);
        return _inspector.Describe(configuration);
    }

    private AIAgent CreateCore(
        FoundryHarnessAgentConfiguration configuration,
        ILoggerFactory? loggerFactory,
        IServiceProvider? services)
    {
        Validate(configuration);

        var tools = configuration.Tools.Count > 0 ? new List<AITool>(configuration.Tools) : null;

        var options = new HarnessAgentOptions
        {
            Id = configuration.Id,
            Name = configuration.Name,
            Description = configuration.Description,
            HarnessInstructions = configuration.HarnessInstructionsOverride,
            ChatOptions = new ChatOptions
            {
                Instructions = configuration.Instructions,
                Tools = tools,
            },
            MaxContextWindowTokens = configuration.MaxContextWindowTokens,
            MaxOutputTokens = configuration.MaxOutputTokens,
            DisableCompaction = !configuration.Features.EnableCompaction,
            MaximumIterationsPerRequest = configuration.MaximumIterationsPerRequest,
            DisableToolAutoApproval = !configuration.Features.EnableToolAutoApproval,
            DisableApprovalNotRequiredFunctionBypassing =
                !configuration.Features.EnableApprovalNotRequiredFunctionBypassing,
            DisableApprovalResponseBinding = !configuration.Features.EnableApprovalResponseBinding,
            DisableFileMemory = !configuration.Features.EnableFileMemory,
            FileAccessStore = configuration.FileAccessStore,
            DisableWebSearch = !configuration.Features.EnableWebSearch,
            DisableTodoProvider = !configuration.Features.EnableTodoProvider,
            DisableAgentModeProvider = !configuration.Features.EnableAgentModeProvider,
            DisableAgentSkillsProvider = !configuration.Features.EnableAgentSkills,
            DisableOpenTelemetry = !configuration.Features.EnableOpenTelemetry,
        };

        return configuration.ChatClient.AsHarnessAgent(options, loggerFactory, services);
    }

    private static void Validate(FoundryHarnessAgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.ChatClient);
        ArgumentNullException.ThrowIfNull(configuration.Tools);
        ArgumentNullException.ThrowIfNull(configuration.Features);

        if (string.IsNullOrWhiteSpace(configuration.Name))
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.Name must be a non-empty value.",
                nameof(configuration));
        }

        if (configuration.Id is not null && string.IsNullOrWhiteSpace(configuration.Id))
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.Id must be non-empty when provided; " +
                "pass null to let the upstream bundle generate an identifier.",
                nameof(configuration));
        }

        if (configuration.MaxContextWindowTokens is { } maxCtx && maxCtx <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                configuration.MaxContextWindowTokens,
                "FoundryHarnessAgentConfiguration.MaxContextWindowTokens must be positive when provided.");
        }

        if (configuration.MaxOutputTokens is { } maxOut && maxOut <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                configuration.MaxOutputTokens,
                "FoundryHarnessAgentConfiguration.MaxOutputTokens must be positive when provided.");
        }

        if (configuration.MaxContextWindowTokens is { } ctx &&
            configuration.MaxOutputTokens is { } output &&
            output >= ctx)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.MaxContextWindowTokens must be greater than " +
                "MaxOutputTokens. The upstream bundle requires MaxOutputTokens < " +
                "MaxContextWindowTokens to reserve context space for the function-invocation loop.",
                nameof(configuration));
        }

        if (configuration.MaximumIterationsPerRequest is { } maxIter && maxIter <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                configuration.MaximumIterationsPerRequest,
                "FoundryHarnessAgentConfiguration.MaximumIterationsPerRequest must be positive when provided.");
        }

        for (int i = 0; i < configuration.Tools.Count; i++)
        {
            var tool = configuration.Tools[i];
            if (tool is null)
            {
                throw new ArgumentException(
                    $"FoundryHarnessAgentConfiguration.Tools contains a null element at index {i}.",
                    nameof(configuration));
            }

            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                throw new ArgumentException(
                    $"FoundryHarnessAgentConfiguration.Tools[{i}] has a blank or empty Name. " +
                    "Tool names must be non-empty.",
                    nameof(configuration));
            }
        }

        var duplicateToolNames = configuration.Tools
            .Select(tool => tool.Name)
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (duplicateToolNames.Count > 0)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.Tools contains duplicate tool names: " +
                $"{string.Join(", ", duplicateToolNames)}.",
                nameof(configuration));
        }

        if (configuration.Features.EnableCompaction &&
            (configuration.MaxContextWindowTokens is null || configuration.MaxOutputTokens is null))
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.Features.EnableCompaction is true, but " +
                "MaxContextWindowTokens and MaxOutputTokens were not both supplied. The upstream " +
                "bundle cannot honor in-loop compaction without both token budgets.",
                nameof(configuration));
        }

        if (configuration.ChatClient.GetService<FunctionInvokingChatClient>() is not null)
        {
            throw new InvalidOperationException(
                "FoundryHarnessAgentConfiguration.ChatClient already contains a function-invocation " +
                "loop. The upstream Harness bundle must own the complete pipeline and cannot compose " +
                "over a pre-existing function-invocation loop.");
        }

        if (configuration.ChatClient.GetService<MessageInjectingChatClient>() is not null)
        {
            throw new InvalidOperationException(
                "FoundryHarnessAgentConfiguration.ChatClient already contains message-injection " +
                "middleware. The upstream Harness bundle must own the complete pipeline and cannot " +
                "compose over pre-existing message injection.");
        }

        if (configuration.ChatClient.GetService<OpenTelemetryChatClient>() is not null)
        {
            throw new InvalidOperationException(
                "FoundryHarnessAgentConfiguration.ChatClient already contains OpenTelemetry " +
                "instrumentation. The upstream Harness bundle must own the complete telemetry " +
                "pipeline; pre-existing instrumentation is rejected regardless of " +
                "FoundryHarnessFeatureSelections.EnableOpenTelemetry: if true it would duplicate " +
                "telemetry; if false the requested disabled state would be ineffective because " +
                "the pre-existing instrumentation remains active.");
        }
    }
}
