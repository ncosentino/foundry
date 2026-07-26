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
    /// fails closed if it already carries a function-invocation loop, message-injection
    /// middleware, or OpenTelemetry instrumentation (regardless of
    /// <see cref="FoundryHarnessFeatureSelections.EnableOpenTelemetry"/>), because the upstream
    /// bundle must own the complete pipeline itself.
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
    /// of its own. Duplicate tool names cause <see cref="FoundryHarnessAgentFactory"/> to fail closed.
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
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/>.
    /// </para>
    /// <para>
    /// Must be <see langword="null"/> when <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/>
    /// is <see langword="false"/>. The upstream default <c>InMemoryChatHistoryProvider</c>
    /// constructs a compaction reducer when both <see cref="MaxContextWindowTokens"/> and
    /// <see cref="MaxOutputTokens"/> are present, independently of
    /// <c>HarnessAgentOptions.DisableCompaction</c>. Supplying a context-window budget while
    /// compaction is disabled would leave a compaction-specific budget configured even though
    /// compaction was explicitly disabled. Foundry rejects that ambiguous combination so the
    /// disabled disposition cannot approach the upstream reducer-activation condition.
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
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/>.
    /// May also be supplied when compaction is disabled as a standalone per-response output
    /// cap, without triggering the upstream default history provider's compaction reducer (which
    /// only activates when both token budgets are present).
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
    /// upstream default: an <c>InMemoryChatHistoryProvider</c>, configured with a compaction-based
    /// chat reducer only when both <see cref="MaxContextWindowTokens"/> and
    /// <see cref="MaxOutputTokens"/> are supplied. Under Foundry validation,
    /// <see cref="MaxContextWindowTokens"/> is rejected when
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="false"/>,
    /// so the default provider only has a reducer when compaction is explicitly enabled with both
    /// token budgets; there is no hidden reducer when compaction is disabled.
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
    /// supplied here, those two token budgets are no longer required for compaction purposes (the
    /// strategy is used directly), though <see cref="MaxOutputTokens"/> may still separately apply
    /// as the chat options' default output-token cap. Only meaningful when
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
