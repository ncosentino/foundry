using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only <see cref="DelegatingChatClient"/> that records the exact materialized message list
/// observed on every call, then forwards it to its inner client completely unchanged. Positioned
/// outer to a <see cref="HarnessHybridCompactionChatClient"/> under test, this stands in for a
/// persistence-facing component such as the real <c>PerServiceCallChatHistoryPersistingChatClient</c> —
/// proving that whatever such an outer component observes is exactly the actual incoming request
/// messages, since a transient recovered body added at the inner
/// <see cref="HarnessContextSnapshotIntegration"/> seam is never part of that incoming message list at
/// all and therefore can never reach this position.
/// </summary>
internal sealed class HarnessRecordingPassthroughChatClient(IChatClient innerClient)
    : DelegatingChatClient(innerClient)
{
    private readonly List<IReadOnlyList<ChatMessage>> _observedCalls = [];

    /// <summary>The exact materialized message list observed on every call, in call order.</summary>
    internal IReadOnlyList<IReadOnlyList<ChatMessage>> ObservedCalls => _observedCalls;

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        _observedCalls.Add(messages as IReadOnlyList<ChatMessage> ?? messages.ToList());
        return base.GetResponseAsync(messages, options, cancellationToken);
    }
}
