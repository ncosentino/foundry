using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The default <see cref="IHarnessContextMessageClassifier"/>: it derives a stable entry id from a
/// message's own content and never overrides the adapter's structural classification. It exists so a
/// caller that has no opinion on classification can still enable hybrid compaction, rather than being
/// forced to author a strategy it does not need.
/// </summary>
/// <remarks>
/// <para>
/// The id is the hex SHA-256 of a canonical rendering of the message's role and content, suffixed with
/// the number of preceding messages that rendered identically. The suffix is required because
/// <see cref="HarnessContextSnapshot.Create"/> rejects duplicate entry ids and a conversation may
/// legitimately repeat a message verbatim. An occurrence ordinal is used rather than the raw index so
/// that ids stay stable when earlier messages are compacted away or a message is prepended, which a
/// positional id would silently invalidate.
/// </para>
/// <para>
/// <see cref="ClassifyOverride"/> always returns <see langword="null"/>. Every kind this classifier
/// could otherwise influence is already derived structurally by
/// <see cref="HarnessMafMessageContextAdapter.Adapt"/>, so deferring is the correct default rather than
/// a guess: a host that genuinely needs a different classification supplies its own strategy.
/// </para>
/// </remarks>
internal sealed class HarnessContentHashContextMessageClassifier : IHarnessContextMessageClassifier
{
    public string ResolveEntryId(
        ChatMessage message,
        int index,
        IReadOnlyList<ChatMessage> allMessages)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(allMessages);

        var canonical = Canonicalize(message);
        var occurrence = 0;
        for (var i = 0; i < index && i < allMessages.Count; i++)
        {
            if (string.Equals(Canonicalize(allMessages[i]), canonical, StringComparison.Ordinal))
            {
                occurrence++;
            }
        }

        var digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{digest}:{occurrence}");
    }

    public HarnessContextEntryKind? ClassifyOverride(
        ChatMessage message,
        int index,
        IReadOnlyList<ChatMessage> allMessages) => null;

    private static string Canonicalize(ChatMessage message)
    {
        var builder = new StringBuilder();
        builder.Append(message.Role.Value).Append('\u001f');
        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent text:
                    builder.Append("text:").Append(text.Text);
                    break;
                case FunctionCallContent call:
                    builder.Append("call:").Append(call.CallId).Append(':').Append(call.Name);
                    break;
                case FunctionResultContent result:
                    builder.Append("result:").Append(result.CallId).Append(':').Append(result.Result);
                    break;
                default:
                    builder.Append("content:").Append(content.GetType().FullName);
                    break;
            }

            builder.Append('\u001e');
        }

        return builder.ToString();
    }
}
