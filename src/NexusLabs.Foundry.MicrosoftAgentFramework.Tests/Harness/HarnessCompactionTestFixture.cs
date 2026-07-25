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

    /// <summary>
    /// Builds a real-shape <see cref="HarnessContextEntryKind.ToolExchange"/> result entry whose
    /// <see cref="FunctionResultContent.Result"/> is the exact canonical <c>artifact://sha256/{digest}</c>
    /// reference string a real eager-offload tool result actually emits (a bare <see cref="string"/>
    /// payload), simulating G4 offloaded references surfacing inside tool-call history.
    /// </summary>
    internal static HarnessContextEntry ToolResultReferenceEntry(string entryId, string callId, string digest) =>
        ToolResultEntry(entryId, (callId, HarnessArtifactIdentity.BuildReferenceId(digest)));

    /// <summary>
    /// Builds a real-shape <see cref="HarnessContextEntryKind.ToolExchange"/> result entry whose
    /// <see cref="FunctionResultContent.Result"/> is a string-valued <see cref="System.Text.Json.JsonElement"/>
    /// carrying the canonical <c>artifact://sha256/{digest}</c> reference — the shape
    /// <see cref="HarnessContextEntry.NormalizeValue"/> stores a result's string payload as after a
    /// round trip through source-generated JSON serialization.
    /// </summary>
    internal static HarnessContextEntry ToolResultReferenceEntryFromJsonElement(string entryId, string callId, string digest)
    {
        var referenceId = HarnessArtifactIdentity.BuildReferenceId(digest);
        var element = System.Text.Json.JsonSerializer.SerializeToElement(referenceId);
        return ToolResultEntry(entryId, (callId, element));
    }

    internal static HarnessContextEntry OptionalEntry(string entryId, string text) =>
        HarnessContextEntry.Create(entryId, HarnessContextEntryKind.OptionalContext, new ChatMessage(ChatRole.Assistant, text));

    /// <summary>
    /// Builds a <see cref="HarnessArtifactReference"/> for <paramref name="content"/> via the untrusted
    /// reconstruction path (no execution binding required), suitable for pairing with
    /// <see cref="RecoverableSegmentEntry"/> and <see cref="ArtifactEntry"/> in assembler fixtures that
    /// do not otherwise need a full <see cref="HarnessArtifactTestFixture"/>.
    /// </summary>
    internal static HarnessArtifactReference SampleReference(string content, DateTimeOffset createdAtUtc)
    {
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var byteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);
        var workspacePath = HarnessArtifactIdentity.BuildPath(digest);

        return HarnessArtifactReference.Reconstruct(
            workspacePath,
            digest,
            byteSize,
            "test artifact reference",
            "assembler-fixture-user",
            "assembler-fixture-orchestration",
            "assembler-fixture-session",
            "assembler-fixture-tool",
            "assembler-fixture-call",
            createdAtUtc);
    }

    /// <summary>
    /// Builds a <see cref="HarnessContextEntryKind.RecoverableContextSegment"/> entry recovering
    /// <paramref name="reference"/>'s content, via <see cref="HarnessContextEntry.CreateRecoverableSegment"/>.
    /// </summary>
    internal static HarnessContextEntry RecoverableSegmentEntry(
        string entryId, HarnessArtifactReference reference, string body, DateTimeOffset rehydratedAtUtc) =>
        HarnessContextEntry.CreateRecoverableSegment(
            entryId, HarnessArtifactRecoverableContextSegment.Create(reference, body, rehydratedAtUtc));
}
