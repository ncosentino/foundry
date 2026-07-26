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
    /// <summary>
    /// The exact upstream marker name for the hosted web search tool
    /// (<c>Microsoft.Extensions.AI.HostedWebSearchTool().Name</c>), confirmed by live
    /// instantiation against <c>Microsoft.Agents.AI.Harness</c> 1.15.0. When
    /// <see cref="FoundryHarnessFeatureSelections.EnableWebSearch"/> is <see langword="true"/>, the
    /// upstream bundle adds a tool with exactly this name to <c>ChatOptions.Tools</c>; a
    /// caller-supplied tool sharing this name would collide with it.
    /// </summary>
    internal const string WebSearchToolName = "web_search";

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
        _ = FoundryHarnessTelemetryComposition.Create(configuration);
        return _inspector.Describe(configuration);
    }

    private AIAgent CreateCore(
        FoundryHarnessAgentConfiguration configuration,
        ILoggerFactory? loggerFactory,
        IServiceProvider? services)
    {
        Validate(configuration);
        var telemetryComposition = FoundryHarnessTelemetryComposition.Create(configuration);

        var tools = configuration.Tools.Count > 0 ? new List<AITool>(configuration.Tools) : null;
        var additionalContextProviders = configuration.AdditionalContextProviders.Count > 0
            ? new List<AIContextProvider>(configuration.AdditionalContextProviders)
            : null;

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
            CompactionStrategy = configuration.CompactionStrategy,
            DisableCompaction = !configuration.Features.EnableCompaction,
            MaximumIterationsPerRequest = configuration.MaximumIterationsPerRequest,
            ChatHistoryProvider = configuration.ChatHistoryProvider,
            AIContextProviders = additionalContextProviders,
            DisableToolAutoApproval = !configuration.Features.EnableToolAutoApproval,
            ToolApprovalAgentOptions = configuration.ToolApprovalAgentOptions,
            DisableApprovalNotRequiredFunctionBypassing =
                !configuration.Features.EnableApprovalNotRequiredFunctionBypassing,
            DisableApprovalResponseBinding = !configuration.Features.EnableApprovalResponseBinding,
            DisableFileMemory = !configuration.Features.EnableFileMemory,
            FileMemoryStore = configuration.FileMemoryStore,
            FileAccessStore = configuration.FileAccessStore,
            FileAccessProviderOptions = configuration.FileAccessProviderOptions,
            DisableWebSearch = !configuration.Features.EnableWebSearch,
            DisableTodoProvider = !configuration.Features.EnableTodoProvider,
            DisableAgentModeProvider = !configuration.Features.EnableAgentModeProvider,
            AgentModeProviderOptions = configuration.AgentModeProviderOptions,
            DisableAgentSkillsProvider = !configuration.Features.EnableAgentSkills,
            AgentSkillsSource = configuration.AgentSkillsSource,
            DisableOpenTelemetry = !configuration.Features.EnableOpenTelemetry,
            OpenTelemetrySourceName = configuration.OpenTelemetrySourceName,
        };

        var agent = telemetryComposition
            .ComposeChatClient(configuration.ChatClient)
            .AsHarnessAgent(options, loggerFactory, services);
        return telemetryComposition.ComposeAgent(agent);
    }

    private static void Validate(FoundryHarnessAgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuration.ChatClient);
        ArgumentNullException.ThrowIfNull(configuration.Tools);
        ArgumentNullException.ThrowIfNull(configuration.Features);
        ArgumentNullException.ThrowIfNull(configuration.AdditionalContextProviders);

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

        if (configuration.MaxOutputTokens is { } maxOut && maxOut < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                configuration.MaxOutputTokens,
                "FoundryHarnessAgentConfiguration.MaxOutputTokens must be non-negative when provided.");
        }

        // Reject a compaction-only input that upstream would silently ignore.
        if (!configuration.Features.EnableCompaction && configuration.MaxContextWindowTokens is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.MaxContextWindowTokens was supplied while " +
                "Features.EnableCompaction is false. The upstream bundle ignores this " +
                "compaction-specific budget when DisableCompaction is true. Pass null for " +
                "MaxContextWindowTokens when compaction is disabled. MaxOutputTokens alone may " +
                "still be supplied as a standalone per-response output cap.",
                nameof(configuration));
        }

        if (configuration.CompactionStrategy is not null &&
            configuration.MaxContextWindowTokens is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.MaxContextWindowTokens was supplied together " +
                "with CompactionStrategy. The upstream bundle uses the explicit strategy directly " +
                "and ignores this context-window budget. Pass null for MaxContextWindowTokens when " +
                "supplying CompactionStrategy. MaxOutputTokens may still be supplied separately as " +
                "a per-response output cap.",
                nameof(configuration));
        }

        if (configuration.CompactionStrategy is null &&
            configuration.MaxContextWindowTokens is { } ctx &&
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

        var callerToolNames = configuration.Tools
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);
        var builtInToolNameCollisions = EnumerateEnabledBuiltInToolNames(configuration)
            .Where(callerToolNames.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (builtInToolNameCollisions.Count > 0)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.Tools collides with enabled upstream built-in " +
                $"provider tool names: {string.Join(", ", builtInToolNameCollisions)}. " +
                "Caller tools would silently shadow the built-in implementations because upstream " +
                "tool dispatch uses the first matching name. Rename the caller tools or disable " +
                "the corresponding built-in providers.",
                nameof(configuration));
        }

        for (int i = 0; i < configuration.AdditionalContextProviders.Count; i++)
        {
            if (configuration.AdditionalContextProviders[i] is null)
            {
                throw new ArgumentException(
                    "FoundryHarnessAgentConfiguration.AdditionalContextProviders contains a null " +
                    $"element at index {i}.",
                    nameof(configuration));
            }
        }

        if (configuration.Features.EnableCompaction &&
            configuration.CompactionStrategy is null &&
            (configuration.MaxContextWindowTokens is null || configuration.MaxOutputTokens is null))
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.Features.EnableCompaction is true, but " +
                "CompactionStrategy was not supplied and MaxContextWindowTokens/MaxOutputTokens " +
                "were not both supplied. The upstream bundle cannot honor in-loop compaction " +
                "without either an explicit strategy or both token budgets.",
                nameof(configuration));
        }

        if (!configuration.Features.EnableCompaction && configuration.CompactionStrategy is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.CompactionStrategy was supplied while " +
                "Features.EnableCompaction is false. Set EnableCompaction to true to use a custom " +
                "compaction strategy, or pass null here to leave compaction disabled.",
                nameof(configuration));
        }

        if (!configuration.Features.EnableFileMemory && configuration.FileMemoryStore is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.FileMemoryStore was supplied while " +
                "Features.EnableFileMemory is false. Set EnableFileMemory to true to use a custom " +
                "file memory store, or pass null here to leave file memory at its disabled state.",
                nameof(configuration));
        }

        if (!configuration.Features.EnableAgentSkills && configuration.AgentSkillsSource is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.AgentSkillsSource was supplied while " +
                "Features.EnableAgentSkills is false. Set EnableAgentSkills to true to use a custom " +
                "skills source, or pass null here to leave agent skills at its disabled state.",
                nameof(configuration));
        }

        if (!configuration.Features.EnableToolAutoApproval && configuration.ToolApprovalAgentOptions is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.ToolApprovalAgentOptions was supplied while " +
                "Features.EnableToolAutoApproval is false. Set EnableToolAutoApproval to true to use " +
                "custom approval options, or pass null here to leave tool auto-approval disabled.",
                nameof(configuration));
        }

        if (!configuration.Features.EnableAgentModeProvider && configuration.AgentModeProviderOptions is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.AgentModeProviderOptions was supplied while " +
                "Features.EnableAgentModeProvider is false. Set EnableAgentModeProvider to true to " +
                "use custom mode options, or pass null here to leave the agent-mode provider disabled.",
                nameof(configuration));
        }

        if (configuration.FileAccessStore is null && configuration.FileAccessProviderOptions is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.FileAccessProviderOptions was supplied while " +
                "FileAccessStore is null. FileAccessProviderOptions only applies when a " +
                "FileAccessStore is also supplied; supply a store or pass null here.",
                nameof(configuration));
        }

        if (!configuration.Features.EnableOpenTelemetry && configuration.OpenTelemetrySourceName is not null)
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.OpenTelemetrySourceName was supplied while " +
                "Features.EnableOpenTelemetry is false. Set EnableOpenTelemetry to true to use a " +
                "custom source name, or pass null here to leave OpenTelemetry disabled.",
                nameof(configuration));
        }

        if (configuration.OpenTelemetrySourceName is not null &&
            string.IsNullOrWhiteSpace(configuration.OpenTelemetrySourceName))
        {
            throw new ArgumentException(
                "FoundryHarnessAgentConfiguration.OpenTelemetrySourceName must be non-empty when " +
                "provided; pass null to use the upstream default source name.",
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

    private static IEnumerable<string> EnumerateEnabledBuiltInToolNames(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (configuration.Features.EnableWebSearch)
        {
            yield return WebSearchToolName;
        }

        if (configuration.Features.EnableTodoProvider)
        {
            yield return "todos_add";
            yield return "todos_complete";
            yield return "todos_remove";
            yield return "todos_get_remaining";
            yield return "todos_get_all";
        }

        if (configuration.Features.EnableAgentModeProvider)
        {
            yield return "mode_set";
            yield return "mode_get";
        }

        if (configuration.Features.EnableFileMemory)
        {
            yield return "file_memory_write";
            yield return "file_memory_read";
            yield return "file_memory_delete";
            yield return "file_memory_ls";
            yield return "file_memory_grep";
            yield return "file_memory_replace";
            yield return "file_memory_replace_lines";
        }

        if (configuration.FileAccessStore is not null)
        {
            yield return "file_access_read";
            yield return "file_access_ls";
            yield return "file_access_grep";
            yield return "file_access_write";
            yield return "file_access_delete";
            yield return "file_access_replace";
            yield return "file_access_replace_lines";
        }

        if (configuration.Features.EnableAgentSkills)
        {
            yield return "load_skill";
            yield return "read_skill_resource";
            yield return "run_skill_script";
        }
    }
}
