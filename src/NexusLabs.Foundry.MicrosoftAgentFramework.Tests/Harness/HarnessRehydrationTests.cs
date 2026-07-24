// Tests intentionally exercise the resolver/rehydration mechanism's explicit CancellationToken
// parameter (including CancellationToken.None and pre-canceled tokens) directly. This is the
// behavior under test, not an oversight of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessArtifactRehydration"/> (T047): explicit-request-only behavior, the
/// exact resolved payload and marked recoverable segment shape, every non-resolved outcome
/// (<c>Unauthorized</c>, <c>Stale</c>, <c>Missing</c>, <c>OverBudget</c>), a stale execution binding
/// (current binding/workspace mismatch), cancellation, and the "no immediate re-offload"
/// marker/behavioral invariant.
/// </summary>
public sealed class HarnessRehydrationTests
{
    private const int DefaultMaximumRehydratedUtf8Bytes = HarnessArtifactTestFixture.DefaultMaximumResolvedUtf8Bytes;

    private static readonly DateTimeOffset CreatedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RehydratedAtUtc = new(2025, 1, 1, 0, 5, 0, TimeSpan.Zero);

    // --- Explicit-request-only behavior -----------------------------------------------------

    [Fact]
    public void HavingAResolvableReferenceAndAWiredRehydrationMechanism_NeverTouchesWorkspace_UntilRehydrateIsCalled()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "content nobody has explicitly requested rehydration for yet";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);

        // Merely minting a reference, persisting its content, and having a resolver/rehydration
        // mechanism wired up must never itself read the workspace — only an explicit
        // Rehydrate(request, ...) call may. There is no ambient/automatic trigger.
        Assert.Equal(0, fixture.Workspace.ReadFileCallCount);
        Assert.Equal(0, fixture.Workspace.FileExistsCallCount);
    }

    // --- Resolved: exact payload + marked recoverable segment shape -------------------------

    [Fact]
    public void Rehydrate_ResolvableReference_ReturnsResolvedWithMarkedRecoverableSegmentCarryingExactBody()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "the exact body that must come back byte-for-byte unchanged";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        Assert.NotNull(result.Segment);
        Assert.Equal(content, result.Segment!.Body);
        Assert.Equal(reference, result.Segment.Reference);
        Assert.Equal(RehydratedAtUtc, result.Segment.RehydratedAtUtc);
        Assert.True(result.Segment.SkipEagerOffload);
    }

    [Fact]
    public void Rehydrate_DeterministicPolicySourcedRequest_AlsoResolvesExplicitly()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "content requested via a deterministic policy rather than a tool call";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.DeterministicPolicy, DefaultMaximumRehydratedUtf8Bytes);

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        Assert.Equal(content, result.Segment!.Body);
    }

    // --- Unauthorized ------------------------------------------------------------------------

    [Fact]
    public void Rehydrate_ForeignOwnedReference_ReturnsUnauthorizedWithNoSegmentAndNeverReadsContent()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "content whose reference records a foreign owner identity";
        var foreignReference = fixture.CreateForeignOwnedReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(foreignReference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            foreignReference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var readsBefore = fixture.Workspace.ReadFileCallCount;

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Unauthorized, result.Status);
        Assert.Null(result.Segment);
        Assert.Equal(readsBefore, fixture.Workspace.ReadFileCallCount);
    }

    // --- Stale ---------------------------------------------------------------------------------

    [Fact]
    public void Rehydrate_ReferenceWhoseContentWasMutatedOutOfBand_ReturnsStaleWithNoSegment()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string originalContent = "original content this reference's digest was computed from";
        var reference = fixture.CreateReference(originalContent, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, originalContent);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, "mutated content with a different digest entirely");
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Stale, result.Status);
        Assert.Null(result.Segment);
    }

    // --- Missing -------------------------------------------------------------------------------

    [Fact]
    public void Rehydrate_ReferenceNeverWrittenToWorkspace_ReturnsMissingWithNoSegment()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var reference = fixture.CreateReference("content that was never actually persisted", CreatedAtUtc);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Missing, result.Status);
        Assert.Null(result.Segment);
    }

    // --- OverBudget ----------------------------------------------------------------------------

    [Fact]
    public void Rehydrate_ContentExceedsRequestedMaximumBudget_ReturnsOverBudgetWithNoSegment()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('x', 100);
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, maximumRehydratedUtf8Bytes: 10);

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.OverBudget, result.Status);
        Assert.Null(result.Segment);
    }

    [Fact]
    public void Rehydrate_MultibyteUtf8Content_ExactBudgetResolvesButOneByteLowerIsOverBudget()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "🙂é";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);

        var exactUtf8ByteBudget = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);
        Assert.True(exactUtf8ByteBudget > content.Length);

        var resolvedRequest = HarnessArtifactRehydrationRequest.Create(
            reference,
            HarnessArtifactRehydrationRequestSource.ToolRequest,
            exactUtf8ByteBudget);
        var overBudgetRequest = HarnessArtifactRehydrationRequest.Create(
            reference,
            HarnessArtifactRehydrationRequestSource.ToolRequest,
            exactUtf8ByteBudget - 1);

        var resolved = fixture.Rehydration.Rehydrate(resolvedRequest, RehydratedAtUtc, CancellationToken.None);
        var overBudget = fixture.Rehydration.Rehydrate(overBudgetRequest, RehydratedAtUtc, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(content, resolved.Segment!.Body);
        Assert.Equal(HarnessArtifactResolutionStatus.OverBudget, overBudget.Status);
        Assert.Null(overBudget.Segment);
        Assert.Equal(exactUtf8ByteBudget, overBudget.Resolution.ObservedContentByteSize);
    }

    // --- Current binding/workspace mismatch -----------------------------------------------------

    [Fact]
    public void Rehydrate_WhenAmbientExecutionContextNoLongerMatchesCapturedBinding_ThrowsInvalidOperationException()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "content that must never be reached because the binding is now invalid";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        // Simulate a different execution context becoming ambient (e.g. a different bound
        // workspace instance for a different tool call) without the fixture's resolver being
        // re-bound to it.
        using var differentScope = fixture.Accessor.BeginScope(
            new AgentExecutionContext(
                HarnessArtifactTestFixture.DefaultUserId,
                HarnessArtifactTestFixture.DefaultOrchestrationId,
                Workspace: new FakeWorkspace()));

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None));
    }

    // --- Cancellation --------------------------------------------------------------------------

    [Fact]
    public void Rehydrate_PreCanceledToken_ThrowsOperationCanceledExceptionBeforeTouchingWorkspace()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "content that must never be touched once cancellation was already requested";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var readsBefore = fixture.Workspace.ReadFileCallCount;
        var fileExistsBefore = fixture.Workspace.FileExistsCallCount;

        Assert.Throws<OperationCanceledException>(() =>
            fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, cancellationSource.Token));

        Assert.Equal(readsBefore, fixture.Workspace.ReadFileCallCount);
        Assert.Equal(fileExistsBefore, fixture.Workspace.FileExistsCallCount);
    }

    // --- No immediate re-offload marker/invariant -----------------------------------------------

    [Fact]
    public void Rehydrate_ResolvedSegment_AlwaysMarksSkipEagerOffloadTrue_RegardlessOfBodySize()
    {
        using var fixture = HarnessArtifactTestFixture.Create();

        const string smallContent = "small";
        var smallReference = fixture.CreateReference(smallContent, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(smallReference.WorkspacePath, smallContent);
        var smallRequest = HarnessArtifactRehydrationRequest.Create(
            smallReference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var largeContent = new string('y', 50_000);
        var largeReference = fixture.CreateReference(largeContent, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(largeReference.WorkspacePath, largeContent);
        var largeRequest = HarnessArtifactRehydrationRequest.Create(
            largeReference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var smallResult = fixture.Rehydration.Rehydrate(smallRequest, RehydratedAtUtc, CancellationToken.None);
        var largeResult = fixture.Rehydration.Rehydrate(largeRequest, RehydratedAtUtc, CancellationToken.None);

        // Dedicated assertion on the structural marker itself: a rehydrated body — however large —
        // must never be indistinguishable from ordinary conversation content that is still eligible
        // for eager offload within the same active request.
        Assert.True(smallResult.Segment!.SkipEagerOffload);
        Assert.True(largeResult.Segment!.SkipEagerOffload);
    }

    [Fact]
    public void Rehydrate_ResolvedOutcome_NeverWritesToWorkspace_EvenForOversizedBodies()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('z', 50_000);
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);
        var request = HarnessArtifactRehydrationRequest.Create(
            reference, HarnessArtifactRehydrationRequestSource.ToolRequest, DefaultMaximumRehydratedUtf8Bytes);

        var writesBefore = fixture.Workspace.WriteFileCallCount;

        var result = fixture.Rehydration.Rehydrate(request, RehydratedAtUtc, CancellationToken.None);

        // Dedicated behavioral assertion: rehydration itself must never re-trigger an eager-offload
        // write within the active request — it only ever reads.
        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, result.Status);
        Assert.Equal(writesBefore, fixture.Workspace.WriteFileCallCount);
    }
}
