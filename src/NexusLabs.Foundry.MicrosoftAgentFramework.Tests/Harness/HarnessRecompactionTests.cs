// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessContextAssembler"/>'s bounded recompaction loop and deterministic
/// fallback: an unchanged, growing, or verifier-rejected reducer proposal is never forwarded and
/// consumes an attempt before falling back; a proposal that strictly reduces the estimated size is
/// forwarded once it fits, or recompacted again up to the configured attempt bound; the exact
/// <c>&lt;= HardLimit</c> boundary is honored; recoverable rehydrated bodies are evicted ahead of the
/// reducer while their durable artifact reference survives; optional context is included in the
/// deterministic fallback only after the reducer stage fails and only when it still fits; and every
/// preserved required kind (system, authoritative, approval, artifact reference, tool exchange) plus
/// tool-call/result sequencing remains valid in the final output.
/// </summary>
public sealed class HarnessRecompactionTests
{
    private static readonly HarnessFixedSizeContextEstimator NoOpEstimator = new(new Dictionary<string, int>());

    // --- Unchanged / growing / invalid reducer output is never forwarded -------------------

    [Fact]
    public async Task AssembleAsync_UnchangedReducerOutput_FallbackFits_ReturnsPreservationFallback()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90, ["recent"] = 20 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.FinalEntries);
        Assert.Equal(["system", "recent"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
    }

    [Fact]
    public async Task AssembleAsync_GrowingReducerOutput_FallbackFits_ReturnsPreservationFallback()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["old"] = 90, ["recent"] = 20, ["grown"] = 10,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> Grow(HarnessContextReductionRequest request, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HarnessContextEntry>>(
                [.. request.Entries, HarnessCompactionTestFixture.ConversationalEntry("grown", ChatRole.Assistant, "grown")]);

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, Grow);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.Equal(["system", "recent"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(1, reducer.InvocationCount);
    }

    [Fact]
    public async Task AssembleAsync_InvalidReducerOutputMissingRequiredEntry_FallbackFits_ReturnsPreservationFallback()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90, ["recent"] = 20 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> DropRequired(HarnessContextReductionRequest request, CancellationToken ct) =>
            Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "system"));

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, DropRequired);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.Equal(["system", "recent"], result.FinalEntries!.Select(e => e.EntryId));
    }

    [Fact]
    public async Task AssembleAsync_UnchangedReducerOutput_FallbackTooLarge_ReturnsIrreducible()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90, ["recent"] = 20 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(40, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Null(result.FinalVerification);
        Assert.Equal(["system", "recent"], result.RequiredEntryIds);
    }

    // --- Valid, strictly-reducing proposals recompact up to the attempt bound --------------

    [Fact]
    public async Task AssembleAsync_ValidReducingProposal_RecompactsAcrossAttempts_ReturnsReduced()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["old1"] = 90, ["old2"] = 90, ["recent"] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 3, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old1", ChatRole.User, "old-1"),
            HarnessCompactionTestFixture.ConversationalEntry("old2", ChatRole.User, "old-2"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> RemoveOneOldEntryPerAttempt(
            HarnessContextReductionRequest request, CancellationToken ct)
        {
            var stillHasOld2 = request.Entries.Any(e => e.EntryId == "old2");
            var proposal = HarnessAssemblerTestFixture.Without(request.Entries, stillHasOld2 ? "old2" : "old1");
            return Task.FromResult(proposal);
        }

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, RemoveOneOldEntryPerAttempt);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(["system", "recent"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(2, reducer.InvocationCount);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(50, result.FinalEstimatedSize);
    }

    [Fact]
    public async Task AssembleAsync_ValidReducingProposal_ExhaustsAttemptBudgetStillOverBudget_FallsBackAndSucceeds()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["old1"] = 90, ["old2"] = 90, ["old3"] = 90, ["recent"] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old1", ChatRole.User, "old-1"),
            HarnessCompactionTestFixture.ConversationalEntry("old2", ChatRole.User, "old-2"),
            HarnessCompactionTestFixture.ConversationalEntry("old3", ChatRole.User, "old-3"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> RemoveOneOldEntryPerAttempt(
            HarnessContextReductionRequest request, CancellationToken ct)
        {
            var oldestRemaining = new[] { "old3", "old2", "old1" }.First(id => request.Entries.Any(e => e.EntryId == id));
            return Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, oldestRemaining));
        }

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, RemoveOneOldEntryPerAttempt);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.Equal(["system", "recent"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(2, reducer.InvocationCount);
        Assert.Equal(2, result.AttemptCount);
    }

    // --- Exact hard-limit boundary -----------------------------------------------------------

    [Fact]
    public async Task AssembleAsync_EstimatedSizeExactlyAtHardLimit_InvokesReducerAndPreservesWithinLimit()
    {
        // At the exact hard limit, the entries are also at/above the trigger threshold (margin=10
        // means threshold=90 <= 100). No recoverable body exists to evict, so per the corrected
        // trigger-margin contract the reducer must still be invoked — reaching the trigger always
        // causes an actual pressure-handling attempt, never an immediate WithinLimit short-circuit
        // merely because the size is already dispatch-eligible. Because the reducer proposes the
        // entries back unchanged (non-reducing) and the current entries already fit the hard limit,
        // the assembler preserves them as a successful WithinLimit result after recording the attempt.
        var sizes = new Dictionary<string, int> { ["system"] = 100 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.WithinLimit, result.Outcome);
        Assert.Equal(100, result.FinalEstimatedSize);
        Assert.Equal(100, result.HardLimit);
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Contains(HarnessContextAssemblyStage.ReducerAttempt, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_EstimatedSizeExactlyAtTriggerThreshold_InvokesReducer()
    {
        // Exactly at the trigger threshold (HardLimit - TriggerMargin), strictly under the hard
        // limit, and no recoverable body to evict: pressure handling must still make an actual
        // reducer invocation rather than treating the trigger as a no-op because the size already
        // fits the hard limit.
        var sizes = new Dictionary<string, int> { ["system"] = 90 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.WithinLimit, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Contains(HarnessContextAssemblyStage.ReducerAttempt, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_EstimatedSizeOneAboveTriggerThreshold_InvokesReducer()
    {
        // One unit above the trigger threshold: still triggered, still under the hard limit, and
        // still no recoverable body — the reducer must still be invoked.
        var sizes = new Dictionary<string, int> { ["system"] = 91 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.WithinLimit, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Contains(HarnessContextAssemblyStage.ReducerAttempt, result.Stages);
    }

    // --- Recoverable segment eviction ahead of the reducer ----------------------------------

    [Fact]
    public async Task AssembleAsync_RecoverableSegmentPresent_EvictedBeforeReducerInvoked_ReferencePreserved()
    {
        var reference = HarnessCompactionTestFixture.SampleReference("artifact body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int> { ["system"] = 20, ["artifact-ref"] = 10, ["recoverable"] = 200 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(50, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ArtifactEntry("artifact-ref", reference.ContentDigest),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "recoverable", reference, "the recovered body", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(["system", "artifact-ref"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(0, reducer.InvocationCount);
        Assert.Equal(30, result.FinalEstimatedSize);
        Assert.Contains(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
    }

    // --- Optional context: dropped only after earlier stages, never substitutes for required -

    [Fact]
    public async Task AssembleAsync_OptionalContextFits_IncludedInExtendedFallback()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["approval"] = 20, ["optional"] = 40, ["filler"] = 50,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ApprovalEntry("approval", "approved"),
            HarnessCompactionTestFixture.OptionalEntry("optional", "nice to have"),
            // Summary entries are never recency units, so "filler" cannot become an implicitly
            // required recent-message entry here; only "optional" competes for the fallback's
            // extended-candidate slot.
            HarnessCompactionTestFixture.SummaryEntry("filler", "filler"),
        };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.Equal(["system", "approval", "optional"], result.FinalEntries!.Select(e => e.EntryId));
    }

    [Fact]
    public async Task AssembleAsync_OptionalContextTooLarge_DroppedAfterExtendedFallbackFails()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["approval"] = 20, ["optional"] = 40, ["filler"] = 50,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(60, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ApprovalEntry("approval", "approved"),
            HarnessCompactionTestFixture.OptionalEntry("optional", "nice to have"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "filler"),
        };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.Equal(["system", "approval"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.DoesNotContain("optional", result.FinalEntries!.Select(e => e.EntryId));
    }

    [Fact]
    public async Task AssembleAsync_OptionalContextCannotSubstituteForRequired_RequiredAloneOverBudget_ReturnsIrreducible()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 60, ["optional"] = 5 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(40, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.OptionalEntry("optional", "nice to have"),
        };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
    }

    // --- Preservation of every required kind, and tool sequence validity, in the final output -

    [Fact]
    public async Task AssembleAsync_MixedRequiredKindsAndToolExchange_AllPreservedInOrderAfterReduction()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 20,
            ["authoritative"] = 20,
            ["approval"] = 20,
            ["artifact-ref"] = 10,
            ["old-filler"] = 90,
            ["tool-call"] = 15,
            ["tool-result"] = 15,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var reference = HarnessCompactionTestFixture.SampleReference("referenced content", DateTimeOffset.UnixEpoch);
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "decisions"),
            HarnessCompactionTestFixture.ApprovalEntry("approval", "approved"),
            HarnessCompactionTestFixture.ArtifactEntry("artifact-ref", reference.ContentDigest),
            HarnessCompactionTestFixture.ConversationalEntry("old-filler", ChatRole.User, "an old message"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("tool-result", ("call-1", "ok")),
        };

        Task<IReadOnlyList<HarnessContextEntry>> DropOldFiller(HarnessContextReductionRequest request, CancellationToken ct) =>
            Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "old-filler"));

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, DropOldFiller);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(
            ["system", "authoritative", "approval", "artifact-ref", "tool-call", "tool-result"],
            result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(100, result.FinalEstimatedSize);
        Assert.NotNull(result.FinalVerification);
        Assert.True(result.FinalVerification!.IsAccepted);
    }

    // --- Deterministic fallback ordering -----------------------------------------------------

    [Fact]
    public async Task AssembleAsync_Fallback_PreservesOriginalRelativeOrderOfSurvivingEntries()
    {
        var sizes = new Dictionary<string, int>
        {
            ["filler-1"] = 90,
            ["authoritative"] = 20,
            ["filler-2"] = 90,
            ["system"] = 20,
            ["filler-3"] = 90,
            ["approval"] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            // Summary-kind filler is used (rather than ConversationalMessage) so none of it is ever
            // pulled in as an implicitly required recent-message recency unit — only the three
            // required-kind entries below survive the fallback, and this test asserts they come back
            // in their original relative order.
            HarnessCompactionTestFixture.SummaryEntry("filler-1", "one"),
            HarnessCompactionTestFixture.AuthoritativeEntry("authoritative", "decisions"),
            HarnessCompactionTestFixture.SummaryEntry("filler-2", "two"),
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler-3", "three"),
            HarnessCompactionTestFixture.ApprovalEntry("approval", "approved"),
        };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.Equal(["authoritative", "system", "approval"], result.FinalEntries!.Select(e => e.EntryId));
    }

    // --- Reducer exceptions propagate; never broadly swallowed -----------------------------

    [Fact]
    public async Task AssembleAsync_ReducerThrows_ExceptionPropagatesUnwrapped()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(50, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> Throwing(HarnessContextReductionRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("reducer failure");

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, Throwing);

        await Assert.ThrowsAsync<InvalidOperationException>(() => assembler.AssembleAsync(CancellationToken.None));
    }

    // --- Cancellation at deterministic checkpoints ------------------------------------------

    [Fact]
    public async Task AssembleAsync_AlreadyCanceledToken_ThrowsBeforeCapturingSnapshot()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, NoOpEstimator);
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var (assembler, provider, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => assembler.AssembleAsync(cts.Token));

        Assert.Equal(0, provider.CaptureCount);
        Assert.Equal(0, reducer.InvocationCount);
    }

    [Fact]
    public async Task AssembleAsync_TokenCanceledDuringReducer_ThrowsImmediatelyAfterReducerReturns()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(50, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
        };
        using var cts = new CancellationTokenSource();

        Task<IReadOnlyList<HarnessContextEntry>> CancelThenReturn(HarnessContextReductionRequest request, CancellationToken ct)
        {
            cts.Cancel();
            return Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "old"));
        }

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, CancelThenReturn);

        await Assert.ThrowsAsync<OperationCanceledException>(() => assembler.AssembleAsync(cts.Token));

        Assert.Equal(1, reducer.InvocationCount);
    }

    [Fact]
    public async Task AssembleAsync_TokenCanceledDuringVersionRecheckCapture_ThrowsBeforeVerifier()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(50, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
        };
        using var cts = new CancellationTokenSource();

        var (assembler, provider, _) = HarnessAssemblerTestFixture.Build(
            policy, entries,
            (request, ct) => Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "old")));

        provider.OnCapture = () =>
        {
            // The first capture is the initial snapshot before the reducer runs; the second is the
            // post-reducer version-recheck capture this test targets.
            if (provider.CaptureCount == 1)
            {
                cts.Cancel();
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => assembler.AssembleAsync(cts.Token));
    }

    // --- Trigger-margin gating: below/at/above threshold ----------------------------------

    [Fact]
    public async Task AssembleAsync_BelowTriggerThreshold_RecoverableBodyNotEvicted_ReturnsWithinLimit()
    {
        // Strictly below the trigger threshold: the assembler must not evict recoverable bodies
        // or invoke the reducer. A rehydrated body present in the context must remain present in
        // the result — it should only be evicted during pressure-handling, which starts at/above
        // the trigger threshold.
        var reference = HarnessCompactionTestFixture.SampleReference("artifact body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["artifact-ref"] = 5, ["recoverable"] = 20 };
        // total=35 < threshold(100-40=60) → strictly below trigger
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 40, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ArtifactEntry("artifact-ref", reference.ContentDigest),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "recoverable", reference, "the recovered body", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.WithinLimit, result.Outcome);
        Assert.True(result.IsSuccess);
        // The recoverable body must still be present: below-trigger never evicts
        Assert.Equal(
            ["system", "artifact-ref", "recoverable"],
            result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(0, reducer.InvocationCount);
        Assert.DoesNotContain(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_AtTriggerThreshold_RecoverableBodyEvictedBeforeReducer_ReferencePreserved()
    {
        // Exactly at the trigger threshold: pressure handling starts; the recoverable body must
        // be evicted ahead of any reducer invocation. The durable ArtifactReference entry for
        // the same digest must remain.
        var reference = HarnessCompactionTestFixture.SampleReference("artifact body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["artifact-ref"] = 5, ["recoverable"] = 45 };
        // total=60 = threshold(100-40=60) → exactly at trigger → triggered
        // After eviction: system=10 + artifact-ref=5 = 15 ≤ 100 → success, no reducer
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 40, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ArtifactEntry("artifact-ref", reference.ContentDigest),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "recoverable", reference, "the recovered body", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.True(result.IsSuccess);
        // Recoverable body evicted; durable artifact reference retained
        Assert.Equal(["system", "artifact-ref"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(0, reducer.InvocationCount);
        Assert.Contains(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
        Assert.DoesNotContain(HarnessContextAssemblyStage.ReducerAttempt, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_AboveTriggerButWithinHardLimit_NoRecoverableBody_ReturnsWithinLimit()
    {
        // Above trigger threshold but still within the hard limit, with no recoverable body to
        // evict: no eviction occurs, so margin is not restored and the reducer must still be
        // invoked (an actual pressure-handling attempt), even though the size already fits the
        // hard limit. Because the reducer is non-reducing, the current context is preserved as a
        // successful WithinLimit result after recording the attempt.
        var sizes = new Dictionary<string, int> { ["system"] = 95 };
        // total=95 >= threshold(100-10=90) → triggered; no eviction possible → reducer invoked
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.WithinLimit, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(["system"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Contains(HarnessContextAssemblyStage.ReducerAttempt, result.Stages);
    }

    // --- ReducerAttempt stage: recorded once per reducer invocation, in order ---------------

    [Fact]
    public async Task AssembleAsync_ReducerInvoked_RecordsReducerAttemptStageOncePerInvocation()
    {
        // Each reducer invocation must record exactly one ReducerAttempt stage entry, ordered
        // after the initial SnapshotCaptured and immediately before the post-reducer
        // SnapshotCaptured (version recheck).
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90, ["recent"] = 20 };
        // total=140 >= threshold(100-10=90) → triggered; 140 > 100 → one reducer attempt
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(1, reducer.InvocationCount);
        // Exactly one ReducerAttempt stage for one invocation
        Assert.Equal(1, result.Stages.Count(s => s == HarnessContextAssemblyStage.ReducerAttempt));

        // ReducerAttempt is sandwiched: SnapshotCaptured → ReducerAttempt → SnapshotCaptured
        var stageList = result.Stages.ToList();
        var reducerIdx = stageList.IndexOf(HarnessContextAssemblyStage.ReducerAttempt);
        Assert.True(reducerIdx > 0, "ReducerAttempt must not be the first stage.");
        Assert.Equal(HarnessContextAssemblyStage.SnapshotCaptured, stageList[reducerIdx - 1]);
        Assert.True(reducerIdx < stageList.Count - 1, "A stage must follow ReducerAttempt.");
        Assert.Equal(HarnessContextAssemblyStage.SnapshotCaptured, stageList[reducerIdx + 1]);
    }

    [Fact]
    public async Task AssembleAsync_TwoReducerAttempts_RecordsTwoReducerAttemptStages()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["old1"] = 90, ["old2"] = 90, ["recent"] = 20,
        };
        // total=230 >= threshold(100-10=90) → triggered; two reducer attempts needed
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 3, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old1", ChatRole.User, "old-1"),
            HarnessCompactionTestFixture.ConversationalEntry("old2", ChatRole.User, "old-2"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> RemoveOneOldPerAttempt(
            HarnessContextReductionRequest request, CancellationToken ct)
        {
            var stillHasOld2 = request.Entries.Any(e => e.EntryId == "old2");
            return Task.FromResult(
                HarnessAssemblerTestFixture.Without(request.Entries, stillHasOld2 ? "old2" : "old1"));
        }

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, RemoveOneOldPerAttempt);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(2, reducer.InvocationCount);
        // One ReducerAttempt stage per invocation
        Assert.Equal(2, result.Stages.Count(s => s == HarnessContextAssemblyStage.ReducerAttempt));
    }

    // --- Recoverable body eviction requires a durable reference entry -----------------------

    [Fact]
    public async Task AssembleAsync_RecoverableBodyWithoutMatchingReference_BodyNotEvictedAndPresentInResult()
    {
        // A RecoverableContextSegment entry whose digest has no corresponding ArtifactReference
        // entry in the context must not be silently discarded. Without a durable reference pointer
        // the body cannot be safely removed — it must remain in the result. Because there is no
        // matching reference, no eviction occurs (margin is not restored), so the reducer must
        // still be invoked as an actual pressure-handling attempt. The orphaned body is now also
        // "required" by SelectRequiredPreservation (bullet 2), so the non-reducing reducer's
        // proposal is verification-accepted but non-reducing, and the current context (including
        // the orphaned body) is preserved as a successful result after recording the attempt.
        var reference = HarnessCompactionTestFixture.SampleReference("artifact body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["orphaned-body"] = 65 };
        // total=75 >= threshold(100-40=60) → triggered; no ArtifactReference → no eviction → reducer invoked
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 40, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            // RecoverableSegment with no matching ArtifactReference entry in the context
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "orphaned-body", reference, "body text", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        // Body is preserved — no eviction without a durable reference
        Assert.True(result.IsSuccess);
        Assert.Contains(result.FinalEntries!, e => e.EntryId == "orphaned-body");
        Assert.DoesNotContain(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Contains(HarnessContextAssemblyStage.ReducerAttempt, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_RecoverableBodyWithMatchingReference_BodyEvictedReferenceRetained()
    {
        // When a durable ArtifactReference entry for the same canonical digest exists, the
        // recoverable body must be evicted (the reference pointer survives independently) and
        // no further reducer invocation is required if the remaining entries fit.
        var reference = HarnessCompactionTestFixture.SampleReference("artifact body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["artifact-ref"] = 5, ["recoverable"] = 65 };
        // total=80 >= threshold(100-40=60) → triggered; after eviction: 15 ≤ 100 → success
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 40, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ArtifactEntry("artifact-ref", reference.ContentDigest),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "recoverable", reference, "the recovered body", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.True(result.IsSuccess);
        // Body evicted; durable reference entry retained
        Assert.Equal(["system", "artifact-ref"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(0, reducer.InvocationCount);
        Assert.Contains(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
        Assert.True(result.FinalVerification!.IsAccepted);
    }

    [Fact]
    public async Task AssembleAsync_DeterministicFallback_CannotDropRequiredReferenceLessRecoverableBody()
    {
        // The reducer proposes the entries unchanged (non-reducing) while the current size still
        // exceeds the hard limit, so the assembler falls through to the deterministic fallback.
        // The reference-less recoverable body has no durable ArtifactReference anywhere in the
        // context, so it is required (bullet 2) and must survive the fallback's required-only
        // candidate even though the non-required, non-recent conversational entry is dropped.
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 10,
            ["orphaned-recoverable"] = 70,
            ["old-conv"] = 15,
            ["recent-conv"] = 10,
        };
        // total=105 >= threshold(100-10=90) → triggered; no matching reference → no eviction →
        // reducer invoked; non-reducing proposal still over the 100 hard limit → fallback.
        // Required-only candidate: system(10) + orphaned-recoverable(70) + recent-conv(10) = 90 ≤ 100.
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "orphaned-recoverable",
                HarnessCompactionTestFixture.SampleReference("orphaned body", DateTimeOffset.UnixEpoch),
                "orphaned body",
                DateTimeOffset.UnixEpoch),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.User, "recent message"),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(["system", "orphaned-recoverable", "recent-conv"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.DoesNotContain(result.FinalEntries!, e => e.EntryId == "old-conv");
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Contains(HarnessContextAssemblyStage.DeterministicFallback, result.Stages);
    }
}
