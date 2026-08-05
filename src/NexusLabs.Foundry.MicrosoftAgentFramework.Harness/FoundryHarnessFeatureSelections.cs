namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Explicit, required choices for every default-on-but-disableable dimension of the upstream
/// <c>Microsoft.Agents.AI.Harness</c> complete-bundle pipeline (MAF 1.15).
/// </summary>
/// <remarks>
/// <para>
/// Upstream ships every dimension here enabled by default and individually disableable via a
/// <c>Disable*</c> flag on <c>Microsoft.Agents.AI.HarnessAgentOptions</c>. Foundry deliberately
/// requires every property here so a caller can never silently inherit an upstream default without
/// consciously choosing it.
/// </para>
/// <para>
/// Dimensions that upstream cannot disable at all (function invocation, message injection,
/// per-service-call history persistence) are intentionally absent from this type; see
/// <see cref="FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable"/>. The file-access dimension
/// is opt-in via <see cref="FoundryHarnessAgentConfiguration.FileAccessStore"/> and is therefore
/// also absent from this type. Background agent delegation and loop evaluation are not yet exposed
/// by this API candidate; they are reported as limitations in
/// <see cref="FoundryHarnessEffectiveDefaults"/> pending a follow-up API-candidate review.
/// </para>
/// </remarks>
public sealed record FoundryHarnessFeatureSelections
{
    /// <summary>
    /// Gets whether the hosted web search tool is added to the agent's chat options.
    /// Upstream default: enabled.
    /// </summary>
    public required bool EnableWebSearch { get; init; }

    /// <summary>
    /// Gets whether the file-based session memory provider is included in the agent's
    /// context providers. Upstream default: enabled.
    /// </summary>
    public required bool EnableFileMemory { get; init; }

    /// <summary>
    /// Gets whether the file-based agent skills provider is included in the agent's
    /// context providers. Upstream default: enabled.
    /// </summary>
    public required bool EnableAgentSkills { get; init; }

    /// <summary>
    /// Gets whether the "don't ask again" tool auto-approval middleware wraps the agent.
    /// Upstream default: enabled.
    /// </summary>
    public required bool EnableToolAutoApproval { get; init; }

    /// <summary>
    /// Gets whether automatically-approved calls for tools that do not require approval are
    /// bypassed rather than surfaced to the caller. Upstream default: enabled.
    /// </summary>
    public required bool EnableApprovalNotRequiredFunctionBypassing { get; init; }

    /// <summary>
    /// Gets whether inbound tool-approval responses are bound to the model-originated approval
    /// requests the framework surfaced. Upstream default: enabled.
    /// </summary>
    public required bool EnableApprovalResponseBinding { get; init; }

    /// <summary>
    /// Gets whether the agent is wrapped with OpenTelemetry instrumentation following the
    /// Semantic Conventions for Generative AI systems. Upstream default: enabled.
    /// </summary>
    public required bool EnableOpenTelemetry { get; init; }

    /// <summary>
    /// Gets whether the persistent todo-list context provider is included. Upstream default: enabled.
    /// </summary>
    public required bool EnableTodoProvider { get; init; }

    /// <summary>
    /// Gets whether the plan/execute agent-mode context provider is included. Upstream default: enabled.
    /// </summary>
    public required bool EnableAgentModeProvider { get; init; }

    /// <summary>
    /// Gets whether upstream's context-window compaction is enabled, bounding the conversation the
    /// agent carries <em>between</em> turns. The upstream disable flag defaults to
    /// <see langword="false"/>, but compaction is effectively inert until either an explicit
    /// <see cref="FoundryHarnessAgentConfiguration.CompactionStrategy"/> or both token budgets are
    /// supplied. Foundry therefore treats this dimension as explicit opt-in and fails closed when an
    /// enabled configuration supplies neither valid backing form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism.</strong> Upstream's <c>CompactionProvider</c> is an <c>AIContextProvider</c>,
    /// so it is invoked once per agent turn — once per <c>RunAsync</c> — above the tool-invocation
    /// loop, and it reduces the persisted chat-history index. It changes what the agent subsequently
    /// remembers, and its budgets are provider tokens.
    /// </para>
    /// <para>
    /// <strong>It does not bound a tool loop.</strong> A single turn making several model calls is
    /// compacted only against the state preceding the first round. Measured over a two-round tool loop
    /// with an always-firing strategy: consulted once, against a two-message index, never seeing the
    /// round that carried the tool call and its result. Identical on 1.15.0, 1.16.0, and 1.17.0. Tracked in
    /// ncosentino/foundry#73.
    /// </para>
    /// <para>
    /// This and <see cref="EnableHybridCompaction"/> are independent rather than layered: either may be
    /// enabled alone, neither suppresses the other, and enabling hybrid does not extend this
    /// dimension's reach. Enable this one when context grows across many turns.
    /// </para>
    /// </remarks>
    public required bool EnableCompaction { get; init; }

    /// <summary>
    /// Gets whether Foundry's per-provider-call hybrid compaction is enabled, bounding what is
    /// <em>sent</em> on each individual provider request. Requires
    /// <see cref="FoundryHarnessAgentConfiguration.HybridCompactionOptions"/>; supplying one while this
    /// is disabled, or enabling this without one, fails closed before any agent is constructed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Mechanism.</strong> Foundry wraps the supplied chat client at the innermost position.
    /// The function-invocation loop and message injection each call their inner client afresh, so every
    /// intermediate tool round cascades down to this node and is bounded — including the round carrying
    /// a tool call and its result, which <see cref="EnableCompaction"/> never observes. Budgets are
    /// UTF-8 bytes of rendered content rather than provider tokens, so choose one with headroom.
    /// </para>
    /// <para>
    /// <strong>Stored history is not shrunk.</strong> This node is inner to the per-service-call
    /// history decorator, so history is persisted before a reduction is applied and every call
    /// re-assembles from the full record. Reductions are non-destructive but also non-cumulative: the
    /// cost is paid per call and the stored record keeps growing. Enable
    /// <see cref="EnableCompaction"/> alongside this when the record itself needs bounding.
    /// </para>
    /// <para>
    /// A context that cannot be reduced below the configured hard limit fails the request rather than
    /// being forwarded over budget. Enable this one when context grows from tool results inside a
    /// single turn.
    /// </para>
    /// </remarks>
    public required bool EnableHybridCompaction { get; init; }
}
