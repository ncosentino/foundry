using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Deterministic builders for <see cref="HarnessContextEntry"/> fixtures and a default
/// <see cref="HarnessHybridContextPolicy"/> factory shared by
/// <see cref="HarnessCompactionPreservationTests"/>, <see cref="HarnessCompactionSequenceTests"/>, and
/// <see cref="HarnessCompactionMarginTests"/>. Mirrors the explicit, no-optional-parameter construction
/// style used by <see cref="HarnessArtifactTestFixture"/>.
/// </summary>
internal static class HarnessCompactionTestFixture
{
    internal const string DefaultPreservationLabel = "hybrid-preservation-v1";
    internal const int DefaultPreservationVersion = 1;

    internal static HarnessHybridContextPolicy CreatePolicy(
        int hardLimit,
        int triggerMargin,
        int recentMessageRetentionCount,
        int maximumCompactionAttempts,
        IHarnessContextSizeEstimator sizeEstimator) =>
        HarnessHybridContextPolicy.Create(
            hardLimit,
            triggerMargin,
            recentMessageRetentionCount,
            maximumCompactionAttempts,
            DefaultPreservationLabel,
            DefaultPreservationVersion,
            sizeEstimator);

    internal static HarnessContextEntry SystemEntry(string entryId, string text) =>
        HarnessContextEntry.Create(entryId, HarnessContextEntryKind.SystemInstruction, new ChatMessage(ChatRole.System, text));

    internal static HarnessContextEntry AuthoritativeEntry(string entryId, string text) =>
        HarnessContextEntry.Create(
            entryId, HarnessContextEntryKind.AuthoritativeSessionState, new ChatMessage(ChatRole.System, text));

    internal static HarnessContextEntry ApprovalEntry(string entryId, string text) =>
        HarnessContextEntry.Create(
            entryId, HarnessContextEntryKind.ApprovalSecurityState, new ChatMessage(ChatRole.System, text));

    internal static HarnessContextEntry SummaryEntry(string entryId, string text) =>
        HarnessContextEntry.Create(entryId, HarnessContextEntryKind.Summary, new ChatMessage(ChatRole.Assistant, text));

    internal static HarnessContextEntry ConversationalEntry(string entryId, ChatRole role, string text) =>
        HarnessContextEntry.Create(entryId, HarnessContextEntryKind.ConversationalMessage, new ChatMessage(role, text));

    internal static string SampleDigest(string seed) => HarnessArtifactIdentity.ComputeDigest(seed);

    internal static HarnessContextEntry ArtifactEntry(string entryId, string digest) =>
        HarnessContextEntry.Create(
            entryId,
            HarnessContextEntryKind.ArtifactReference,
            new ChatMessage(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest)));

    internal static HarnessContextEntry ArtifactEntryFromRawText(string entryId, string rawText) =>
        HarnessContextEntry.Create(
            entryId, HarnessContextEntryKind.ArtifactReference, new ChatMessage(ChatRole.Tool, rawText));

    internal static HarnessContextEntry ToolCallEntry(string entryId, params (string CallId, string Name)[] calls)
    {
        var contents = calls
            .Select(call => (AIContent)new FunctionCallContent(call.CallId, call.Name, new Dictionary<string, object?>()))
            .ToList();
        return HarnessContextEntry.Create(entryId, HarnessContextEntryKind.ToolExchange, new ChatMessage(ChatRole.Assistant, contents));
    }

    internal static HarnessContextEntry ToolResultEntry(string entryId, params (string CallId, object? Result)[] results)
    {
        var contents = results
            .Select(result => (AIContent)new FunctionResultContent(result.CallId, result.Result))
            .ToList();
        return HarnessContextEntry.Create(entryId, HarnessContextEntryKind.ToolExchange, new ChatMessage(ChatRole.Tool, contents));
    }
}
