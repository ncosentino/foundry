using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Immutable, fully explicit configuration for constructing an official upstream
/// <c>Microsoft.Agents.AI.HarnessAgent</c> complete-bundle pipeline via
/// <see cref="FoundryHarnessAgentFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every property is <see langword="required"/>. There are no hidden defaults: a caller must
/// consciously supply a value (including explicit <see langword="null"/> for optional-by-design
/// upstream inputs) for every dimension this type exposes. This is a deliberate departure from
/// the upstream <c>Microsoft.Agents.AI.HarnessAgentOptions</c> shape, which allows every property
/// to be left unset and silently defaulted.
/// </para>
/// <para>
/// This type composes the official upstream bundle (<c>Microsoft.Agents.AI.Harness</c>); it is
/// not part of, and must not be confused with, the selected-provider composition
/// surface in <c>NexusLabs.Foundry.MicrosoftAgentFramework</c>. The two lanes are intentionally
/// separate and are not interchangeable.
/// </para>
/// </remarks>
public sealed record FoundryHarnessAgentConfiguration
{
    /// <summary>
    /// Gets the agent identifier, or <see langword="null"/> to let the upstream bundle
    /// generate one.
    /// </summary>
    public required string? Id { get; init; }

    /// <summary>
    /// Gets the agent's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a human-readable description of the agent's purpose, or <see langword="null"/> for none.
    /// </summary>
    public required string? Description { get; init; }

    /// <summary>
    /// Gets agent-specific instructions (mapped to
    /// <c>HarnessAgentOptions.ChatOptions.Instructions</c>), or <see langword="null"/> for none.
    /// These are combined with, and follow, <see cref="HarnessInstructionsOverride"/> (or the
    /// upstream default harness instructions when that is <see langword="null"/>).
    /// </summary>
    public required string? Instructions { get; init; }

    /// <summary>
    /// Gets an override for the harness-level instructions (mapped to
    /// <c>HarnessAgentOptions.HarnessInstructions</c>). Pass <see langword="null"/> to use the
    /// upstream built-in default instructions, or <see cref="string.Empty"/> to omit harness-level
    /// instructions entirely.
    /// </summary>
    public required string? HarnessInstructionsOverride { get; init; }

    /// <summary>
    /// Gets the provider <see cref="IChatClient"/> the upstream bundle pipeline wraps.
    /// </summary>
    /// <remarks>
    /// This must be a "raw" selected-provider chat client. <see cref="FoundryHarnessAgentFactory"/>
    /// rejects function-invocation, message-injection, and OpenTelemetry middleware that is
    /// discoverable through <see cref="IChatClient.GetService(Type, object?)"/>, following the
    /// forwarding convention implemented by <c>DelegatingChatClient</c>. The <see cref="IChatClient"/>
    /// contract does not require wrappers to forward service discovery, so opaque wrappers can
    /// defeat this check. Callers remain responsible for supplying an undecorated client; this
    /// validation catches common accidental double-wrapping and is not a security boundary.
    /// </remarks>
    public required IChatClient ChatClient { get; init; }

    /// <summary>
    /// Gets the complete set of tools available to the agent (mapped to
    /// <c>HarnessAgentOptions.ChatOptions.Tools</c>).
    /// </summary>
    /// <remarks>
    /// Supply an empty list for no additional tools. Source-generator callers (for example,
    /// <c>[AgentFunctionGroup]</c>-declared functions resolved via
    /// <c>NexusLabs.Foundry.MicrosoftAgentFramework</c>) must resolve their generated
    /// <see cref="AIFunction"/> instances explicitly and include them in this list; this
    /// configuration type intentionally performs no reflection-based or generated-tool discovery
    /// of its own. Duplicate caller tool names and collisions with enabled, known upstream built-in
    /// provider tools cause <see cref="FoundryHarnessAgentFactory"/> to fail closed. Additional
    /// caller-supplied <see cref="AIContextProvider"/> instances can inject tools dynamically; their
    /// names are outside the factory's control and must not collide with this list or each other.
    /// </remarks>
    public required IReadOnlyList<AITool> Tools { get; init; }

