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
        "opt-in re-invocation via HarnessAgentOptions.LoopEvaluators; tracked for a follow-up " +
        "API-candidate review.";

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
            AlwaysOn(FoundryHarnessFeature.HistoryPersistence, HistoryPersistenceLimitation),
            Toggle(FoundryHarnessFeature.WebSearch, features.EnableWebSearch),
            Toggle(FoundryHarnessFeature.FileMemory, features.EnableFileMemory),
            Toggle(FoundryHarnessFeature.AgentSkills, features.EnableAgentSkills),
            Toggle(FoundryHarnessFeature.ToolAutoApproval, features.EnableToolAutoApproval),
            Toggle(
                FoundryHarnessFeature.ApprovalNotRequiredFunctionBypassing,
                features.EnableApprovalNotRequiredFunctionBypassing),
            Toggle(FoundryHarnessFeature.ApprovalResponseBinding, features.EnableApprovalResponseBinding),
            Toggle(FoundryHarnessFeature.OpenTelemetry, features.EnableOpenTelemetry),
            Toggle(FoundryHarnessFeature.TodoProvider, features.EnableTodoProvider),
            Toggle(FoundryHarnessFeature.AgentModeProvider, features.EnableAgentModeProvider),
            Toggle(FoundryHarnessFeature.Compaction, features.EnableCompaction),
            OptIn(FoundryHarnessFeature.FileAccess, configuration.FileAccessStore is not null),
            NotExposed(FoundryHarnessFeature.BackgroundAgents, BackgroundAgentsLimitation),
            NotExposed(FoundryHarnessFeature.LoopEvaluation, LoopEvaluationLimitation),
        };

        return FoundryHarnessEffectiveDefaults.Create(dispositions);
    }

    private static FoundryHarnessFeatureDisposition AlwaysOn(
        FoundryHarnessFeature feature,
        string limitation) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            FoundryHarnessFeatureRequestedState.NotConfigurable,
            FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable,
            limitation);

    private static FoundryHarnessFeatureDisposition Toggle(FoundryHarnessFeature feature, bool enabled) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            enabled
                ? FoundryHarnessFeatureRequestedState.RequestedEnabled
                : FoundryHarnessFeatureRequestedState.RequestedDisabled,
            enabled
                ? FoundryHarnessFeatureEffectiveState.Enabled
                : FoundryHarnessFeatureEffectiveState.Disabled,
            null);

    private static FoundryHarnessFeatureDisposition OptIn(FoundryHarnessFeature feature, bool requested) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            requested
                ? FoundryHarnessFeatureRequestedState.RequestedEnabled
                : FoundryHarnessFeatureRequestedState.NotRequested,
            requested
                ? FoundryHarnessFeatureEffectiveState.Enabled
                : FoundryHarnessFeatureEffectiveState.Disabled,
            null);

    private static FoundryHarnessFeatureDisposition NotExposed(
        FoundryHarnessFeature feature,
        string limitation) =>
        FoundryHarnessFeatureDisposition.Create(
            feature,
            FoundryHarnessFeatureRequestedState.NotRequested,
            FoundryHarnessFeatureEffectiveState.Disabled,
            limitation);
}
