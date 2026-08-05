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
    /// Upstream's context-window compaction, which bounds the conversation an agent carries
    /// <em>between</em> turns. Opt-in: disabled unless both a context-window token budget and an
    /// output token budget (or a custom strategy) are supplied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism.</strong> Upstream installs a <c>CompactionProvider</c>, which is an
    /// <c>AIContextProvider</c>. Context providers are invoked once per <em>agent turn</em> — once per
    /// <c>RunAsync</c> — above the tool-invocation loop, and they operate on the persisted chat-history
    /// index rather than on the messages of any individual provider request. A reduction they perform
    /// changes what the agent subsequently remembers. Budgets are denominated in provider tokens.
    /// </para>
    /// <para>
    /// <strong>Consequence.</strong> A single turn that makes several model calls — one per tool round
    /// — is compacted only against the state that preceded the <em>first</em> round. Measured over a
    /// deterministic two-round tool loop with a strategy whose trigger always fires, the strategy is
    /// consulted once, against a two-message index; the round carrying the tool call and its result is
    /// never offered to it. Identical on <c>Microsoft.Agents.AI.Harness</c> 1.15.0, 1.16.0, and 1.17.0, because
    /// this follows from the <c>AIContextProvider</c> contract rather than from a defect. Tracked in
    /// ncosentino/foundry#73.
    /// </para>
    /// <para>
    /// If context growth comes from tool results inside one turn, this dimension will not bound it;
    /// see <see cref="HybridCompaction"/>. The two are independent and may be combined.
    /// </para>
    /// </remarks>
    Compaction,

    /// <summary>
    /// Foundry's per-provider-call hybrid compaction, which bounds what is <em>sent</em> on each
    /// individual provider request rather than what the agent remembers. Opt-in: disabled unless
    /// <c>FoundryHarnessAgentConfiguration.HybridCompactionOptions</c> is supplied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism.</strong> Foundry wraps the caller's chat client at the innermost position,
    /// beneath every decorator the bundle installs above it. Both the function-invocation loop and
    /// message injection recurse by calling their inner client afresh, so every intermediate tool round
    /// and every injected extra call cascades down to this node. It therefore observes the exact
    /// message set dispatched for each provider request, including the round carrying a tool call and
    /// its result — precisely the round <see cref="Compaction"/> never sees. Budgets are denominated in
    /// UTF-8 bytes of rendered content, not provider tokens, because bytes are computable locally
    /// without a tokenizer matched to the provider.
    /// </para>
    /// <para>
    /// <strong>Stored history is not shrunk.</strong> This node sits inner to the per-service-call
    /// history decorator, so history is persisted <em>before</em> a reduction is applied and every call
    /// re-assembles from the full record. A reduction bounds one dispatch; it never permanently
    /// discards conversation. The trade-off is that the work is not cumulative — reduction cost is paid
    /// on every call and the stored record still grows — which is a reason to enable
    /// <see cref="Compaction"/> alongside this rather than instead of it.
    /// </para>
    /// <para>
    /// <strong>Failure mode.</strong> The supplied reducer's output is treated as a proposal and
    /// verified against the hard limit and Foundry's structural-preservation rules. A context that
    /// cannot be reduced below the hard limit fails the request rather than being forwarded over
    /// budget.
    /// </para>
    /// <para>
    /// This is Foundry-owned rather than upstream, so it is not covered by upstream's compatibility
    /// guarantees and its position depends on the verified bundle middleware order.
    /// </para>
    /// </remarks>
    HybridCompaction,

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
