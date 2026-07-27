using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed class HostedOutputCapChatClient(
    IChatClient innerClient,
    int maximumOutputTokens) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();
        options.MaxOutputTokens = Math.Min(
            options.MaxOutputTokens ?? maximumOutputTokens,
            maximumOutputTokens);
        options.Temperature = 0;
        return base.GetResponseAsync(messages, options, cancellationToken);
    }
}
