namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Maps a <see cref="FoundryHarnessAgentConfiguration"/> to the requested-versus-effective
/// disposition of every upstream <c>Microsoft.Agents.AI.Harness</c> bundle dimension (MAF 1.15).
/// </summary>
/// <remarks>
/// This mapping is pure and evidence-derived from the upstream
/// <c>Microsoft.Agents.AI.HarnessAgentOptions</c> XML documentation shipped with
/// <c>Microsoft.Agents.AI.Harness</c> 1.15.0: it performs no reflection or probing of a live
/// agent instance. Categorical dimensions this type does not yet expose (background agents,
/// loop evaluation) are reported as unrequested limitations rather than silently omitted.
/// </remarks>
internal sealed class FoundryHarnessBundleDefaultsInspector
{
    private const string BackgroundAgentsLimitation =
        "Not exposed by FoundryHarnessAgentConfiguration in this candidate. Upstream supports " +
        "opt-in delegation via HarnessAgentOptions.BackgroundAgents; tracked for a follow-up " +
        "API-candidate review.";

    private const string LoopEvaluationLimitation =
        "Not exposed by FoundryHarnessAgentConfiguration in this candidate. Upstream supports " +
        "opt-in re-invocation via HarnessAgentOptions.LoopEvaluators/LoopAgentOptions; tracked for " +
        "a follow-up API-candidate review.";

    private const string FunctionInvocationLimitation =
        "Upstream always wraps the provider chat client with FunctionInvokingChatClient. " +
        "MaximumIterationsPerRequest is configurable, but the loop itself cannot be disabled.";

    private const string MessageInjectionLimitation =
        "Upstream always wraps the provider chat client with MessageInjectingChatClient. " +
        "There is no supported way to disable message injection.";

    private const string HistoryPersistenceLimitation =
        "Upstream always wraps the provider chat client with a per-service-call chat history " +
        "persisting decorator. The backing ChatHistoryProvider store is the only configurable part.";

    internal FoundryHarnessEffectiveDefaults Describe(FoundryHarnessAgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var features = configuration.Features;
        var dispositions = new List<FoundryHarnessFeatureDisposition>
        {
            AlwaysOn(FoundryHarnessFeature.FunctionInvocation, FunctionInvocationLimitation),
            AlwaysOn(FoundryHarnessFeature.MessageInjection, MessageInjectionLimitation),
            DescribeHistoryPersistence(configuration),
            DescribeHarnessInstructions(configuration),
            Toggle(FoundryHarnessFeature.WebSearch, features.EnableWebSearch),
            DescribeFileMemory(configuration),
            DescribeFileAccess(configuration),
            DescribeAgentSkills(configuration),
            DescribeToolAutoApproval(configuration),
            Toggle(
                FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing,
                features.EnableApprovalNotRequiredFunctionBypassing),
            Toggle(FoundryHarnessFeature.ApprovalResponseBinding, features.EnableApprovalResponseBinding),
            DescribeOpenTelemetry(configuration),
            Toggle(FoundryHarnessFeature.TodoProvider, features.EnableTodoProvider),
            DescribeAgentModeProvider(configuration),
            DescribeCompaction(configuration),
            DescribeAdditionalContextProviders(configuration),
            NotExposed(FoundryHarnessFeature.BackgroundAgents, BackgroundAgentsLimitation),
            NotExposed(FoundryHarnessFeature.LoopEvaluation, LoopEvaluationLimitation),
        };

        return FoundryHarnessEffectiveDefaults.Create(dispositions);
    }

