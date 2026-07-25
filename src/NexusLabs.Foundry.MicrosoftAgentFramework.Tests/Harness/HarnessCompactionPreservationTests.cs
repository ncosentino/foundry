// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for the labeled, structural preservation set <see cref="HarnessHybridContextPolicy.SelectRequiredPreservation"/>
/// computes and <see cref="HarnessCompactionVerifier"/> enforces: every system, authoritative-state,
/// approval/security-state, and canonical artifact-reference entry is always required; ordinary
/// reducible conversational history and summaries outside the retention window may disappear; a
/// contradictory summary can never substitute for a dropped authoritative entry; a canonical artifact
/// reference is recognized purely structurally; and a defensively-copied entry is immune to later
/// out-of-band mutation of the original <see cref="ChatMessage"/> and its content.
/// </summary>
public sealed class HarnessCompactionPreservationTests
{
    private static readonly HarnessUtf8ContextSizeEstimator DefaultEstimator = new();

    // --- Labeled preservation set: required kinds + trailing recency units retained --------

    [Fact]
    public void SelectRequiredPreservation_RetainsRequiredKindsAndTrailingRecencyUnits_DropsOldReducibleHistory()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "system instructions"),
            HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "authoritative session state"),
            HarnessCompactionTestFixture.ApprovalEntry("approval", "approval granted"),
            HarnessCompactionTestFixture.ArtifactEntry("artifact", HarnessCompactionTestFixture.SampleDigest("artifact-body")),
            HarnessCompactionTestFixture.SummaryEntry("old-summary", "a reducible summary of earlier turns"),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv-1", ChatRole.User, "old message one"),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv-2", ChatRole.Assistant, "old message two"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("tool-result", ("call-1", "ok")),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv-1", ChatRole.User, "recent message one"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv-2", ChatRole.Assistant, "recent message two"),
        };

        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);

        Assert.Equal(
            new[]
            {
                "system", "authoritative", "approval", "artifact",
                "tool-call", "tool-result", "recent-conv-1", "recent-conv-2",
            },
            selection.RequiredEntryIds);
        Assert.DoesNotContain("old-summary", selection.RequiredEntryIds);
        Assert.DoesNotContain("old-conv-1", selection.RequiredEntryIds);
        Assert.DoesNotContain("old-conv-2", selection.RequiredEntryIds);
    }

    [Fact]
    public void Verify_ReductionDropsOldReducibleHistory_Accepted()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "system instructions"),
            HarnessCompactionTestFixture.SummaryEntry("old-summary", "a reducible summary of earlier turns"),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv-1", ChatRole.User, "old message one"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv-1", ChatRole.User, "recent message one"),
        };
        var proposed = new[] { original[0], original[3] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
    }

    // --- Recent-message retention never splits a tool exchange group -----------------------

    [Fact]
    public void SelectRequiredPreservation_RecentWindowBoundary_NeverSplitsToolExchangeGroup()
    {
        // A retention count of 3 raw trailing entries would otherwise land inside the tool group
        // (keeping only "tool-result", not "tool-call"). The recency-unit rule must keep the whole
        // group together instead of splitting it.
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "system instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("conv-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("tool-result", ("call-1", "ok")),
            HarnessCompactionTestFixture.ConversationalEntry("conv-3", ChatRole.User, "three"),
            HarnessCompactionTestFixture.ConversationalEntry("conv-4", ChatRole.Assistant, "four"),
        };

        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);

        Assert.Contains("tool-call", selection.RequiredEntryIds);
        Assert.Contains("tool-result", selection.RequiredEntryIds);
        Assert.Contains("conv-3", selection.RequiredEntryIds);
        Assert.Contains("conv-4", selection.RequiredEntryIds);
        Assert.DoesNotContain("conv-1", selection.RequiredEntryIds);
        Assert.DoesNotContain("conv-2", selection.RequiredEntryIds);
    }

    [Fact]
    public void Verify_ProposedOutputSplittingRequiredToolGroup_RejectedAsMissingRequiredEntry()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "system instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("conv-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("tool-result", ("call-1", "ok")),
            HarnessCompactionTestFixture.ConversationalEntry("conv-3", ChatRole.User, "three"),
        };
        // Naively drops only the call half of the required group, keeping the result.
        var proposed = new[] { original[0], original[3], original[4] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("tool-call", result.MissingRequiredEntryIds);
    }

    // --- Incomplete tool exchanges are never silently reducible, even old and out of window -----

    [Fact]
    public void SelectRequiredPreservation_OldUnmatchedCallOutsideRecentWindow_IsRequired()
    {
        // Retention count of 1 keeps only the trailing "recent" unit; without the incomplete-group
        // rule, "orphaned-call" (which never has a matching result anywhere) would age out and be
        // silently droppable.
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("orphaned-call", ("orphan-call-id", "lookup")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };

        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);

        Assert.Contains("orphaned-call", selection.RequiredEntryIds);
        Assert.DoesNotContain("filler-1", selection.RequiredEntryIds);
        Assert.DoesNotContain("filler-2", selection.RequiredEntryIds);
    }

    [Fact]
    public void Verify_DroppingOldUnmatchedCall_RejectedAsMissingRequiredEntry()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("orphaned-call", ("orphan-call-id", "lookup")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };
        var proposed = new[] { original[0], original[4] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("orphaned-call", result.MissingRequiredEntryIds);
    }

    [Fact]
    public void Verify_PreservingOldUnmatchedCall_StillRejectedAsOrphanedToolCall_IrreducibleTermination()
    {
        // The exchange is irreparably broken in the original entries themselves (no result ever
        // existed), so requiring it does not make a passing reduction possible: preserving it still
        // fails the proposed entries' own tool-exchange self-consistency check. Dropping it fails the
        // required-preservation check above instead — there is no proposed set that can pass both.
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("orphaned-call", ("orphan-call-id", "lookup")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };
        var proposed = new[] { original[0], original[1], original[4] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.OrphanedToolCall, result.RejectionReasons);
        Assert.Contains("orphaned-call", result.InvalidEntryIds);
        Assert.DoesNotContain(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
    }

    [Fact]
    public void SelectRequiredPreservation_OldOrphanedResultOutsideRecentWindow_IsRequired()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolResultEntry("orphaned-result", ("never-called-id", "value")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };

        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);

        Assert.Contains("orphaned-result", selection.RequiredEntryIds);
    }

    [Fact]
    public void Verify_DroppingOldOrphanedResult_RejectedAsMissingRequiredEntry()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolResultEntry("orphaned-result", ("never-called-id", "value")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };
        var proposed = new[] { original[0], original[4] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("orphaned-result", result.MissingRequiredEntryIds);
    }

    [Fact]
    public void Verify_PreservingOldOrphanedResult_StillRejectedAsOrphanedToolResult_IrreducibleTermination()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolResultEntry("orphaned-result", ("never-called-id", "value")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };
        var proposed = new[] { original[0], original[1], original[4] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.OrphanedToolResult, result.RejectionReasons);
        Assert.Contains("orphaned-result", result.InvalidEntryIds);
        Assert.DoesNotContain(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
    }

    // --- Structured authoritative state wins over a contradictory summary ------------------

    [Fact]
    public void Verify_ReducedOutputDropsAuthoritativeStateButKeepsContradictorySummary_RejectedCategorically()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "authorized-budget=500"),
            HarnessCompactionTestFixture.SummaryEntry(
                "summary", "earlier in the conversation the budget was described as unlimited"),
        };
        var proposed = new[] { original[1] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("authoritative", result.MissingRequiredEntryIds);
    }

    [Fact]
    public void Verify_ApprovalSecurityStateDropped_RejectedCategorically()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.ApprovalEntry("approval", "approved-scope=read-only"),
            HarnessCompactionTestFixture.SummaryEntry("summary", "the user approved full write access earlier"),
        };
        var proposed = new[] { original[1] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("approval", result.MissingRequiredEntryIds);
    }

    [Fact]
    public void Verify_RequiredEntryPreservedUnderSameIdWithDifferentContent_RejectedAsContentMismatch()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[] { HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "authorized-budget=500") };
        var proposed = new[] { HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "authorized-budget=999") };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RequiredEntryContentMismatch, result.RejectionReasons);
        Assert.Contains("authoritative", result.InvalidEntryIds);
    }

    // --- Canonical artifact-reference structural recognition -------------------------------

    [Fact]
    public void HarnessContextEntry_Create_CanonicalArtifactReference_Succeeds()
    {
        var digest = HarnessCompactionTestFixture.SampleDigest("artifact contents");

        var entry = HarnessCompactionTestFixture.ArtifactEntry("artifact", digest);

        Assert.Equal(digest, entry.ArtifactReferenceDigest);
    }

    [Fact]
    public void HarnessContextEntry_Create_ArtifactReferenceWithUppercaseDigest_ThrowsArgumentException()
    {
        var digest = HarnessCompactionTestFixture.SampleDigest("artifact contents").ToUpperInvariant();

        Assert.Throws<ArgumentException>(() =>
            HarnessCompactionTestFixture.ArtifactEntryFromRawText("artifact", $"artifact://sha256/{digest}"));
    }

    [Fact]
    public void HarnessContextEntry_Create_ArtifactReferenceWithTruncatedDigest_ThrowsArgumentException()
    {
        var digest = HarnessCompactionTestFixture.SampleDigest("artifact contents")[..32];

        Assert.Throws<ArgumentException>(() =>
            HarnessCompactionTestFixture.ArtifactEntryFromRawText("artifact", $"artifact://sha256/{digest}"));
    }

    [Fact]
    public void HarnessContextEntry_Create_ArtifactReferenceWithWrongScheme_ThrowsArgumentException()
    {
        var digest = HarnessCompactionTestFixture.SampleDigest("artifact contents");

        Assert.Throws<ArgumentException>(() =>
            HarnessCompactionTestFixture.ArtifactEntryFromRawText("artifact", $"artifact://sha1/{digest}"));
    }

    [Fact]
    public void HarnessContextEntry_Create_BareWorkspacePathInsteadOfReferenceId_ThrowsArgumentException()
    {
        var digest = HarnessCompactionTestFixture.SampleDigest("artifact contents");
        var bareWorkspacePath = HarnessArtifactIdentity.BuildPath(digest);

        Assert.Throws<ArgumentException>(() =>
            HarnessCompactionTestFixture.ArtifactEntryFromRawText("artifact", bareWorkspacePath));
    }

    [Fact]
    public void HarnessContextEntry_Create_ArbitraryUriInsteadOfReferenceId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            HarnessCompactionTestFixture.ArtifactEntryFromRawText("artifact", "https://example.com/artifact"));
    }

    // --- Defensive copies of mutable MEAI objects -------------------------------------------

    [Fact]
    public void Create_LaterMutatingOriginalContentsList_DoesNotAffectEntry()
    {
        var contents = new List<AIContent> { new TextContent("original text") };
        var message = new ChatMessage(ChatRole.Assistant, contents);

        var entry = HarnessContextEntry.Create("entry-1", HarnessContextEntryKind.ConversationalMessage, message);

        contents.Add(new TextContent("appended after entry creation"));

        Assert.Single(entry.Message.Contents);
    }

    [Fact]
    public void Create_LaterMutatingOriginalTextContentInstance_DoesNotAffectEntry()
    {
        var message = new ChatMessage(ChatRole.System, "original text");
        var entry = HarnessContextEntry.Create("system", HarnessContextEntryKind.SystemInstruction, message);

        var originalTextContent = Assert.IsType<TextContent>(message.Contents[0]);
        originalTextContent.Text = "mutated after entry creation";

        Assert.Equal("original text", entry.Message.Text);
    }

    [Fact]
    public void Create_LaterMutatingOriginalFunctionCallContentInstance_DoesNotAffectEntry()
    {
        var arguments = new Dictionary<string, object?> { ["key"] = "original-value" };
        var call = new FunctionCallContent("call-1", "lookup", arguments);
        var message = new ChatMessage(ChatRole.Assistant, new List<AIContent> { call });

        var entry = HarnessContextEntry.Create("call", HarnessContextEntryKind.ToolExchange, message);

        arguments["key"] = "mutated-value";
        call.Exception = new InvalidOperationException("mutated after entry creation");

        var copiedCall = Assert.IsType<FunctionCallContent>(entry.Message.Contents[0]);
        Assert.Equal("original-value", copiedCall.Arguments!["key"]);
        Assert.Null(copiedCall.Exception);
    }

    [Fact]
    public void Create_LaterMutatingOriginalMessageAuthorName_DoesNotAffectEntry()
    {
        var message = new ChatMessage(ChatRole.System, "text");

        var entry = HarnessContextEntry.Create("system", HarnessContextEntryKind.SystemInstruction, message);

        message.AuthorName = "mutated-author-after-creation";

        Assert.Null(entry.Message.AuthorName);
    }

    // --- Retained-entry identity: non-required entries must not be mutated under the same id -----

    [Fact]
    public void Verify_NonRequiredConversationalEntryTextChangedUnderSameId_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv", ChatRole.User, "original text"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.Assistant, "recent"),
        };
        var mutatedOldConv = HarnessContextEntry.Create(
            "old-conv",
            HarnessContextEntryKind.ConversationalMessage,
            new ChatMessage(ChatRole.User, "replaced text"));
        var proposed = new[] { original[0], mutatedOldConv, original[2] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("old-conv", result.InvalidEntryIds);
        Assert.DoesNotContain(HarnessCompactionRejectionReason.RequiredEntryContentMismatch, result.RejectionReasons);
    }

    [Fact]
    public void Verify_NonRequiredToolEntryReclassifiedToSummaryKind_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("old-call", ("old-call-id", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("old-result", ("old-call-id", "ok")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-3", ChatRole.User, "three"),
        };
        var reclassifiedCall = HarnessContextEntry.Create(
            "old-call",
            HarnessContextEntryKind.Summary,
            new ChatMessage(ChatRole.Assistant, "former tool call"));
        var reclassifiedResult = HarnessContextEntry.Create(
            "old-result",
            HarnessContextEntryKind.Summary,
            new ChatMessage(ChatRole.Assistant, "former tool result"));
        var proposed = new[]
        {
            original[0], reclassifiedCall, reclassifiedResult,
            original[3], original[4], original[5],
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("old-call", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_RequiredAuthoritativeKindChangedUnderSameId_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.AuthoritativeEntry("auth", "authorized-budget=500"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.User, "recent"),
        };
        var kindChanged = HarnessContextEntry.Create(
            "auth",
            HarnessContextEntryKind.Summary,
            new ChatMessage(ChatRole.System, "authorized-budget=500"));
        var proposed = new[] { kindChanged, original[1] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("auth", result.InvalidEntryIds);
        Assert.DoesNotContain(HarnessCompactionRejectionReason.RequiredEntryContentMismatch, result.RejectionReasons);
    }

    [Fact]
    public void Verify_NonRequiredToolEntryFunctionCallArgumentChangedUnderSameId_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("old-call", ("old-call-id", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("old-result", ("old-call-id", "ok")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-3", ChatRole.User, "three"),
        };
        var changedCallContent = new FunctionCallContent(
            "old-call-id", "lookup", new Dictionary<string, object?> { ["param"] = "changed-value" });
        var changedCallEntry = HarnessContextEntry.Create(
            "old-call",
            HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { changedCallContent }));
        var proposed = new[]
        {
            original[0], changedCallEntry, original[2],
            original[3], original[4], original[5],
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("old-call", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_NonRequiredToolEntryFunctionResultPayloadChangedUnderSameId_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("old-call", ("old-call-id", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("old-result", ("old-call-id", "original-payload")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-3", ChatRole.User, "three"),
        };
        var changedResultContent = new FunctionResultContent("old-call-id", "changed-payload");
        var changedResultEntry = HarnessContextEntry.Create(
            "old-result",
            HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Tool, new List<AIContent> { changedResultContent }));
        var proposed = new[]
        {
            original[0], original[1], changedResultEntry,
            original[3], original[4], original[5],
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("old-result", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_NonRequiredEntryRoleChangedUnderSameId_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.Assistant, "recent"),
        };
        var roleChanged = HarnessContextEntry.Create(
            "old-conv",
            HarnessContextEntryKind.ConversationalMessage,
            new ChatMessage(ChatRole.Assistant, "old message"));
        var proposed = new[] { original[0], roleChanged, original[2] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("old-conv", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_NonRequiredEntryAuthorNameChangedUnderSameId_RejectedAsRetainedOriginalEntryMutated()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessContextEntry.Create(
                "old-conv",
                HarnessContextEntryKind.ConversationalMessage,
                new ChatMessage(ChatRole.User, "message") { AuthorName = "alice" }),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.Assistant, "recent"),
        };
        var authorChanged = HarnessContextEntry.Create(
            "old-conv",
            HarnessContextEntryKind.ConversationalMessage,
            new ChatMessage(ChatRole.User, "message") { AuthorName = "bob" });
        var proposed = new[] { original[0], authorChanged, original[2] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.RetainedOriginalEntryMutated, result.RejectionReasons);
        Assert.Contains("old-conv", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_NewIdSummaryEntry_Accepted()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var newSummary = HarnessContextEntry.Create(
            "brand-new-summary",
            HarnessContextEntryKind.Summary,
            new ChatMessage(ChatRole.Assistant, "reducer-authored summary of removed history"));
        var proposed = new[] { original[0], newSummary };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
    }

    // --- Defensive copy: nested argument and result mutation must not affect the entry --------

    [Fact]
    public void Create_LaterMutatingNestedArgumentDictionary_DoesNotAffectEntry()
    {
        var nestedDict = new Dictionary<string, object?> { ["inner-key"] = "inner-value" };
        var arguments = new Dictionary<string, object?> { ["nested"] = nestedDict };
        var call = new FunctionCallContent("call-1", "lookup", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        nestedDict["inner-key"] = "mutated-inner-value";

        var copiedCall = Assert.IsType<FunctionCallContent>(entry.Message.Contents[0]);
        var copiedNested = Assert.IsType<Dictionary<string, object?>>(copiedCall.Arguments!["nested"]);
        Assert.Equal("inner-value", copiedNested["inner-key"]);
    }

    [Fact]
    public void Create_LaterMutatingResultDictionary_DoesNotAffectEntry()
    {
        var resultDict = new Dictionary<string, object?> { ["key"] = "original-value" };
        var resultContent = new FunctionResultContent("call-1", resultDict);
        var entry = HarnessContextEntry.Create(
            "result", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Tool, new List<AIContent> { resultContent }));

        resultDict["key"] = "mutated-value";

        var copiedResult = Assert.IsType<FunctionResultContent>(entry.Message.Contents[0]);
        var copiedResultDict = Assert.IsType<Dictionary<string, object?>>(copiedResult.Result);
        Assert.Equal("original-value", copiedResultDict["key"]);
    }

    [Fact]
    public void Verify_Accepted_FallbackEntryIdsMatchRequiredPreservationSelection()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent"),
        };
        var expectedFallback = policy.SelectRequiredPreservation(original, CancellationToken.None).RequiredEntryIds;

        var result = HarnessCompactionVerifier.Verify(original, original, policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Equal(expectedFallback, result.PreservationOnlyFallbackEntryIds);
    }

    [Fact]
    public void Verify_Rejected_StillExposesDeterministicFallbackEntryIds()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new[] { HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "state") };
        var expectedFallback = policy.SelectRequiredPreservation(original, CancellationToken.None).RequiredEntryIds;
        var proposed = Array.Empty<HarnessContextEntry>();

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(expectedFallback, result.PreservationOnlyFallbackEntryIds);
    }
}
