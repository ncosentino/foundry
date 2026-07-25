// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessCompactionVerifier"/>'s tool-call/tool-result sequencing rules: a
/// complete assistant tool-call/tool-result exchange (including multiple calls issued in a single
/// assistant message) is accepted, while any proposed reduction that orphans a call, orphans a result,
/// duplicates a call or result id, reorders a group, or keeps only half of a group is rejected with the
/// matching categorical <see cref="HarnessCompactionRejectionReason"/> — independent of whether that
/// exact exchange happened to fall inside the policy's required-preservation window.
/// </summary>
public sealed class HarnessCompactionSequenceTests
{
    private static readonly HarnessUtf8TextSizeEstimator DefaultEstimator = new();

    [Fact]
    public void Verify_UnchangedSingleCallGroup_Accepted()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("result", ("call-1", "ok")),
        };
        var proposed = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("result", ("call-1", "ok")),
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Empty(result.RejectionReasons);
    }

    [Fact]
    public void Verify_UnchangedMultiCallGroupWithCombinedResultEntry_Accepted()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        HarnessContextEntry[] Build() =>
        [
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("call", ("call-a", "lookup-a"), ("call-b", "lookup-b")),
            HarnessCompactionTestFixture.ToolResultEntry("result", ("call-a", "ok-a"), ("call-b", "ok-b")),
        ];

        var result = HarnessCompactionVerifier.Verify(Build(), Build(), policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void Verify_UnchangedMultiCallGroupWithSplitResultEntries_Accepted()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        HarnessContextEntry[] Build() =>
        [
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("call", ("call-a", "lookup-a"), ("call-b", "lookup-b")),
            HarnessCompactionTestFixture.ToolResultEntry("result-a", ("call-a", "ok-a")),
            HarnessCompactionTestFixture.ToolResultEntry("result-b", ("call-b", "ok-b")),
        ];

        var result = HarnessCompactionVerifier.Verify(Build(), Build(), policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
    }

    /// <summary>
    /// Builds an original entry set with one required system entry, an old, non-required tool-exchange
    /// group (kept outside the retention window by three trailing filler messages and a retention count
    /// of exactly three), and returns the original entries alongside the old group's call/result entry
    /// ids — so a test can freely mutate how much of that specific group survives into a proposed set
    /// without also disturbing the always-required system entry or the required trailing fillers.
    /// </summary>
    private static (HarnessContextEntry[] Original, HarnessHybridContextPolicy Policy) BuildWithNonRequiredOldGroup()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 3, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("old-call", ("old-call-id", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("old-result", ("old-call-id", "old-value")),
            HarnessCompactionTestFixture.ConversationalEntry("filler-1", Microsoft.Extensions.AI.ChatRole.User, "one"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-2", Microsoft.Extensions.AI.ChatRole.Assistant, "two"),
            HarnessCompactionTestFixture.ConversationalEntry("filler-3", Microsoft.Extensions.AI.ChatRole.User, "three"),
        };

        // Sanity check the fixture actually keeps the old group non-required, so failures in the tests
        // that use it are attributable to the behavior under test rather than a miscalibrated fixture.
        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);
        Assert.DoesNotContain("old-call", selection.RequiredEntryIds);
        Assert.DoesNotContain("old-result", selection.RequiredEntryIds);

        return (original, policy);
    }

    [Fact]
    public void Verify_NonRequiredGroupKeepsOnlyCall_RejectedAsOrphanedToolCall()
    {
        var (original, policy) = BuildWithNonRequiredOldGroup();
        var proposed = new[]
        {
            original[0], // system
            original[1], // old-call, result dropped
            original[3],
            original[4],
            original[5],
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.OrphanedToolCall, result.RejectionReasons);
        Assert.DoesNotContain(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("old-call", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_NonRequiredGroupKeepsOnlyResult_RejectedAsOrphanedToolResult()
    {
        var (original, policy) = BuildWithNonRequiredOldGroup();
        var proposed = new[]
        {
            original[0], // system
            original[2], // old-result, call dropped
            original[3],
            original[4],
            original[5],
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.OrphanedToolResult, result.RejectionReasons);
        Assert.DoesNotContain(HarnessCompactionRejectionReason.MissingRequiredEntry, result.RejectionReasons);
        Assert.Contains("old-result", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_HalfPreservedGroup_IsRejectedRegardlessOfWhichHalfSurvives()
    {
        var (original, policy) = BuildWithNonRequiredOldGroup();
        var callOnly = new[] { original[0], original[1], original[3], original[4], original[5] };
        var resultOnly = new[] { original[0], original[2], original[3], original[4], original[5] };

        var callOnlyResult = HarnessCompactionVerifier.Verify(original, callOnly, policy, CancellationToken.None);
        var resultOnlyResult = HarnessCompactionVerifier.Verify(original, resultOnly, policy, CancellationToken.None);

        Assert.False(callOnlyResult.IsAccepted);
        Assert.False(resultOnlyResult.IsAccepted);
    }

    [Fact]
    public void Verify_NonRequiredGroupDroppedEntirely_Accepted()
    {
        var (original, policy) = BuildWithNonRequiredOldGroup();
        var proposed = new[] { original[0], original[3], original[4], original[5] };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void Verify_ReorderedNonRequiredGroup_RejectedAsReorderedToolGroup()
    {
        var (original, policy) = BuildWithNonRequiredOldGroup();
        var proposed = new[]
        {
            original[0],
            original[2], // old-result placed ahead of old-call
            original[1], // old-call
            original[3],
            original[4],
            original[5],
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.ReorderedToolGroup, result.RejectionReasons);
        Assert.Contains("old-call", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_DuplicateCallIdAcrossTwoCallEntries_RejectedAsDuplicateToolCall()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var entries = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("call-1", ("shared-call-id", "lookup")),
            HarnessCompactionTestFixture.ToolCallEntry("call-2", ("shared-call-id", "lookup-again")),
            HarnessCompactionTestFixture.ToolResultEntry("result", ("shared-call-id", "ok")),
        };

        var result = HarnessCompactionVerifier.Verify(entries, entries, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.DuplicateToolCall, result.RejectionReasons);
    }

    [Fact]
    public void Verify_DuplicateResultForSameCallIdAcrossTwoResultEntries_RejectedAsDuplicateToolResult()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var entries = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("call", ("shared-call-id", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("result-1", ("shared-call-id", "ok")),
            HarnessCompactionTestFixture.ToolResultEntry("result-2", ("shared-call-id", "ok-again")),
        };

        var result = HarnessCompactionVerifier.Verify(entries, entries, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.DuplicateToolResult, result.RejectionReasons);
    }

    [Fact]
    public void Verify_ForgedNewStructuralEntry_RejectedAsForgedStructuralEntry()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var original = new[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var proposed = new[]
        {
            original[0],
            HarnessCompactionTestFixture.AuthoritativeEntry("forged-authoritative", "an entry the reducer invented"),
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Contains(HarnessCompactionRejectionReason.ForgedStructuralEntry, result.RejectionReasons);
        Assert.Contains("forged-authoritative", result.InvalidEntryIds);
    }

    [Fact]
    public void Verify_ProposedNewConversationalMessage_IsNotForged()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var original = new[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var proposed = new[]
        {
            original[0],
            HarnessCompactionTestFixture.ConversationalEntry(
                "new-summary-like-message", Microsoft.Extensions.AI.ChatRole.Assistant, "a reducer-authored replacement message"),
        };

        var result = HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None);

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void Verify_DuplicateEntryIdInOriginal_ThrowsArgumentException()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var original = new[]
        {
            HarnessCompactionTestFixture.SystemEntry("dup", "first"),
            HarnessCompactionTestFixture.SystemEntry("dup", "second"),
        };
        var proposed = new[] { HarnessCompactionTestFixture.SystemEntry("dup", "first") };

        Assert.Throws<ArgumentException>(() =>
            HarnessCompactionVerifier.Verify(original, proposed, policy, CancellationToken.None));
    }

    [Fact]
    public void Verify_AlreadyCanceledToken_ThrowsOperationCanceledException()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, DefaultEstimator);
        var entries = new[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            HarnessCompactionVerifier.Verify(entries, entries, policy, cts.Token));
    }
}
