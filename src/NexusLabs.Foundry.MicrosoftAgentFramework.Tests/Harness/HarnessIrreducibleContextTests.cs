// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessContextAssembler"/>'s distinct <see cref="HarnessContextAssemblyOutcome.Irreducible"/>
/// termination: reached only when both the bounded reducer loop and the deterministic preservation-only
/// fallback (extended with any retained optional context, then required-only) still exceed the hard
/// limit. An over-budget history is never returned as a success under any of these conditions — the
/// termination carries only categorical evidence (sizes, hard limit, attempt count, required entry ids,
/// latest snapshot version), never the raw entries.
/// </summary>
public sealed class HarnessIrreducibleContextTests
{
    [Fact]
    public async Task AssembleAsync_GrowingReducerOutput_FallbackTooLarge_ReturnsIrreducible()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90, ["recent"] = 20, ["grown"] = 10 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(40, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("old", ChatRole.User, "old message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent", ChatRole.User, "recent message"),
        };

        Task<IReadOnlyList<HarnessContextEntry>> Grow(HarnessContextReductionRequest request, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HarnessContextEntry>>(
                [.. request.Entries, HarnessCompactionTestFixture.ConversationalEntry("grown", ChatRole.Assistant, "grown")]);

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, Grow);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
    }

    [Fact]
    public async Task AssembleAsync_InvalidReducerOutput_FallbackTooLarge_ReturnsIrreducible()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["old"] = 90, ["recent"] = 20 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(40, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
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

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
    }

    [Fact]
    public async Task AssembleAsync_RequiredContentAloneExceedsHardLimitByOne_ReturnsIrreducible()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 101 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal(101, result.FinalEstimatedSize);
        Assert.Equal(100, result.HardLimit);
    }

    [Fact]
    public async Task AssembleAsync_Irreducible_NeverReturnsOverBudgetHistoryAsSuccess()
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

        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Null(result.FinalVerification);
    }

    [Fact]
    public async Task AssembleAsync_Irreducible_CarriesCategoricalEvidenceOnly()
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
        Assert.Equal(40, result.HardLimit);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(["system", "recent"], result.RequiredEntryIds);
        Assert.Equal(0, result.LatestSnapshotVersion);
        Assert.Equal(50, result.FinalEstimatedSize);
        Assert.Equal(140, result.OriginalEstimatedSize);
    }

    [Fact]
    public async Task AssembleAsync_UnderLimitOrphanedToolCall_BelowTrigger_ReturnsIrreducibleNotSuccess()
    {
        // An orphaned tool call (a call entry with no matching result anywhere in the context)
        // makes the entries structurally invalid. Even when the total size is strictly below the
        // trigger threshold — which would normally be a WithinLimit fast path — the verifier's
        // rejection of the invalid tool sequence must cause a structured Irreducible termination.
        // The broken context must never be forwarded as a WithinLimit success.
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["orphaned-call"] = 5 };
        // total=15 < threshold(100-40=60) → strictly below trigger → below-trigger verification
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 40, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            // ToolCallEntry with call-id "call-1" but no matching ToolResultEntry → orphaned call
            HarnessCompactionTestFixture.ToolCallEntry("orphaned-call", ("call-1", "do_thing")),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Null(result.FinalVerification);
        // Reducer is never invoked: the below-trigger path terminates on verification failure
        // without ever reaching the bounded reducer loop.
        Assert.Equal(0, reducer.InvocationCount);
    }

    [Fact]
    public async Task AssembleAsync_RequiredReferenceLessRecoverableBodyAlonePreventsFit_ReturnsIrreducible()
    {
        // A RecoverableContextSegment with no matching ArtifactReference is required (bullet 2):
        // it alone, together with the system entry, already exceeds the hard limit, so neither the
        // reducer (non-reducing) nor the deterministic fallback's required-only candidate can ever
        // fit. This must terminate as a distinct Irreducible outcome, not a stale success.
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["orphaned-recoverable"] = 35 };
        // total=45 >= threshold(40-5=35) → triggered; no matching reference → no eviction →
        // reducer invoked (non-reducing, still over the 40 hard limit) → fallback's required-only
        // candidate (system + orphaned-recoverable = 45) also exceeds the 40 hard limit.
        var policy = HarnessCompactionTestFixture.CreatePolicy(40, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "orphaned-recoverable",
                HarnessCompactionTestFixture.SampleReference("orphaned body", DateTimeOffset.UnixEpoch),
                "orphaned body",
                DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(["system", "orphaned-recoverable"], result.RequiredEntryIds);
        Assert.Contains(HarnessContextAssemblyStage.DeterministicFallback, result.Stages);
    }

    // --- Taxonomy: ConcurrentMutationLimit is reserved for the direct churn path only ---------
    //
    // A version change alone, or even a version change that consumed a restart earlier in the
    // same assembly, never by itself selects ConcurrentMutationLimit. Only the direct churn path
    // — a version change observed with the bounded attempt budget already exhausted before that
    // restart can be consumed — does. Once the assembler successfully restarts onto a newer,
    // stable snapshot version, any later termination is evaluated purely on whether required
    // content fits/verifies against that stable version: Irreducible if not, regardless of the
    // earlier restart.

    [Fact]
    public async Task AssembleAsync_RestartEstablishesStableSnapshotButRequiredContentStillTooLarge_ReturnsIrreducibleNotConcurrentMutationLimit()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["filler"] = 80, ["injected-approval"] = 80 };
        // Initial: system+filler=110 >= threshold(100-10=90) → triggered; no recoverable body →
        // no eviction → reducer attempt 1 injects an ApprovalSecurityState entry (always
        // required) and proposes dropping "filler"; the post-reducer version check detects the
        // injection with one attempt still available (1 < 2), so it restarts — successfully
        // establishing a stable snapshot version (1) that is never superseded again. Reducer
        // attempt 2 (against the restarted, newest snapshot) proposes [system, injected-approval]
        // (110), which is progressing (110 < 190) but still exceeds the 100 hard limit, so the
        // attempt budget (now exhausted at 2) falls through to the deterministic fallback, whose
        // required-only candidate ([system, injected-approval] = 110) also exceeds the hard
        // limit. Required content alone cannot fit against the stable, successfully-restarted
        // snapshot: this is Irreducible, never ConcurrentMutationLimit, even though a restart
        // occurred earlier in this same assembly.
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "old summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(entries);
        var injected = false;

        Task<IReadOnlyList<HarnessContextEntry>> InjectOnceThenDropFiller(
            HarnessContextReductionRequest request, CancellationToken cancellationToken)
        {
            if (!injected)
            {
                injected = true;
                provider.Inject(HarnessCompactionTestFixture.ApprovalEntry(
                    "injected-approval", "newly approved action"));
            }

            return Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "filler"));
        }

        var reducer = new HarnessScriptedContextReducer(InjectOnceThenDropFiller);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Null(result.FinalVerification);
        Assert.Equal(2, reducer.InvocationCount);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(1, result.LatestSnapshotVersion);
        Assert.Equal(110, result.FinalEstimatedSize);
        Assert.Equal(100, result.HardLimit);
        Assert.Equal(["system", "injected-approval"], result.RequiredEntryIds);
        Assert.Contains(HarnessContextAssemblyStage.RestartedAfterMutation, result.Stages);
        Assert.Contains(HarnessContextAssemblyStage.DeterministicFallback, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_ChurnExhaustsBudgetBeforeRestartCanBeConsumed_StillReturnsConcurrentMutationLimit()
    {
        // Contrast with the test above: here the single available attempt is consumed by the
        // one reducer invocation that itself triggers the injection, so the very next version
        // check observes the change with the budget already exhausted — the direct churn path.
        // This remains ConcurrentMutationLimit: unlike the restart-then-stable case above, no
        // later stable snapshot version is ever successfully established.
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["filler"] = 80, ["churn"] = 5 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "old summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(entries);

        Task<IReadOnlyList<HarnessContextEntry>> InjectDuringReducer(
            HarnessContextReductionRequest request, CancellationToken cancellationToken)
        {
            provider.Inject(HarnessCompactionTestFixture.ConversationalEntry("churn", ChatRole.User, "churn message"));
            return Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "filler"));
        }

        var reducer = new HarnessScriptedContextReducer(InjectDuringReducer);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.ConcurrentMutationLimit, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Null(result.FinalVerification);
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(1, result.LatestSnapshotVersion);
    }
}
