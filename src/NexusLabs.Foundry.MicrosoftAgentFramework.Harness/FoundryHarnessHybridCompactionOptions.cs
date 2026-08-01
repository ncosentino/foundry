using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// The explicit configuration for Foundry's per-provider-call hybrid compaction, which bounds the exact
/// message set dispatched for <em>every</em> provider request rather than once per agent turn.
/// </summary>
/// <remarks>
/// <para>
/// Sizes are measured in UTF-8 bytes of rendered message content, not provider tokens. Bytes are used
/// because they are computable locally and deterministically without a tokenizer that matches the
/// specific provider; pick a budget with headroom rather than treating it as a token count.
/// </para>
/// <para>
/// <see cref="UpstreamReducer"/> proposes a reduction and is never trusted as ground truth: Foundry
/// verifies the proposal against <see cref="HardLimitBytes"/> and its own structural-preservation rules,
/// and fails the request rather than forwarding an over-budget or structurally invalid context.
/// </para>
/// </remarks>
public sealed record FoundryHarnessHybridCompactionOptions
{
    /// <summary>
    /// Gets the absolute ceiling, in UTF-8 bytes, for the assembled context of a single provider call.
    /// A call whose context cannot be reduced below this fails rather than being forwarded.
    /// </summary>
    public required int HardLimitBytes { get; init; }

    /// <summary>
    /// Gets the margin, in UTF-8 bytes, below <see cref="HardLimitBytes"/> at which compaction begins.
    /// Compaction triggers once assembled context exceeds
    /// <c>HardLimitBytes - TriggerMarginBytes</c>. Must be strictly less than
    /// <see cref="HardLimitBytes"/>.
    /// </summary>
    public required int TriggerMarginBytes { get; init; }

    /// <summary>
    /// Gets how many of the most recent messages are always preserved and never reduced away.
    /// </summary>
    public required int RecentMessageRetentionCount { get; init; }

    /// <summary>
    /// Gets how many reduction attempts may run for one provider call before the context is declared
    /// irreducible and the call fails.
    /// </summary>
    public required int MaximumCompactionAttempts { get; init; }

    /// <summary>
    /// Gets the reducer whose output is treated as a compaction <em>proposal</em>, always verified
    /// before use. Upstream's compaction strategies are not usable here because they operate on
    /// upstream's per-turn index rather than on a single provider request.
    /// </summary>
    public required IChatReducer UpstreamReducer { get; init; }
}
