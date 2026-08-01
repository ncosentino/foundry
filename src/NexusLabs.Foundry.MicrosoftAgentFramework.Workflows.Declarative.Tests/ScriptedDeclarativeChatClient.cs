using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// A chat client that answers with a deterministic transformation of the last user message, so
/// declarative workflow tests observe agent participation without a live provider.
/// </summary>
internal sealed class ScriptedDeclarativeChatClient(string responsePrefix) : IChatClient
{
    private readonly List<string> _observedPrompts = [];

    internal IReadOnlyList<string> ObservedPrompts
    {
        get
        {
            lock (_observedPrompts)
            {
                return [.. _observedPrompts];
            }
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = LastUserText(messages);
        lock (_observedPrompts)
        {
            _observedPrompts.Add(prompt);
        }

        return Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, $"{responsePrefix}{prompt}")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var prompt = LastUserText(messages);
        lock (_observedPrompts)
        {
            _observedPrompts.Add(prompt);
        }

        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, $"{responsePrefix}{prompt}");
    }

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }

    private static string LastUserText(IEnumerable<ChatMessage> messages) =>
        messages.LastOrDefault(m => m.Role.Equals(ChatRole.User))?.Text ?? string.Empty;
}
