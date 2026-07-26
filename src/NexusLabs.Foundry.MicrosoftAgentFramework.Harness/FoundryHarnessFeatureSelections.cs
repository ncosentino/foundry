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
    /// Gets whether in-loop context-window compaction is enabled. The upstream disable flag defaults
    /// to <see langword="false"/>, but compaction is effectively inert until either an explicit
    /// <see cref="FoundryHarnessAgentConfiguration.CompactionStrategy"/> or both token budgets are
    /// supplied. Foundry therefore treats this dimension as explicit opt-in and fails closed when an
    /// enabled configuration supplies neither valid backing form.
    /// </summary>
    public required bool EnableCompaction { get; init; }
}
