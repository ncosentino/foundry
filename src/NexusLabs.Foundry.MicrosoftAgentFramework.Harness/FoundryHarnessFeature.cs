namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Identifies a distinct default-controlling dimension of the upstream
/// <c>Microsoft.Agents.AI.Harness</c> complete-bundle pipeline (MAF 1.15).
/// </summary>
/// <remarks>
/// Every value here corresponds to a specific pipeline decorator, context provider,
/// or tool documented on <c>Microsoft.Agents.AI.HarnessAgentOptions</c>. See
/// <see cref="FoundryHarnessEffectiveDefaults"/> for the requested-versus-effective
/// disposition of each dimension for a given <see cref="FoundryHarnessAgentConfiguration"/>.
/// </remarks>
public enum FoundryHarnessFeature
{
    /// <summary>
    /// The innermost <c>Microsoft.Extensions.AI.FunctionInvokingChatClient</c> tool-invocation loop.
    /// Always present; only its iteration limit is configurable.
    /// </summary>
    FunctionInvocation,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.MessageInjectingChatClient</c> decorator that allows external
    /// code to inject messages into the conversation mid-stream. Always present.
    /// </summary>
    MessageInjection,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.PerServiceCallChatHistoryPersistingChatClient</c> decorator
    /// that persists chat history after every individual service call. Always present; only the
    /// backing <c>ChatHistoryProvider</c> store is configurable.
    /// </summary>
    HistoryPersistence,

    /// <summary>
    /// The harness-level instructions (<c>HarnessAgentOptions.HarnessInstructions</c>) that guide
    /// general tool usage and reasoning patterns, combined with (and preceding) agent-specific
    /// instructions. <see langword="null"/> uses the upstream built-in default instructions,
    /// <see cref="string.Empty"/> omits harness-level instructions entirely, and any other value is
    /// used verbatim.
    /// </summary>
    HarnessInstructions,

    /// <summary>
    /// The hosted <c>Microsoft.Extensions.AI.HostedWebSearchTool</c> added to chat options.
    /// </summary>
    WebSearch,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.FileMemoryProvider</c> context provider for persisted
    /// session notes and artifacts.
    /// </summary>
    FileMemory,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.FileAccessProvider</c> context provider for shared
    /// working-directory read/write tools. Opt-in: disabled unless a store is supplied.
    /// </summary>
    FileAccess,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.AgentSkillsProvider</c> context provider for file-based
    /// skill discovery.
    /// </summary>
    AgentSkills,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.ToolApprovalAgent</c> "don't ask again" auto-approval middleware.
    /// </summary>
    ToolAutoApproval,

    /// <summary>
    /// The decorator that bypasses approval surfacing for tools that do not require approval.
    /// </summary>
    ApprovalNotRequiredFunctionBypassing,

    /// <summary>
    /// The decorator that binds inbound tool-approval responses to model-originated approval requests.
    /// </summary>
    ApprovalResponseBinding,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.OpenTelemetryAgent</c> wrapper providing OpenTelemetry
    /// instrumentation for Generative AI semantic conventions.
    /// </summary>
    OpenTelemetry,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.TodoProvider</c> context provider for multi-step plan tracking.
    /// </summary>
    TodoProvider,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.AgentModeProvider</c> context provider for plan/execute mode tracking.
    /// </summary>
    AgentModeProvider,

    /// <summary>
    /// In-loop context-window compaction. Opt-in: disabled unless both a context-window token
    /// budget and an output token budget (or a custom strategy) are supplied.
    /// </summary>
    Compaction,

    /// <summary>
    /// Additional <c>Microsoft.Agents.AI.AIContextProvider</c> instances (mapped to
    /// <c>HarnessAgentOptions.AIContextProviders</c>) included in the agent pipeline alongside the
    /// built-in providers. Opt-in: disabled unless at least one instance is supplied.
    /// </summary>
    AdditionalContextProviders,

    /// <summary>
    /// The <c>Microsoft.Agents.AI.BackgroundAgentsProvider</c> context provider for delegating
    /// to background agents. Not exposed by <see cref="FoundryHarnessAgentConfiguration"/> in
    /// this candidate; tracked as a limitation until a follow-up API-candidate review.
    /// </summary>
    BackgroundAgents,

    /// <summary>
    /// The outermost <c>Microsoft.Agents.AI.LoopAgent</c> decorator that re-invokes the agent
    /// until loop evaluators are satisfied. Not exposed by <see cref="FoundryHarnessAgentConfiguration"/>
    /// in this candidate; tracked as a limitation until a follow-up API-candidate review.
    /// </summary>
    LoopEvaluation,
}
