using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace HarnessEvaluationApp;

internal sealed class HostedHybridMessageClassifier : IHarnessContextMessageClassifier
{
    public string ResolveEntryId(
        ChatMessage message,
        int index,
        IReadOnlyList<ChatMessage> allMessages)
    {
        var seed = $"{index}|{message.Role.Value}|{message.Text}";
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(seed)))
            .ToLowerInvariant();
        return $"hosted-{digest[..16]}";
    }

    public HarnessContextEntryKind? ClassifyOverride(
        ChatMessage message,
        int index,
        IReadOnlyList<ChatMessage> allMessages) =>
        null;
}
