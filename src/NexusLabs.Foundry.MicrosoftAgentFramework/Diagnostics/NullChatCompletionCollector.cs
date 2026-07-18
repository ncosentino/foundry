namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// No-op <see cref="IChatCompletionCollector"/> returned when diagnostics middleware is not wired.
/// </summary>
internal sealed class NullChatCompletionCollector : IChatCompletionCollector
{
    internal static readonly NullChatCompletionCollector Instance = new();

    public IReadOnlyList<ChatCompletionDiagnostics> DrainCompletions() => [];
}
