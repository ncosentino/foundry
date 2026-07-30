using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed class HostedTruncatingChatReducer : IChatReducer
{
    private const int MaximumTextLength = 8000;

    public Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reduced = messages
            .Select(message =>
                message.Text is { Length: > MaximumTextLength } text
                    ? new ChatMessage(message.Role, text[..MaximumTextLength])
                    : message)
            .ToArray();
        return Task.FromResult<IEnumerable<ChatMessage>>(reduced);
    }
}