    private static FoundryHarnessFeatureDisposition DescribeHistoryPersistence(
        FoundryHarnessAgentConfiguration configuration)
    {
        bool callerSupplied = configuration.ChatHistoryProvider is not null;
        bool hasBothTokenBudgets =
            configuration.MaxContextWindowTokens is not null && configuration.MaxOutputTokens is not null;

        string backingDescription = callerSupplied
            ? "Caller-supplied ChatHistoryProvider instance is used directly."
            : hasBothTokenBudgets
                ? "Upstream default: InMemoryChatHistoryProvider, configured with a compaction-based " +
                  "chat reducer because both MaxContextWindowTokens and MaxOutputTokens are supplied. " +
                  "Under Foundry validation, MaxContextWindowTokens is rejected when " +
                  "Features.EnableCompaction is false, so a reducer is only present here when " +
                  "compaction is explicitly enabled with both token budgets; there is no hidden " +
                  "reducer when compaction is disabled."
                : "Upstream default: InMemoryChatHistoryProvider, no in-loop compaction configured. " +
                  "MaxContextWindowTokens was not supplied; the upstream default provider only " +
                  "activates compaction when both MaxContextWindowTokens and MaxOutputTokens are " +
                  "present.";

        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.HistoryPersistence,
            FoundryHarnessFeatureRequestedState.NotConfigurable,
            FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
            HistoryPersistenceLimitation,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            backingDescription);
    }

    private static FoundryHarnessFeatureDisposition DescribeHarnessInstructions(
        FoundryHarnessAgentConfiguration configuration)
    {
        var overrideValue = configuration.HarnessInstructionsOverride;

        if (overrideValue is null)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.HarnessInstructions,
                FoundryHarnessFeatureRequestedState.NotRequested,
                FoundryHarnessFeatureEffectiveState.Enabled,
                null,
                FoundryHarnessFeatureBackingSelection.UpstreamDefault,
                "Upstream default: the built-in HarnessAgent default instructions text is used, " +
                "combined before (and followed by) agent-specific Instructions.");
        }

        if (overrideValue.Length == 0)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.HarnessInstructions,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.HarnessInstructions,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            FoundryHarnessFeatureBackingSelection.CallerSupplied,
            "Caller-supplied harness instructions text is used verbatim, combined before (and " +
            "followed by) agent-specific Instructions.");
    }

    private static FoundryHarnessFeatureDisposition DescribeFileMemory(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (!configuration.Features.EnableFileMemory)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FileMemory,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool callerSupplied = configuration.FileMemoryStore is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.FileMemory,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            callerSupplied
                ? "Caller-supplied FileMemoryStore instance is used directly."
                : "Upstream default: a FileSystemAgentFileStore rooted at a process-local, " +
                  "timestamp/guid-qualified directory under the current working directory " +
                  "(agent-file-memory/{timestamp}_{guid}).");
    }

    private static FoundryHarnessFeatureDisposition DescribeFileAccess(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (configuration.FileAccessStore is null)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.FileAccess,
                FoundryHarnessFeatureRequestedState.NotRequested,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool optionsSupplied = configuration.FileAccessProviderOptions is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.FileAccess,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            FoundryHarnessFeatureBackingSelection.CallerSupplied,
            optionsSupplied
                ? "Caller-supplied FileAccessStore instance is used directly, configured with " +
                  "caller-supplied FileAccessProviderOptions. There is no upstream default store for " +
                  "this dimension: it is fully opt-in."
                : "Caller-supplied FileAccessStore instance is used directly; FileAccessProviderOptions " +
                  "was not supplied so the provider uses its own default options. There is no upstream " +
                  "default store for this dimension: it is fully opt-in.");
    }

    private static FoundryHarnessFeatureDisposition DescribeAgentSkills(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (!configuration.Features.EnableAgentSkills)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.AgentSkills,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool callerSupplied = configuration.AgentSkillsSource is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.AgentSkills,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            callerSupplied
                ? "Caller-supplied AgentSkillsSource instance is used directly."
                : "Upstream default: file-based skill discovery rooted at the current working directory.");
    }

    private static FoundryHarnessFeatureDisposition DescribeToolAutoApproval(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (!configuration.Features.EnableToolAutoApproval)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.ToolAutoApproval,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool callerSupplied = configuration.ToolApprovalAgentOptions is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.ToolAutoApproval,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            callerSupplied
                ? "Caller-supplied ToolApprovalAgentOptions instance is used directly."
                : "Upstream default: ToolApprovalAgent uses its own built-in default options.");
    }

    private static FoundryHarnessFeatureDisposition DescribeAgentModeProvider(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (!configuration.Features.EnableAgentModeProvider)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.AgentModeProvider,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool callerSupplied = configuration.AgentModeProviderOptions is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.AgentModeProvider,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            callerSupplied
                ? "Caller-supplied AgentModeProviderOptions instance is used directly."
                : "Upstream default: built-in \"plan\" and \"execute\" modes.");
    }

    private static FoundryHarnessFeatureDisposition DescribeOpenTelemetry(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (!configuration.Features.EnableOpenTelemetry)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.OpenTelemetry,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool callerSupplied = configuration.OpenTelemetrySourceName is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.OpenTelemetry,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            callerSupplied
                ? $"Caller-supplied ActivitySource name \"{configuration.OpenTelemetrySourceName}\" is used."
                : "Upstream default source name: \"Experimental.Microsoft.Agents.AI\".");
    }

    private static FoundryHarnessFeatureDisposition DescribeCompaction(
        FoundryHarnessAgentConfiguration configuration)
    {
        if (!configuration.Features.EnableCompaction)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.Compaction,
                FoundryHarnessFeatureRequestedState.RequestedDisabled,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        bool callerSupplied = configuration.CompactionStrategy is not null;
        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.Compaction,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            callerSupplied
                ? FoundryHarnessFeatureBackingSelection.CallerSupplied
                : FoundryHarnessFeatureBackingSelection.UpstreamDefault,
            callerSupplied
                ? "Caller-supplied CompactionStrategy instance is used directly; " +
                  "MaxContextWindowTokens/MaxOutputTokens are not required for compaction purposes " +
                  "when a strategy is supplied."
                : "Upstream default: a ContextWindowCompactionStrategy constructed from the supplied " +
                  "MaxContextWindowTokens and MaxOutputTokens token budgets.");
    }

    private static FoundryHarnessFeatureDisposition DescribeAdditionalContextProviders(
        FoundryHarnessAgentConfiguration configuration)
    {
        int count = configuration.AdditionalContextProviders.Count;
        if (count == 0)
        {
            return FoundryHarnessFeatureDisposition.Create(
                FoundryHarnessFeature.AdditionalContextProviders,
                FoundryHarnessFeatureRequestedState.NotRequested,
                FoundryHarnessFeatureEffectiveState.Disabled,
                null,
                FoundryHarnessFeatureBackingSelection.NotApplicable,
                null);
        }

        return FoundryHarnessFeatureDisposition.Create(
            FoundryHarnessFeature.AdditionalContextProviders,
            FoundryHarnessFeatureRequestedState.RequestedEnabled,
            FoundryHarnessFeatureEffectiveState.Enabled,
            null,
            FoundryHarnessFeatureBackingSelection.CallerSupplied,
            $"{count} caller-supplied AIContextProvider instance(s) included alongside the built-in providers.");
    }

    private static FoundryHarnessFeatureDisposition AlwaysOn(
        FoundryHarnessFeature feature,
        string limitation) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            FoundryHarnessFeatureRequestedState.NotConfigurable,
            FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
            limitation,
            FoundryHarnessFeatureBackingSelection.NotApplicable,
            null);

    private static FoundryHarnessFeatureDisposition Toggle(FoundryHarnessFeature feature, bool enabled) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            enabled
                ? FoundryHarnessFeatureRequestedState.RequestedEnabled
                : FoundryHarnessFeatureRequestedState.RequestedDisabled,
            enabled
                ? FoundryHarnessFeatureEffectiveState.Enabled
                : FoundryHarnessFeatureEffectiveState.Disabled,
            null,
            FoundryHarnessFeatureBackingSelection.NotApplicable,
            null);

    private static FoundryHarnessFeatureDisposition NotExposed(
        FoundryHarnessFeature feature,
        string limitation) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            FoundryHarnessFeatureRequestedState.NotRequested,
            FoundryHarnessFeatureEffectiveState.Disabled,
            limitation,
            FoundryHarnessFeatureBackingSelection.NotApplicable,
            null);
}
