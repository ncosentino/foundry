using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative.Tests;

/// <summary>
/// A deterministic chat client shared by every declared test agent, which answers using the
/// instructions the invoking agent supplied so a test can tell which agent ran.
/// </summary>
/// <remarks>
/// Agents built through <see cref="IAgentFactory"/> share the one chat client configured on the
/// runtime builder, so per-agent behavior has to be derived from something the agent itself carries.
/// Each declared test agent's <c>Instructions</c> act as its tag, which is why they are single words
/// rather than prose.
/// </remarks>
internal sealed class ScriptedDeclarativeChatClient : IChatClient
{
    private readonly List<(string Tag, string Prompt)> _invocations = [];

    internal IReadOnlyList<(string Tag, string Prompt)> Invocations
    {
        get
        {
            lock (_invocations)
            {
                return [.. _invocations];
            }
        }
    }

    internal IReadOnlyList<string> PromptsFor(string tag) =>
        [.. Invocations.Where(i => i.Tag == tag).Select(i => i.Prompt)];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var invocation = Record(messages, options);
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, CreateContent(invocation))));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var invocation = Record(messages, options);
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, CreateContent(invocation));
    }

    public object? GetService(Type serviceType, object? key) => null;

    public void Dispose()
    {
    }

    private (string Tag, string Prompt) Record(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options)
    {
        var materialized = messages.ToList();
        var tag = options?.Instructions
            ?? materialized.FirstOrDefault(m => m.Role.Equals(ChatRole.System))?.Text
            ?? "<untagged>";
        var prompt = materialized.LastOrDefault(m => m.Role.Equals(ChatRole.User))?.Text ?? string.Empty;

        lock (_invocations)
        {
            _invocations.Add((tag, prompt));
        }

        return (tag, prompt);
    }

    private static IList<AIContent> CreateContent((string Tag, string Prompt) invocation) =>
        invocation.Tag == "failed"
            ?
            [
                new ErrorContent("The scripted agent failed.")
                {
                    ErrorCode = "server_error",
                },
            ]
            : [new TextContent($"{invocation.Tag}:{invocation.Prompt}")];
}