    /// <summary>
    /// Gets the explicit choices for every default-on-but-disableable upstream bundle dimension.
    /// </summary>
    public required FoundryHarnessFeatureSelections Features { get; init; }

    /// <summary>
    /// Gets the maximum number of tokens the model's context window supports, or
    /// <see langword="null"/> if not applicable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Required (together with <see cref="MaxOutputTokens"/>) when
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/> and
    /// <see cref="CompactionStrategy"/> is <see langword="null"/>. Must be positive when provided.
    /// </para>
    /// <para>
    /// Must be <see langword="null"/> when <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/>
    /// is <see langword="false"/>. Upstream ignores this value when
    /// <c>HarnessAgentOptions.DisableCompaction</c> is <see langword="true"/>. Foundry rejects that
    /// no-op configuration so an explicitly supplied context-window budget is never silently
    /// discarded.
    /// Must also be <see langword="null"/> when <see cref="CompactionStrategy"/> is supplied because
    /// upstream uses that strategy directly and ignores this budget.
    /// <see cref="MaxOutputTokens"/> alone is still permitted as an independent per-response
    /// output cap when compaction is disabled; it does not trigger the reducer.
    /// </para>
    /// </remarks>
    public required int? MaxContextWindowTokens { get; init; }

    /// <summary>
    /// Gets the maximum number of output tokens the model can generate per response, or
    /// <see langword="null"/> if not applicable.
    /// </summary>
    /// <remarks>
    /// Required (together with <see cref="MaxContextWindowTokens"/>) when
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/> and
    /// <see cref="CompactionStrategy"/> is <see langword="null"/>. Must be non-negative when
    /// provided. May also be supplied when compaction is disabled or an explicit strategy is used
    /// as a standalone per-response output cap.
    /// Upstream propagates this value to <c>ChatOptions.MaxOutputTokens</c> while
    /// <c>DisableCompaction</c> prevents chat reduction.
    /// </remarks>
    public required int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Gets the maximum number of function-invocation loop iterations per request, or
    /// <see langword="null"/> to use the upstream <c>FunctionInvokingChatClient</c> default.
    /// </summary>
    public required int? MaximumIterationsPerRequest { get; init; }

    /// <summary>
    /// Gets the <see cref="AgentFileStore"/> that enables the shared file-access provider, or
    /// <see langword="null"/> to leave file access disabled (the upstream default: this dimension
    /// is opt-in, not default-on).
    /// </summary>
    public required AgentFileStore? FileAccessStore { get; init; }

    /// <summary>
    /// Gets the options backing the <c>FileAccessProvider</c> configuration (mapped to
    /// <c>HarnessAgentOptions.FileAccessProviderOptions</c>), or <see langword="null"/> to let the
    /// provider use its own default options. Only meaningful when <see cref="FileAccessStore"/> is
    /// non-<see langword="null"/>; the factory fails closed if this is supplied while
    /// <see cref="FileAccessStore"/> is <see langword="null"/>.
    /// </summary>
    public required FileAccessProviderOptions? FileAccessProviderOptions { get; init; }

    /// <summary>
    /// Gets the <see cref="ChatHistoryProvider"/> backing history persistence (mapped to
    /// <c>HarnessAgentOptions.ChatHistoryProvider</c>), or <see langword="null"/> to use the
    /// upstream default: an <c>InMemoryChatHistoryProvider</c>. When compaction is enabled, the
    /// default provider uses a compaction-based chat reducer backed by either
    /// <see cref="CompactionStrategy"/> or the strategy constructed from
    /// <see cref="MaxContextWindowTokens"/> and <see cref="MaxOutputTokens"/>. When compaction is
    /// disabled, upstream configures the default provider without chat reduction.
    /// </summary>
    public required ChatHistoryProvider? ChatHistoryProvider { get; init; }

    /// <summary>
    /// Gets the <see cref="AgentFileStore"/> backing the file-based session memory provider
    /// (mapped to <c>HarnessAgentOptions.FileMemoryStore</c>), or <see langword="null"/> to use the
    /// upstream default: a <c>FileSystemAgentFileStore</c> rooted at a process-local,
    /// timestamp/guid-qualified directory under the current working directory. Only meaningful
    /// when <see cref="FoundryHarnessFeatureSelections.EnableFileMemory"/> is <see langword="true"/>;
    /// the factory fails closed if this is supplied while that feature is disabled.
    /// </summary>
    public required AgentFileStore? FileMemoryStore { get; init; }

