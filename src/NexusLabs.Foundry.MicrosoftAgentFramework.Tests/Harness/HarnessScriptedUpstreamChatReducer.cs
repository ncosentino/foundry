using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only <see cref="IChatReducer"/> whose behavior per invocation is fully controlled by an
/// injected callback, mirroring <see cref="HarnessScriptedContextReducer"/> but at the raw
/// <see cref="ChatMessage"/> level the real upstream abstraction operates on.
/// </summary>
internal sealed class HarnessScriptedUpstreamChatReducer : IChatReducer
{
    private readonly Func<IEnumerable<ChatMessage>, CancellationToken, Task<IEnumerable<ChatMessage>>> _callback;

    internal HarnessScriptedUpstreamChatReducer(
        Func<IEnumerable<ChatMessage>, CancellationToken, Task<IEnumerable<ChatMessage>>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    /// <summary>The number of times <see cref="ReduceAsync"/> has been invoked.</summary>
    internal int InvocationCount { get; private set; }

    /// <summary>Builds a reducer that always returns its input messages unchanged.</summary>
    internal static HarnessScriptedUpstreamChatReducer Echo() =>
        new((messages, _) => Task.FromResult(messages));

    public Task<IEnumerable<ChatMessage>> ReduceAsync(
        IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return _callback(messages, cancellationToken);
    }
}
