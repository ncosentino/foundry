// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for the immutable snapshot/request/result boundaries <see cref="HarnessContextSnapshot"/>,
/// <see cref="HarnessContextReductionRequest"/>, and <see cref="HarnessContextAssemblyResult"/> seal
/// around a <see cref="HarnessContextEntry"/>: every factory defensively copies its inputs into a
/// read-only collection (and, for entries, a deep <see cref="HarnessContextEntry.Copy"/>) so mutating
/// the caller's original list, an entry's <see cref="HarnessContextEntry.Message"/>, or the collection
/// this type hands back can never change what the sealed boundary itself reports — including when the
/// mutation attempt originates from a reducer casting and editing its own reduction request in place.
/// </summary>
public sealed class HarnessContextBoundaryImmutabilityTests
{
    // --- HarnessContextSnapshot.Create seals its entries -------------------------------------

    [Fact]
    public void SnapshotCreate_MutatingOriginalListAfterward_DoesNotChangeSnapshot()
    {
        var entries = new List<HarnessContextEntry>
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
        };

        var snapshot = HarnessContextSnapshot.Create(0, entries);

        entries.Add(HarnessCompactionTestFixture.ConversationalEntry("extra", ChatRole.User, "extra message"));
        entries.Clear();

        Assert.Single(snapshot.Entries);
        Assert.Equal("system", snapshot.Entries[0].EntryId);
    }

    [Fact]
    public void SnapshotCreate_Entries_IsReadOnly_ThrowsOnMutationAttempt()
    {
        var entries = new List<HarnessContextEntry>
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
        };

        var snapshot = HarnessContextSnapshot.Create(0, entries);
        var mutableView = (IList<HarnessContextEntry>)snapshot.Entries;

        Assert.Throws<NotSupportedException>(() => mutableView.Add(entries[0]));
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        Assert.IsNotType<List<HarnessContextEntry>>(snapshot.Entries);
    }

    [Fact]
    public void SnapshotCreate_MutatingReturnedEntryMessageAfterward_DoesNotChangeSnapshot()
    {
        var entry = HarnessCompactionTestFixture.SystemEntry("system", "instructions");
        var snapshot = HarnessContextSnapshot.Create(0, [entry]);

        // Mutating a Message obtained from the snapshot's entry must not affect a later read.
        snapshot.Entries[0].Message.Contents.Add(new TextContent("smuggled"));

        Assert.Single(snapshot.Entries[0].Message.Contents);
        Assert.Equal("instructions", snapshot.Entries[0].Message.Text);
    }

    // --- HarnessContextReductionRequest.Create seals its entries and required ids ------------

    [Fact]
    public void ReductionRequestCreate_MutatingOriginalListsAfterward_DoesNotChangeRequest()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(new Dictionary<string, int> { ["system"] = 10 }));
        var entries = new List<HarnessContextEntry> { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var requiredEntryIds = new List<string> { "system" };

        var request = HarnessContextReductionRequest.Create(entries, requiredEntryIds, policy, attemptNumber: 1);

        entries.Add(HarnessCompactionTestFixture.ConversationalEntry("extra", ChatRole.User, "extra message"));
        entries.Clear();
        requiredEntryIds.Add("forged");
        requiredEntryIds.Clear();

        Assert.Single(request.Entries);
        Assert.Equal("system", request.Entries[0].EntryId);
        Assert.Single(request.RequiredEntryIds);
        Assert.Equal("system", request.RequiredEntryIds[0]);
    }

    [Fact]
    public void ReductionRequestCreate_EntriesAndRequiredEntryIds_AreReadOnly_ThrowOnMutationAttempt()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(new Dictionary<string, int> { ["system"] = 10 }));
        var entries = new List<HarnessContextEntry> { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var requiredEntryIds = new List<string> { "system" };

        var request = HarnessContextReductionRequest.Create(entries, requiredEntryIds, policy, attemptNumber: 1);

        Assert.Throws<NotSupportedException>(() => ((IList<HarnessContextEntry>)request.Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)request.RequiredEntryIds).Add("forged"));
        Assert.IsNotType<List<HarnessContextEntry>>(request.Entries);
        Assert.IsNotType<List<string>>(request.RequiredEntryIds);
    }

    [Fact]
    public void ReductionRequestCreate_ReturnsIndependentEntryCopy_NotTheAuthoritativeInstance()
    {
        var policy = HarnessCompactionTestFixture.CreatePolicy(
            100, 10, 1, 1, new HarnessFixedSizeContextEstimator(new Dictionary<string, int> { ["system"] = 10 }));
        var originalEntry = HarnessCompactionTestFixture.SystemEntry("system", "instructions");

        var request = HarnessContextReductionRequest.Create([originalEntry], ["system"], policy, attemptNumber: 1);

        // A reducer that casts the returned entry and mutates a Message it obtains cannot change
        // the entry instance the assembler originally captured, since Create() deep-copied it.
        request.Entries[0].Message.Contents.Add(new TextContent("smuggled"));

        Assert.Single(originalEntry.Message.Contents);
        Assert.Equal("instructions", originalEntry.Message.Text);
    }

    // --- HarnessContextEntry.Copy(): independent deep copy -----------------------------------

    [Fact]
    public void Copy_ReturnsIndependentEntry_MutatingOneCopysMessageDoesNotAffectTheOther()
    {
        var original = HarnessCompactionTestFixture.SystemEntry("system", "instructions");

        var copy = original.Copy();
        copy.Message.Contents.Add(new TextContent("smuggled into copy's clone"));

        Assert.Equal(original.EntryId, copy.EntryId);
        Assert.Equal(original.Kind, copy.Kind);
        Assert.Single(original.Message.Contents);
        Assert.Single(copy.Message.Contents);
        Assert.Equal("instructions", original.Message.Text);
        Assert.Equal("instructions", copy.Message.Text);
    }

    // --- HarnessContextAssemblyResult.Success/Terminated seal their inputs -------------------

    [Fact]
    public void ResultSuccess_MutatingSourceCollectionsAfterward_DoesNotChangeResult()
    {
        var finalEntries = new List<HarnessContextEntry> { HarnessCompactionTestFixture.SystemEntry("system", "instructions") };
        var stages = new List<HarnessContextAssemblyStage> { HarnessContextAssemblyStage.SnapshotCaptured };
        var requiredEntryIds = new List<string> { "system" };
        var verification = HarnessCompactionVerificationResult.Accepted(requiredEntryIds);

        var result = HarnessContextAssemblyResult.Success(
            HarnessContextAssemblyOutcome.WithinLimit,
            finalEntries,
            originalEstimatedSize: 10,
            finalEstimatedSize: 10,
            hardLimit: 100,
            attemptCount: 0,
            stages,
            requiredEntryIds,
            latestSnapshotVersion: 0,
            verification);

        finalEntries.Add(HarnessCompactionTestFixture.ConversationalEntry("extra", ChatRole.User, "extra"));
        finalEntries.Clear();
        stages.Add(HarnessContextAssemblyStage.ReducerAttempt);
        stages.Clear();
        requiredEntryIds.Add("forged");
        requiredEntryIds.Clear();

        Assert.Single(result.FinalEntries!);
        Assert.Equal("system", result.FinalEntries![0].EntryId);
        Assert.Single(result.Stages);
        Assert.Equal(HarnessContextAssemblyStage.SnapshotCaptured, result.Stages[0]);
        Assert.Single(result.RequiredEntryIds);
        Assert.Equal("system", result.RequiredEntryIds[0]);
    }

    [Fact]
    public void ResultSuccess_FinalEntries_IsIndependentCopy_MutatingReturnedMessageDoesNotAffectResult()
    {
        var sourceEntry = HarnessCompactionTestFixture.SystemEntry("system", "instructions");
        var requiredEntryIds = new List<string> { "system" };
        var verification = HarnessCompactionVerificationResult.Accepted(requiredEntryIds);

        var result = HarnessContextAssemblyResult.Success(
            HarnessContextAssemblyOutcome.WithinLimit,
            [sourceEntry],
            originalEstimatedSize: 10,
            finalEstimatedSize: 10,
            hardLimit: 100,
            attemptCount: 0,
            [HarnessContextAssemblyStage.SnapshotCaptured],
            requiredEntryIds,
            latestSnapshotVersion: 0,
            verification);

        result.FinalEntries![0].Message.Contents.Add(new TextContent("smuggled"));

        Assert.Single(sourceEntry.Message.Contents);
        Assert.Single(result.FinalEntries![0].Message.Contents);
    }

    [Fact]
    public void ResultTerminated_MutatingSourceCollectionsAfterward_DoesNotChangeResult()
    {
        var stages = new List<HarnessContextAssemblyStage> { HarnessContextAssemblyStage.DeterministicFallback };
        var requiredEntryIds = new List<string> { "system" };

        var result = HarnessContextAssemblyResult.Terminated(
            HarnessContextAssemblyOutcome.Irreducible,
            originalEstimatedSize: 10,
            finalEstimatedSize: 10,
            hardLimit: 5,
            attemptCount: 1,
            stages,
            requiredEntryIds,
            latestSnapshotVersion: 0);

        stages.Add(HarnessContextAssemblyStage.ReducerAttempt);
        stages.Clear();
        requiredEntryIds.Add("forged");
        requiredEntryIds.Clear();

        Assert.Single(result.Stages);
        Assert.Equal(HarnessContextAssemblyStage.DeterministicFallback, result.Stages[0]);
        Assert.Single(result.RequiredEntryIds);
        Assert.Equal("system", result.RequiredEntryIds[0]);
    }

    // --- Assembler-level: a reducer casting/mutating its request in place cannot corrupt the ---
    // --- assembler's authoritative snapshot, and the corruption attempt is never carried into --
    // --- the final result. --------------------------------------------------------------------

    [Fact]
    public async Task AssembleAsync_ReducerCastsAndMutatesRequestEntriesInPlace_AuthoritativeSnapshotUnaffected()
    {
        var sizes = new Dictionary<string, int> { ["system"] = 50, ["filler"] = 50 };
        // Both entries fit the 100 hard limit exactly, but the size (100) is still at or above
        // the trigger threshold (100-10=90), so the assembler still invokes the reducer.
        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ConversationalEntry("filler", ChatRole.User, "filler message"),
        };

        var provider = new HarnessMutableContextSnapshotProvider(entries);

        Task<IReadOnlyList<HarnessContextEntry>> MutateRequestInPlaceAndEcho(
            HarnessContextReductionRequest request, CancellationToken cancellationToken)
        {
            // A malicious/buggy reducer tries every avenue to mutate the assembler's
            // authoritative state through what it was handed: casting the read-only collections
            // back to a mutable list type (rejected structurally), and mutating a Message it
            // obtained from a request entry (isolated: Message always returns a fresh clone).
            Assert.Throws<NotSupportedException>(() => ((IList<HarnessContextEntry>)request.Entries).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)request.RequiredEntryIds).Add("forged"));
            request.Entries[0].Message.Contents.Add(new TextContent(" — smuggled"));

            // Echo the (attemptedly corrupted, but actually untouched) request entries back
            // unchanged — a non-reducing proposal the assembler must preserve as-is.
            return Task.FromResult(request.Entries);
        }

        var reducer = new HarnessScriptedContextReducer(MutateRequestInPlaceAndEcho);
        var assembler = new HarnessContextAssembler(policy, provider, reducer);

        var result = await assembler.AssembleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var systemEntry = Assert.Single(result.FinalEntries!, e => e.EntryId == "system");
        Assert.Equal("instructions", systemEntry.Message.Text);
        Assert.Single(systemEntry.Message.Contents);

        // The provider's own authoritative snapshot is likewise unaffected by the reducer's
        // in-place mutation attempt.
        var laterSnapshot = provider.CaptureSnapshot();
        var laterSystemEntry = laterSnapshot.Entries.Single(e => e.EntryId == "system");
        Assert.Equal("instructions", laterSystemEntry.Message.Text);
        Assert.Single(laterSystemEntry.Message.Contents);
    }
}
