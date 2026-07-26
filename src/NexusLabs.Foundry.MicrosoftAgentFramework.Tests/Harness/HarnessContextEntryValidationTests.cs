using System.Text.Json;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for the structural shape <see cref="HarnessContextEntry.Create"/> enforces on every kind: tool
/// call/result content is only ever permitted in a <see cref="HarnessContextEntryKind.ToolExchange"/>
/// entry (fail-closed rejection for every other kind, including a reducer-authored
/// <see cref="HarnessContextEntryKind.Summary"/> or <see cref="HarnessContextEntryKind.ConversationalMessage"/>
/// entry), a <see cref="HarnessContextEntryKind.ToolExchange"/> entry may never carry both a call and a
/// result at once, and <see cref="HarnessContextEntry.NormalizeValue"/> has no reflection-based fallback
/// for an unsupported argument/result value type.
/// </summary>
public sealed class HarnessContextEntryValidationTests
{
    // xUnit requires public test-method parameter types to be at least as accessible as the
    // method itself, but HarnessContextEntryKind is internal (by design). The MemberData below
    // therefore boxes the enum as its underlying int value, and each test unboxes it locally.
    public static IEnumerable<object[]> NonToolExchangeKinds()
    {
        yield return [(int)HarnessContextEntryKind.SystemInstruction];
        yield return [(int)HarnessContextEntryKind.AuthoritativeSessionState];
        yield return [(int)HarnessContextEntryKind.ApprovalSecurityState];
        yield return [(int)HarnessContextEntryKind.ArtifactReference];
        yield return [(int)HarnessContextEntryKind.ConversationalMessage];
        yield return [(int)HarnessContextEntryKind.Summary];
    }

    // --- Tool content can never be smuggled in under a non-ToolExchange label ---------------

    [Theory]
    [MemberData(nameof(NonToolExchangeKinds))]
    public void Create_NonToolExchangeKindWithFunctionCallContent_ThrowsArgumentException(int kindValue)
    {
        var kind = (HarnessContextEntryKind)kindValue;
        var message = new ChatMessage(
            ChatRole.Assistant, new List<AIContent> { new FunctionCallContent("call-1", "lookup") });

        Assert.Throws<ArgumentException>(() => HarnessContextEntry.Create("entry", kind, message));
    }

    [Theory]
    [MemberData(nameof(NonToolExchangeKinds))]
    public void Create_NonToolExchangeKindWithFunctionResultContent_ThrowsArgumentException(int kindValue)
    {
        var kind = (HarnessContextEntryKind)kindValue;
        var message = new ChatMessage(ChatRole.Tool, new List<AIContent> { new FunctionResultContent("call-1", "ok") });

        Assert.Throws<ArgumentException>(() => HarnessContextEntry.Create("entry", kind, message));
    }

