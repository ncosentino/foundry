using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace DeclarativeWorkflowApp;

/// <summary>
/// A deterministic chat client standing in for a model, so the example runs offline and always
/// produces the same transcript.
/// </summary>
/// <remarks>
/// Agents built through the agent factory share the one chat client configured on the runtime, so
/// per-agent behavior is derived from the instructions each declared agent carries.
/// </remarks>
internal sealed class ScriptedChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Respond(messages, options))));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reply = Respond(messages, options);
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
    }

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }

    private static string Respond(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var materialized = messages.ToList();
        var role = options?.Instructions
            ?? materialized.FirstOrDefault(m => m.Role.Equals(ChatRole.System))?.Text
            ?? string.Empty;
        var report = materialized.LastOrDefault(m => m.Role.Equals(ChatRole.User))?.Text ?? string.Empty;

        return role switch
        {
            "classifier" => report.Contains("cannot log in", StringComparison.OrdinalIgnoreCase)
                ? "category: access"
                : "category: general",
            "responder" => $"Thanks for reporting: {report}",
            _ => report,
        };
    }
}