    /// <summary>
    /// Gets the <see cref="AgentSkillsSource"/> backing the agent skills provider (mapped to
    /// <c>HarnessAgentOptions.AgentSkillsSource</c>), or <see langword="null"/> to use the upstream
    /// default: file-based skill discovery rooted at the current working directory. Only
    /// meaningful when <see cref="FoundryHarnessFeatureSelections.EnableAgentSkills"/> is
    /// <see langword="true"/>; the factory fails closed if this is supplied while that feature is
    /// disabled.
    /// </summary>
    public required AgentSkillsSource? AgentSkillsSource { get; init; }

    /// <summary>
    /// Gets the <see cref="ToolApprovalAgentOptions"/> configuring the "don't ask again" tool
    /// auto-approval middleware (mapped to <c>HarnessAgentOptions.ToolApprovalAgentOptions</c>), or
    /// <see langword="null"/> to use the middleware's own default options. Only meaningful when
    /// <see cref="FoundryHarnessFeatureSelections.EnableToolAutoApproval"/> is <see langword="true"/>;
    /// the factory fails closed if this is supplied while that feature is disabled.
    /// </summary>
    public required ToolApprovalAgentOptions? ToolApprovalAgentOptions { get; init; }

    /// <summary>
    /// Gets the <see cref="AgentModeProviderOptions"/> configuring the plan/execute agent-mode
    /// context provider (mapped to <c>HarnessAgentOptions.AgentModeProviderOptions</c>), or
    /// <see langword="null"/> to use the upstream default modes ("plan" and "execute"). Only
    /// meaningful when <see cref="FoundryHarnessFeatureSelections.EnableAgentModeProvider"/> is
    /// <see langword="true"/>; the factory fails closed if this is supplied while that feature is
    /// disabled.
    /// </summary>
    public required AgentModeProviderOptions? AgentModeProviderOptions { get; init; }

    /// <summary>
    /// Gets an explicit in-loop compaction strategy (mapped to
    /// <c>HarnessAgentOptions.CompactionStrategy</c>), or <see langword="null"/> to let the upstream
    /// bundle construct a default <c>ContextWindowCompactionStrategy</c> from
    /// <see cref="MaxContextWindowTokens"/> and <see cref="MaxOutputTokens"/>. When a strategy is
    /// supplied here, <see cref="MaxContextWindowTokens"/> must be <see langword="null"/> because
    /// upstream ignores it. <see cref="MaxOutputTokens"/> may still separately apply as the chat
    /// options' default output-token cap. Only meaningful when
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/>; the
    /// factory fails closed if this is supplied while that feature is disabled.
    /// </summary>
    public required CompactionStrategy? CompactionStrategy { get; init; }

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivitySource"/> name used by the OpenTelemetry
    /// instrumentation wrapper (mapped to <c>HarnessAgentOptions.OpenTelemetrySourceName</c>), or
    /// <see langword="null"/> to use the upstream default source name
    /// ("Experimental.Microsoft.Agents.AI"). Only meaningful when
    /// <see cref="FoundryHarnessFeatureSelections.EnableOpenTelemetry"/> is <see langword="true"/>;
    /// the factory fails closed if this is supplied while that feature is disabled, or if it is a
    /// whitespace-only string.
    /// </summary>
    public required string? OpenTelemetrySourceName { get; init; }

    /// <summary>
    /// Gets additional <see cref="AIContextProvider"/> instances (mapped to
    /// <c>HarnessAgentOptions.AIContextProviders</c>) included in the agent pipeline alongside the
    /// built-in providers. Supply an empty list for none; this list is never <see langword="null"/>
    /// (unlike the nullable backing-object properties above) because upstream applies no default
    /// substitution here; an empty collection and a <see langword="null"/> collection are
    /// equivalent upstream, so this type always uses the non-nullable empty-list spelling.
    /// </summary>
    public required IReadOnlyList<AIContextProvider> AdditionalContextProviders { get; init; }
}