    [Fact]
    public void Create_SummaryEntrySmugglingFunctionCallContent_ThrowsArgumentException()
    {
        var message = new ChatMessage(
            ChatRole.Assistant, new List<AIContent> { new FunctionCallContent("call-1", "lookup") });

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("summary", HarnessContextEntryKind.Summary, message));
    }

    [Fact]
    public void Create_SummaryEntrySmugglingFunctionResultContent_ThrowsArgumentException()
    {
        var message = new ChatMessage(ChatRole.Tool, new List<AIContent> { new FunctionResultContent("call-1", "ok") });

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("summary", HarnessContextEntryKind.Summary, message));
    }

    [Fact]
    public void Create_ConversationalMessageEntrySmugglingFunctionCallContent_ThrowsArgumentException()
    {
        var message = new ChatMessage(
            ChatRole.Assistant, new List<AIContent> { new FunctionCallContent("call-1", "lookup") });

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("conv", HarnessContextEntryKind.ConversationalMessage, message));
    }

    [Fact]
    public void Create_ConversationalMessageEntrySmugglingFunctionResultContent_ThrowsArgumentException()
    {
        var message = new ChatMessage(ChatRole.Tool, new List<AIContent> { new FunctionResultContent("call-1", "ok") });

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("conv", HarnessContextEntryKind.ConversationalMessage, message));
    }

    // --- A ToolExchange entry has one unambiguous shape: a call, or a result, never both ------

    [Fact]
    public void Create_ToolExchangeWithNeitherCallNorResult_ThrowsArgumentException()
    {
        var message = new ChatMessage(ChatRole.Assistant, "no tool content here");

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("empty-tool", HarnessContextEntryKind.ToolExchange, message));
    }

    [Fact]
    public void Create_ToolExchangeWithBothCallAndResultInSameEntry_ThrowsArgumentException()
    {
        var contents = new List<AIContent>
        {
            new FunctionCallContent("call-1", "lookup"),
            new FunctionResultContent("call-1", "ok"),
        };
        var message = new ChatMessage(ChatRole.Assistant, contents);

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("mixed", HarnessContextEntryKind.ToolExchange, message));
    }

    // --- ToolExchange role coherence: calls must be Assistant, results must be Tool -------------

    [Theory]
    [InlineData("user")]
    [InlineData("system")]
    [InlineData("tool")]
    public void Create_ToolExchangeCallBearingWithNonAssistantRole_ThrowsArgumentException(string roleName)
    {
        var role = new ChatRole(roleName);
        var message = new ChatMessage(role, new List<AIContent> { new FunctionCallContent("call-1", "lookup") });

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("call", HarnessContextEntryKind.ToolExchange, message));
    }

    [Theory]
    [InlineData("user")]
    [InlineData("system")]
    [InlineData("assistant")]
    public void Create_ToolExchangeResultBearingWithNonToolRole_ThrowsArgumentException(string roleName)
    {
        var role = new ChatRole(roleName);
        var message = new ChatMessage(role, new List<AIContent> { new FunctionResultContent("call-1", "ok") });

        Assert.Throws<ArgumentException>(() =>
            HarnessContextEntry.Create("result", HarnessContextEntryKind.ToolExchange, message));
    }

    [Fact]
    public void Create_ToolExchangeCallBearingWithAssistantRole_Succeeds()
    {
        var message = new ChatMessage(
            ChatRole.Assistant, new List<AIContent> { new FunctionCallContent("call-1", "lookup") });

        var entry = HarnessContextEntry.Create("call", HarnessContextEntryKind.ToolExchange, message);

        Assert.Equal(HarnessContextEntryKind.ToolExchange, entry.Kind);
        Assert.Equal(ChatRole.Assistant, entry.Message.Role);
    }

    [Fact]
    public void Create_ToolExchangeResultBearingWithToolRole_Succeeds()
    {
        var message = new ChatMessage(
            ChatRole.Tool, new List<AIContent> { new FunctionResultContent("call-1", "ok") });

        var entry = HarnessContextEntry.Create("result", HarnessContextEntryKind.ToolExchange, message);

        Assert.Equal(HarnessContextEntryKind.ToolExchange, entry.Kind);
        Assert.Equal(ChatRole.Tool, entry.Message.Role);
    }

    // --- CreateRecoverableSegment: no orphan ChatRole.Tool message for transient recovered body -

    [Fact]
    public void CreateRecoverableSegment_NeverProducesOrphanToolRoleMessage_UsesChatRoleUser()
    {
        var rehydratedAtUtc = DateTimeOffset.UtcNow;
        const string body = "recovered artifact body text";
        var reference = HarnessCompactionTestFixture.SampleReference(body, rehydratedAtUtc.AddMinutes(-5));
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, body, rehydratedAtUtc);

        var entry = HarnessContextEntry.CreateRecoverableSegment("recoverable-1", segment);

        Assert.Equal(HarnessContextEntryKind.RecoverableContextSegment, entry.Kind);
        Assert.NotEqual(ChatRole.Tool, entry.Message.Role);
        Assert.NotEqual(ChatRole.System, entry.Message.Role);
        Assert.Equal(ChatRole.User, entry.Message.Role);

        // Raw recovered body must still be available exactly as given, just dispatched under a
        // provider-valid, non-privileged role rather than an orphan tool-role message with no
        // correlating FunctionCallContent/FunctionResultContent pair.
        Assert.Equal(body, entry.Message.Text);
        Assert.DoesNotContain(entry.Message.Contents, content => content is FunctionCallContent or FunctionResultContent);
    }

    // --- NormalizeValue: no reflection-based fallback for an unsupported type ------------------

    private sealed class UnsupportedPayload
    {
        public string Value { get; set; } = "irrelevant";
    }

    [Fact]
    public void Create_FunctionCallArgumentWithUnsupportedCustomObject_ThrowsNotSupportedException()
    {
        var arguments = new Dictionary<string, object?> { ["custom"] = new UnsupportedPayload() };
        var call = new FunctionCallContent("call-1", "lookup", arguments);
        var message = new ChatMessage(ChatRole.Assistant, new List<AIContent> { call });

        Assert.Throws<NotSupportedException>(() =>
            HarnessContextEntry.Create("call", HarnessContextEntryKind.ToolExchange, message));
    }

    [Fact]
    public void Create_FunctionResultWithUnsupportedCustomObject_ThrowsNotSupportedException()
    {
        var resultContent = new FunctionResultContent("call-1", new UnsupportedPayload());
        var message = new ChatMessage(ChatRole.Tool, new List<AIContent> { resultContent });

        Assert.Throws<NotSupportedException>(() =>
            HarnessContextEntry.Create("result", HarnessContextEntryKind.ToolExchange, message));
    }

    [Fact]
    public void NormalizeValue_UnsupportedCustomObject_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => HarnessContextEntry.NormalizeValue(new UnsupportedPayload()));
    }

    // --- Unsupported AIContent shapes fail closed, never passed through by reference ----------

    private sealed class UnsupportedContent : AIContent;

    [Fact]
    public void Create_ConversationalMessageWithUnsupportedContentType_ThrowsNotSupportedException()
    {
        var message = new ChatMessage(ChatRole.Assistant, new List<AIContent> { new UnsupportedContent() });

        Assert.Throws<NotSupportedException>(() =>
            HarnessContextEntry.Create("conv", HarnessContextEntryKind.ConversationalMessage, message));
    }

    [Fact]
    public void Create_SystemInstructionWithUnsupportedContentType_ThrowsNotSupportedException()
    {
        var message = new ChatMessage(ChatRole.System, new List<AIContent> { new UnsupportedContent() });

        Assert.Throws<NotSupportedException>(() =>
            HarnessContextEntry.Create("system", HarnessContextEntryKind.SystemInstruction, message));
    }

    // --- Nested dictionary/list/JsonElement values are still deep-copied and disposal-safe ----

    [Fact]
    public void Create_LaterMutatingNestedListArgument_DoesNotAffectEntry()
    {
        var nestedList = new List<object?> { "item-one", "item-two" };
        var arguments = new Dictionary<string, object?> { ["items"] = nestedList };
        var call = new FunctionCallContent("call-1", "lookup", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        nestedList[0] = "mutated-item";
        nestedList.Add("appended-item");

        var copiedCall = Assert.IsType<FunctionCallContent>(entry.Message.Contents[0]);
        var copiedList = Assert.IsType<List<object?>>(copiedCall.Arguments!["items"]);
        Assert.Equal(["item-one", "item-two"], copiedList);
    }

    [Fact]
    public void Create_ArgumentWithJsonElementValue_SurvivesOriginalDocumentDisposal()
    {
        object? cloned;
        using (var doc = JsonDocument.Parse("""{"key":"value"}"""))
        {
            var arguments = new Dictionary<string, object?> { ["payload"] = doc.RootElement };
            var call = new FunctionCallContent("call-1", "lookup", arguments);
            var entry = HarnessContextEntry.Create(
                "call", HarnessContextEntryKind.ToolExchange,
                new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

            var copiedCall = Assert.IsType<FunctionCallContent>(entry.Message.Contents[0]);
            cloned = copiedCall.Arguments!["payload"];
        }

        var element = Assert.IsType<JsonElement>(cloned);
        Assert.Equal("value", element.GetProperty("key").GetString());
    }

    // --- ArtifactReferenceDigests: defensive copy — never a mutable List<string> ----------------

    /// <summary>
    /// <see cref="HarnessContextEntry.ArtifactReferenceDigests"/> must never expose the mutable
    /// <see cref="List{T}"/> that <c>ExtractResultReferenceDigests</c> builds internally for a
    /// tool-exchange result entry. The private constructor must store a defensive copy (an independently
    /// allocated array) so a caller who casts the returned property back to a mutable collection cannot
    /// alter this entry's own authoritative state.
    /// </summary>
    [Fact]
    public void ToolExchangeResult_ArtifactReferenceDigests_IsNotMutableList()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("mutation-test-content");
        var result = new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest));
        var message = new ChatMessage(ChatRole.Tool, new List<AIContent> { result });
        var entry = HarnessContextEntry.Create("result", HarnessContextEntryKind.ToolExchange, message);

        // Precondition: the entry must have actually extracted a digest so the test is non-vacuous.
        Assert.NotEmpty(entry.ArtifactReferenceDigests);

        // The stored collection is a defensive copy (string[]), not the internal List<string> that
        // ExtractResultReferenceDigests builds; casting must not yield a List<string>.
        Assert.IsNotType<List<string>>(entry.ArtifactReferenceDigests);
    }

    /// <summary>
    /// <see cref="HarnessContextEntry.ArtifactReferenceDigests"/> on an
    /// <see cref="HarnessContextEntryKind.ArtifactReference"/> entry must not be castable to a mutable
    /// collection — the single-element array built by <see cref="HarnessContextEntry.Create"/> must be
    /// stored as an independent defensive copy, never the caller's original collection.
    /// </summary>
    [Fact]
    public void ArtifactReferenceEntry_ArtifactReferenceDigests_IsNotMutableList()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("artifact-ref-immutability-test");
        var message = new ChatMessage(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest));
        var entry = HarnessContextEntry.Create("entry", HarnessContextEntryKind.ArtifactReference, message);

        Assert.NotEmpty(entry.ArtifactReferenceDigests);
        Assert.IsNotType<List<string>>(entry.ArtifactReferenceDigests);
    }

    /// <summary>
    /// <see cref="HarnessContextEntry.Copy"/> must produce an entry whose
    /// <see cref="HarnessContextEntry.ArtifactReferenceDigests"/> is a separately allocated collection,
    /// not the same object reference as the source entry's collection, so a consumer mutating one
    /// boundary copy cannot affect any other boundary copy.
    /// </summary>
    [Fact]
    public void Copy_ArtifactReferenceDigests_IsIndependentInstance_NotSameReference()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("copy-independence-test-content");
        var message = new ChatMessage(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest));
        var entry = HarnessContextEntry.Create("entry", HarnessContextEntryKind.ArtifactReference, message);
        var copy = entry.Copy();

        // Each entry holds its own independently allocated array — not the same reference.
        Assert.False(
            ReferenceEquals(entry.ArtifactReferenceDigests, copy.ArtifactReferenceDigests),
            "Expected Copy() to produce a new ArtifactReferenceDigests instance, not the same reference.");

        // Content must still be equal.
        Assert.Equal(entry.ArtifactReferenceDigests.Count, copy.ArtifactReferenceDigests.Count);
        Assert.Equal(entry.ArtifactReferenceDigests[0], copy.ArtifactReferenceDigests[0]);
    }
}
