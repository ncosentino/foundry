using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace DeclarativeMcpWorkflowApp;

/// <summary>
/// A deterministic chat client standing in for a model, so the only thing this example reaches over
/// a network is the MCP server it is demonstrating.
/// </summary>
internal sealed class ScriptedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Respond(messages))));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reply = Respond(messages);
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
    }

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }

    private static string Respond(IEnumerable<ChatMessage> messages)
    {
        var request = messages
            .LastOrDefault(m => m.Role.Equals(ChatRole.User))?.Text ?? string.Empty;

        return $"The MCP server was asked to echo: {request}";
    }
}
