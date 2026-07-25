// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessContextAssembler"/>'s interaction with a versioned
/// <see cref="HarnessContextSnapshot"/> injected mid-reduction: a new entry appearing while the
/// configured <see cref="IHarnessContextReducer"/> is in flight is detected by the very next snapshot
/// version recheck, the in-flight (now stale) proposal is discarded entirely, and assembly restarts
/// deterministically from the newest snapshot's entries — consuming one attempt of the bounded budget.
/// Every injection here happens synchronously inside the scripted reducer callback, so these tests are
/// fully deterministic with no real timing or races.
/// </summary>
public sealed class HarnessMessageInjectionTests
{
    [Fact]
    public async Task AssembleAsync_MessageInjectedDuringReducer_DiscardsStaleProposalAndRestartsFromLatestSnapshot()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["filler"] = 80, ["injected-approval"] = 15 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 3, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "a prior summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(entries);
        var injected = false;

        Task<IReadOnlyList<HarnessContextEntry>> ReduceAndSometimesInject(
            HarnessContextReductionRequest request, CancellationToken cancellationToken)
        {
            if (!injected)
            {
                injected = true;
                provider.Inject(HarnessCompactionTestFixture.ApprovalEntry("injected-approval", "newly approved action"));
            }

            return Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "filler"));
        }

        var reducer = new HarnessScriptedContextReducer(ReduceAndSometimesInject);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(["system", "injected-approval"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(2, reducer.InvocationCount);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(4, provider.CaptureCount);
        Assert.Equal(1, result.LatestSnapshotVersion);
        Assert.Contains(HarnessContextAssemblyStage.RestartedAfterMutation, result.Stages);

        // The injected entry appears exactly once — never lost, never duplicated.
        Assert.Single(result.FinalEntries!, e => e.EntryId == "injected-approval");
    }

    [Fact]
    public async Task AssembleAsync_ToolExchangeInjectedDuringReducer_PreservedAsValidSequenceAfterRestart()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["filler"] = 100, ["injected-call"] = 10, ["injected-result"] = 10,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(120, 10, 1, 3, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "a prior summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(entries);
        var injected = false;

        Task<IReadOnlyList<HarnessContextEntry>> ReduceAndSometimesInject(
            HarnessContextReductionRequest request, CancellationToken cancellationToken)
        {
            if (!injected)
            {
                injected = true;
                provider.Inject(HarnessCompactionTestFixture.ToolCallEntry("injected-call", ("call-1", "lookup")));
                provider.Inject(HarnessCompactionTestFixture.ToolResultEntry("injected-result", ("call-1", "ok")));
            }

            return Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "filler"));
        }

        var reducer = new HarnessScriptedContextReducer(ReduceAndSometimesInject);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(["system", "injected-call", "injected-result"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.NotNull(result.FinalVerification);
        Assert.True(result.FinalVerification!.IsAccepted);
    }

    [Fact]
    public async Task AssembleAsync_PerpetualInjectionEveryAttempt_ExhaustsBudget_ReturnsConcurrentMutationLimit()
    {
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 101, ["injected-1"] = 5, ["injected-2"] = 5,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 5, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };

        var provider = new HarnessMutableContextSnapshotProvider(entries);
        var injectionCount = 0;

        Task<IReadOnlyList<HarnessContextEntry>> AlwaysInject(
            HarnessContextReductionRequest request, CancellationToken cancellationToken)
        {
            injectionCount++;
            provider.Inject(HarnessCompactionTestFixture.ConversationalEntry(
                $"injected-{injectionCount}", ChatRole.User, $"churn message {injectionCount}"));
            return Task.FromResult(request.Entries);
        }

        var reducer = new HarnessScriptedContextReducer(AlwaysInject);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.ConcurrentMutationLimit, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FinalEntries);
        Assert.Null(result.FinalVerification);
        Assert.Equal(2, reducer.InvocationCount);
        Assert.Equal(2, result.AttemptCount);
        Assert.Equal(2, result.LatestSnapshotVersion);
    }

    [Fact]
    public async Task AssembleAsync_NoInjectionOccurs_NeverReturnsConcurrentMutationLimit()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 101 };
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 5, 1, 2, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[] { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        // No restart was ever observed, so a still-over-budget outcome is Irreducible, never
        // ConcurrentMutationLimit — that distinction is reserved for when a version change was
        // actually observed and consumed an attempt.
        Assert.Equal(HarnessContextAssemblyOutcome.Irreducible, result.Outcome);
    }

    // --- Finalization-capture injection: injected entries appear exactly once ----------------

    [Fact]
    public async Task AssembleAsync_InjectionDuringBelowTriggerFinalizationCapture_InjectedEntryPresentExactlyOnce()
    {
        // The initial snapshot is strictly below the trigger threshold. During the finalization
        // version check that guards the below-trigger success return, an entry is injected —
        // bumping the snapshot version. The assembler must detect this, discard the stale
        // WithinLimit candidate, and restart deterministically from the newest snapshot. The
        // injected entry must appear exactly once in the final result — never lost, never
        // duplicated.
        var sizes = new Dictionary<string, int> { ["system"] = 10, ["injected-msg"] = 5 };
        // Initial: system only (10) < threshold (100-40=60) → strictly below trigger
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 40, 1, 3, new HarnessFixedSizeContextEstimator(sizes));
        var initialEntries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(initialEntries);
        var injected = false;

        // OnCapture fires before CaptureCount is incremented. CaptureCount==1 at entry of the
        // SECOND CaptureSnapshot call (first=0→1, second=1→2). That second call is the
        // finalization capture of the below-trigger path.
        provider.OnCapture = () =>
        {
            if (provider.CaptureCount == 1 && !injected)
            {
                injected = true;
                provider.Inject(
                    HarnessCompactionTestFixture.ConversationalEntry(
                        "injected-msg", ChatRole.User, "injected!"));
            }
        };

        var reducer = new HarnessScriptedContextReducer(HarnessAssemblerTestFixture.Unchanged);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        // Injected entry present exactly once — neither lost nor duplicated.
        Assert.True(result.IsSuccess);
        Assert.Single(result.FinalEntries!, e => e.EntryId == "injected-msg");
        Assert.Contains(HarnessContextAssemblyStage.RestartedAfterMutation, result.Stages);
        Assert.Equal(0, reducer.InvocationCount);
    }

    [Fact]
    public async Task AssembleAsync_InjectionDuringPostReducerFinalizationCapture_InjectedEntryPresentExactlyOnce()
    {
        // The reducer successfully reduces the context to within the hard limit. During the
        // finalization version check that guards the post-reducer success return, an entry is
        // injected. The assembler must discard the stale Reduced candidate and restart, eventually
        // including the injected entry in the final result exactly once — never lost, never
        // duplicated.
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 30, ["filler"] = 80, ["injected"] = 10,
        };
        // Initial: system+filler=110 >= threshold(100-10=90) → triggered; filler is reducible.
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 4, new HarnessFixedSizeContextEstimator(sizes));
        var initialEntries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "old summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(initialEntries);
        var injected = false;

        // Captures in the triggered path (one reducer call):
        //   capture 0 (CaptureCount=0): initial snapshot
        //   capture 1 (CaptureCount=1): post-reducer version recheck  ← no injection
        //   capture 2 (CaptureCount=2): finalization capture           ← INJECT HERE
        provider.OnCapture = () =>
        {
            if (provider.CaptureCount == 2 && !injected)
            {
                injected = true;
                provider.Inject(
                    HarnessCompactionTestFixture.ConversationalEntry(
                        "injected", ChatRole.User, "injected!"));
            }
        };

        // Reducer always drops "filler" regardless of which attempt it is.
        Task<IReadOnlyList<HarnessContextEntry>> DropFiller(
            HarnessContextReductionRequest request, CancellationToken ct) =>
            Task.FromResult(HarnessAssemblerTestFixture.Without(request.Entries, "filler"));

        var reducer = new HarnessScriptedContextReducer(DropFiller);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        // Injected entry present exactly once in the final entries.
        Assert.True(result.IsSuccess);
        Assert.Single(result.FinalEntries!, e => e.EntryId == "injected");
        Assert.Contains(HarnessContextAssemblyStage.RestartedAfterMutation, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_InjectionDuringFallbackFinalizationCapture_RestartsAndIncludesInjectedEntryExactlyOnce()
    {
        // The reducer is non-reducing and the current entries still exceed the hard limit, so the
        // assembler falls through to the deterministic fallback. During the finalization version
        // check that guards the fallback's required-only candidate, an entry is injected. With a
        // budget large enough to still afford a restart, the assembler must discard the stale
        // fallback candidate, restart deterministically from the newest snapshot, and recompute the
        // fallback candidates fresh — the injected entry appearing exactly once in the final result,
        // never lost, never duplicated, and never returned as a stale success.
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["filler"] = 80, ["injected-msg"] = 5 };
        // Initial: system+filler=110 >= threshold(100-10=90) → triggered; no recoverable body → no
        // eviction → reducer invoked once (non-reducing, still over the 100 hard limit) → fallback.
        // Required-only candidate (filler, a Summary, is never required): system(30) ≤ 100.
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 3, new HarnessFixedSizeContextEstimator(sizes));
        var initialEntries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "old summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(initialEntries);
        var injected = false;

        // Captures in the triggered path (one non-reducing reducer call, no eviction):
        //   capture 0 (CaptureCount=0): initial snapshot
        //   capture 1 (CaptureCount=1): post-reducer version recheck        ← no injection
        //   capture 2 (CaptureCount=2): fallback's required-candidate       ← INJECT HERE
        //                                finalization (extended candidate is skipped: no
        //                                OptionalContext entries exist, so extendedIdSet.Count
        //                                equals requiredIdSet.Count).
        provider.OnCapture = () =>
        {
            if (provider.CaptureCount == 2 && !injected)
            {
                injected = true;
                provider.Inject(
                    HarnessCompactionTestFixture.ConversationalEntry(
                        "injected-msg", ChatRole.User, "injected!"));
            }
        };

        var reducer = new HarnessScriptedContextReducer(HarnessAssemblerTestFixture.Unchanged);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.Equal(HarnessContextAssemblyOutcome.PreservationFallback, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(["system", "injected-msg"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Single(result.FinalEntries!, e => e.EntryId == "injected-msg");
        Assert.DoesNotContain(result.FinalEntries!, e => e.EntryId == "filler");
        Assert.Equal(1, reducer.InvocationCount);
        Assert.Equal(2, result.AttemptCount);
        Assert.Contains(HarnessContextAssemblyStage.RestartedAfterMutation, result.Stages);
        Assert.Contains(HarnessContextAssemblyStage.DeterministicFallback, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_ChurnDuringFallbackFinalizationWithExhaustedBudget_ReturnsConcurrentMutationLimitNotStaleSuccess()
    {
        // The single attempt budget is exhausted by the reducer loop before the fallback's
        // required-only candidate would otherwise fit and succeed. An injection during the
        // fallback's finalization capture must not be silently absorbed into a stale
        // PreservationFallback success merely because the required-only candidate (computed
        // against now-superseded content) would have fit — the assembler must terminate directly
        // as ConcurrentMutationLimit against the latest observed snapshot instead.
        var sizes = new Dictionary<string, int> { ["system"] = 30, ["filler"] = 80, ["churn"] = 5 };
        // Initial: system+filler=110 >= threshold(100-10=90) → triggered; no recoverable body → no
        // eviction → reducer invoked once (non-reducing, still over the 100 hard limit; this
        // consumes the single available attempt) → fallback. Required-only candidate (system=30)
        // would fit the 100 hard limit, but the budget is already exhausted when the churn is
        // observed during the fallback's finalization capture.
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var initialEntries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.SummaryEntry("filler", "old summary"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(initialEntries);
        var injected = false;

        // Captures in the triggered path (one non-reducing reducer call, no eviction):
        //   capture 0 (CaptureCount=0): initial snapshot
        //   capture 1 (CaptureCount=1): post-reducer version recheck        ← no injection
        //   capture 2 (CaptureCount=2): fallback's required-candidate       ← INJECT HERE
        //                                finalization, with the single attempt already consumed.
        provider.OnCapture = () =>
        {
            if (provider.CaptureCount == 2 && !injected)
            {
                injected = true;
                provider.Inject(
                    HarnessCompactionTestFixture.ConversationalEntry("churn", ChatRole.User, "churn message"));
            }
        };

        var reducer = new HarnessScriptedContextReducer(HarnessAssemblerTestFixture.Unchanged);
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
