using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessExecutionBindingChatClient(
    IChatClient innerClient,
    HarnessExecutionBinding binding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureValid();
        var response = await base
            .GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        EnsureValid();
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureValid();
        await foreach (var update in base
            .GetStreamingResponseAsync(messages, options, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            EnsureValid();
            yield return update;
        }

        EnsureValid();
    }

    private void EnsureValid()
        => binding.EnsureCurrent(executionContextAccessor, sessionId);
}
