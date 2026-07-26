using Microsoft.Agents.AI;
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
/// This type composes the official upstream bundle (<c>Microsoft.Agents.AI.Harness</c>) — it is
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
    /// Required (together with <see cref="MaxOutputTokens"/>) when
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/>.
    /// </remarks>
    public required int? MaxContextWindowTokens { get; init; }

    /// <summary>
    /// Gets the maximum number of output tokens the model can generate per response, or
    /// <see langword="null"/> if not applicable.
    /// </summary>
    /// <remarks>
    /// Required (together with <see cref="MaxContextWindowTokens"/>) when
    /// <see cref="FoundryHarnessFeatureSelections.EnableCompaction"/> is <see langword="true"/>.
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
}
