using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// The explicit configuration for Foundry's per-provider-call hybrid compaction, which bounds what is
/// <em>sent</em> on each individual provider request rather than what the agent remembers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Where this acts.</strong> Foundry wraps the configured chat client at the innermost
/// position, beneath everything the bundle installs above it. Both the function-invocation loop and
/// message injection call their inner client afresh for each round, so every intermediate tool round
/// cascades down and is bounded — including the round carrying a tool call and its result, which
/// upstream's per-turn compaction never observes. See
/// <see cref="FoundryHarnessFeature.HybridCompaction"/> and
/// <see cref="FoundryHarnessFeature.Compaction"/> for how the two dimensions differ.
/// </para>
/// <para>
/// <strong>Sizes are UTF-8 bytes of rendered message content, not provider tokens.</strong> Bytes are
/// used because they are computable locally and deterministically without a tokenizer that matches the
/// specific provider; pick a budget with headroom rather than treating it as a token count.
/// </para>
/// <para>
/// <strong>Reductions bound one dispatch, not stored history.</strong> This node is inner to the
/// per-service-call history decorator, so history is persisted before a reduction is applied and every
/// call re-assembles from the full record. Nothing is permanently discarded, and the work is not
/// cumulative.
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
    /// <see cref="HardLimitBytes"/>, so that a positive trigger threshold exists.
    /// </summary>
    public required int TriggerMarginBytes { get; init; }

    /// <summary>
    /// Gets how many of the most recent messages are always preserved and never reduced away.
    /// </summary>
    public required int RecentMessageRetentionCount { get; init; }

    /// <summary>
    /// Gets how many reduction attempts may run for one provider call before the context is declared
    /// irreducible and the call fails. More than one attempt can be needed because a concurrent
    /// injection can invalidate an in-flight proposal, which is discarded and restarted from the
    /// newest snapshot.
    /// </summary>
    public required int MaximumCompactionAttempts { get; init; }

    /// <summary>
    /// Gets the reducer whose output is treated as a compaction <em>proposal</em>, never as ground
    /// truth: Foundry verifies it against <see cref="HardLimitBytes"/> and its own
    /// structural-preservation rules before anything is dispatched, and fails the request rather than
    /// forwarding an over-budget or structurally invalid context.
    /// </summary>
    /// <remarks>
    /// Upstream's <c>CompactionStrategy</c> implementations are not usable here. They operate on
    /// upstream's per-turn message index; this seam reduces the messages of a single provider request
    /// and therefore takes an <see cref="IChatReducer"/>.
    /// </remarks>
    public required IChatReducer UpstreamReducer { get; init; }
}
