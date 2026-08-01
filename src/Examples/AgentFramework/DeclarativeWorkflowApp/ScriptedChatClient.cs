using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace DeclarativeWorkflowApp;

/// <summary>
/// A deterministic chat client standing in for a model, so the example runs offline and always
/// produces the same transcript.
/// </summary>
internal sealed class ScriptedChatClient(Func<string, string> respond) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, respond(LastUserText(messages)))));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, respond(LastUserText(messages)));
    }

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }

    private static string LastUserText(IEnumerable<ChatMessage> messages) =>
        messages.LastOrDefault(m => m.Role.Equals(ChatRole.User))?.Text ?? string.Empty;
}
