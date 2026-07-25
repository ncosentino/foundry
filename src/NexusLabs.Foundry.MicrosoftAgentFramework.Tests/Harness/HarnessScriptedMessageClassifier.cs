using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only, content-derived <see cref="IHarnessContextMessageClassifier"/>: entry ids are minted
/// deterministically from a message's role, author, and content shape (never from
/// <see cref="ChatMessage.MessageId"/> or instance identity), and the optional kind override is delegated
/// to an injected callback so a test can script classification overrides without a production default to
/// depend on.
/// </summary>
internal sealed class HarnessScriptedMessageClassifier : IHarnessContextMessageClassifier
{
    private readonly Func<ChatMessage, int, IReadOnlyList<ChatMessage>, HarnessContextEntryKind?>? _classifyOverride;

    internal HarnessScriptedMessageClassifier(
        Func<ChatMessage, int, IReadOnlyList<ChatMessage>, HarnessContextEntryKind?>? classifyOverride = null)
    {
        _classifyOverride = classifyOverride;
    }

    /// <summary>Every index (in adaptation order) this classifier's <see cref="ResolveEntryId"/> was called with, across every call.</summary>
    internal List<int> ResolvedIndices { get; } = [];

    public string ResolveEntryId(ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages)
    {
        ResolvedIndices.Add(index);

        var contentSeed = string.Join(
            '|',
            message.Contents.Select(content => content switch
            {
                TextContent text => $"text:{text.Text}",
                FunctionCallContent call => $"call:{call.CallId}:{call.Name}",
                FunctionResultContent result => $"result:{result.CallId}",
                _ => content.GetType().Name,
            }));

        var seed = $"{message.Role}|{message.AuthorName}|{contentSeed}";
        return $"entry-{HarnessArtifactIdentity.ComputeDigest(seed)}";
    }

    public HarnessContextEntryKind? ClassifyOverride(
        ChatMessage message, int index, IReadOnlyList<ChatMessage> allMessages) =>
        _classifyOverride?.Invoke(message, index, allMessages);
}
