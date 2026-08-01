using Microsoft.Extensions.AI;

namespace AotHarnessApp;

/// <summary>
/// A reducer that proposes the messages it was given, unchanged. The AOT scenario exercises the
/// compaction node's construction, trimming, and disposition reporting rather than a reduction
/// outcome, so proposing no change keeps the scripted transcript deterministic.
/// </summary>
internal sealed class AotHarnessPassthroughChatReducer : IChatReducer
{
    public Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken) =>
        Task.FromResult(messages);
}
