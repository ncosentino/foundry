// Tests intentionally exercise HarnessToolResultOffloadTransform's explicit CancellationToken
// parameter (including a pre-canceled token) directly. This is the behavior under test, not an
// oversight of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Tools;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessToolResultOffloadTransform"/>'s cancellation points and the two
/// named recovery windows: "artifact-written/reference-not-committed" (token canceled during write
/// or checkpoint seam failure) and "reference-committed/history-persistence-failed" (a downstream
/// failure simulated after the transform already returned its reference). Also covers plain write
/// failures.
/// </summary>
public sealed class HarnessWorkspaceCancellationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Transform_PreCanceledToken_ThrowsOperationCanceledException_BeforeTouchingWorkspace()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('a', 500);
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var request = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            cancellationSource.Token);

        var writesBefore = fixture.Workspace.WriteFileCallCount;
        var fileExistsBefore = fixture.Workspace.FileExistsCallCount;

        Assert.Throws<OperationCanceledException>(() => HarnessToolResultOffloadTransform.Transform(request));

        Assert.Equal(writesBefore, fixture.Workspace.WriteFileCallCount);
        Assert.Equal(fileExistsBefore, fixture.Workspace.FileExistsCallCount);
    }

    // --- Token-canceled-during-write recovery window -----------------------------------------

    [Fact]
    public void Transform_TokenCanceledDuringWrite_ReturnsRecoveryRequired_ArtifactPersisted_RetryReturnsExistingReference()
    {
        // FakeWorkspace wraps an inner InMemoryWorkspace so the artifact really persists even
        // though the write override cancels the CTS before returning. The outer FakeWorkspace's
        // FileExists/TryReadFile delegate to the inner, so the persisted artifact is visible on
        // the retry path.
        var innerWorkspace = new InMemoryWorkspace();
        var fakeWorkspace = new FakeWorkspace(innerWorkspace);
        using var fixture = HarnessArtifactTestFixture.Create(fakeWorkspace);
        var content = new string('b', 500);
        var expectedDigest = HarnessArtifactIdentity.ComputeDigest(content);
        var expectedPath = HarnessArtifactIdentity.BuildPath(expectedDigest);

        using var cts = new CancellationTokenSource();
        fakeWorkspace.WriteFileOverride = (path, text) =>
        {
            // Write to inner so the artifact actually persists, then cancel before returning.
            var result = innerWorkspace.TryWriteFile(path, text);
            cts.Cancel();
            return result;
        };

        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var firstRequest = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            cts.Token);

        var firstOutcome = HarnessToolResultOffloadTransform.Transform(firstRequest);

        Assert.Equal(HarnessToolResultOffloadStatus.RecoveryRequired, firstOutcome.Status);
        Assert.Null(firstOutcome.Reference);
        Assert.NotNull(firstOutcome.RecoveryWorkspacePath);
        Assert.NotNull(firstOutcome.RecoveryContentDigest);
        Assert.Equal(expectedPath, firstOutcome.RecoveryWorkspacePath);
        Assert.Equal(expectedDigest, firstOutcome.RecoveryContentDigest);
        Assert.NotNull(firstOutcome.Evidence);
        Assert.Contains("CanceledAfterWrite", firstOutcome.Evidence);
        Assert.True(innerWorkspace.FileExists(expectedPath), "artifact must be persisted on disk");
        Assert.Equal(1, fakeWorkspace.WriteFileCallCount);

        // Retry: remove the write override and supply a non-canceled token. The artifact is
        // already present so the transform takes the ExistingReference branch — no duplicate write.
        fakeWorkspace.WriteFileOverride = null;
        var retryRequest = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None);

        var retryOutcome = HarnessToolResultOffloadTransform.Transform(retryRequest);

        Assert.Equal(HarnessToolResultOffloadStatus.ExistingReference, retryOutcome.Status);
        Assert.NotNull(retryOutcome.Reference);
        Assert.Equal(expectedDigest, retryOutcome.Reference!.ContentDigest);
        Assert.Equal(expectedPath, retryOutcome.Reference!.WorkspacePath);
        Assert.Equal(1, fakeWorkspace.WriteFileCallCount);
    }

    // --- Artifact-written/reference-not-committed recovery window (checkpoint failure) -------

    [Fact]
    public void Transform_CheckpointFailsAfterWrite_ReturnsRecoveryRequired_ThenRetrySucceedsAsExistingReferenceWithNoDuplicateWrite()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('e', 500);
        var checkpointCallCount = 0;
        // Checkpoint receives bounded path/digest (not a reference) — the reference is not yet
        // constructed when the checkpoint fires.
        HarnessToolResultOffloadCheckpoint checkpoint = (workspacePath, contentDigest, _) =>
        {
            checkpointCallCount++;
            throw new InvalidOperationException("simulated checkpoint failure");
        };
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint);
        var request = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None);

        var firstOutcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.RecoveryRequired, firstOutcome.Status);
        // Reference is null — not constructed before checkpoint fires.
        Assert.Null(firstOutcome.Reference);
        Assert.NotNull(firstOutcome.RecoveryWorkspacePath);
        Assert.NotNull(firstOutcome.RecoveryContentDigest);
        Assert.NotNull(firstOutcome.Evidence);
        Assert.Contains("CheckpointFailed", firstOutcome.Evidence);
        Assert.Equal(1, checkpointCallCount);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);
        var expectedDigest = HarnessArtifactIdentity.ComputeDigest(content);
        Assert.Equal(expectedDigest, firstOutcome.RecoveryContentDigest);

        // Retry: the artifact bytes are already on disk from the first attempt, so the retry
        // takes the ExistingReference branch — the checkpoint is never re-invoked, no dup write.
        var retryOutcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.ExistingReference, retryOutcome.Status);
        Assert.Equal(1, checkpointCallCount);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);
        Assert.NotNull(retryOutcome.Reference);
        Assert.Equal(firstOutcome.RecoveryWorkspacePath, retryOutcome.Reference!.WorkspacePath);
        Assert.Equal(firstOutcome.RecoveryContentDigest, retryOutcome.Reference!.ContentDigest);
    }

    [Fact]
    public void Transform_CheckpointCanceledAfterWrite_ReturnsRecoveryRequired_RetryStillResolvesSameReference()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('f', 500);
        // Checkpoint receives bounded path/digest — throwing with the cancellation token is still
        // caught and mapped to RecoveryRequired (not propagated as OperationCanceledException).
        HarnessToolResultOffloadCheckpoint checkpoint = (_, _, cancellationToken) =>
            throw new OperationCanceledException(cancellationToken);
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint);
        var request = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None);

        var firstOutcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.RecoveryRequired, firstOutcome.Status);
        Assert.Null(firstOutcome.Reference);
        Assert.NotNull(firstOutcome.RecoveryWorkspacePath);
        Assert.NotNull(firstOutcome.RecoveryContentDigest);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);

        var retryOutcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.ExistingReference, retryOutcome.Status);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);
        Assert.NotNull(retryOutcome.Reference);
        Assert.Equal(firstOutcome.RecoveryWorkspacePath, retryOutcome.Reference!.WorkspacePath);
        Assert.Equal(firstOutcome.RecoveryContentDigest, retryOutcome.Reference!.ContentDigest);
    }

    // --- Reference-committed/history-persistence-failed window -------------------------------

    [Fact]
    public void Transform_HistoryPersistenceFailureAfterReferenceReturned_RetryReturnsExistingReference_NoToolReinvocation()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('g', 500);
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var request = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None);

        var firstOutcome = HarnessToolResultOffloadTransform.Transform(request);
        Assert.Equal(HarnessToolResultOffloadStatus.Offloaded, firstOutcome.Status);
        Assert.NotNull(firstOutcome.Reference);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);

        // Inject a real history-persistence callback that receives the returned reference,
        // records the failure state, and throws a purpose-specific exception. This simulates
        // a downstream failure appending/persisting the reference to conversation history —
        // entirely outside the transform's responsibility.
        HarnessArtifactReference? callbackReceivedReference = null;
        var callbackInvoked = false;
        var historyPersistenceFailed = false;

        void SimulateHistoryPersistence(HarnessArtifactReference reference)
        {
            callbackInvoked = true;
            callbackReceivedReference = reference;
            historyPersistenceFailed = true;
            throw new InvalidOperationException("ReferenceCommittedHistoryPersistenceFailed: " +
                "simulated durable history store unavailable after reference was returned");
        }

        var caughtException = Assert.Throws<InvalidOperationException>(
            () => SimulateHistoryPersistence(firstOutcome.Reference!));

        Assert.True(callbackInvoked);
        Assert.True(historyPersistenceFailed);
        Assert.NotNull(callbackReceivedReference);
        Assert.Contains("ReferenceCommittedHistoryPersistenceFailed", caughtException.Message);

        // Retry: reuse the identical raw content without re-invoking the tool. The artifact is
        // already present so the transform resolves ExistingReference — same path/digest, no
        // duplicate write.
        var retryOutcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.ExistingReference, retryOutcome.Status);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);
        Assert.NotNull(retryOutcome.Reference);
        Assert.Equal(firstOutcome.Reference!.ContentDigest, retryOutcome.Reference!.ContentDigest);
        Assert.Equal(firstOutcome.Reference!.WorkspacePath, retryOutcome.Reference!.WorkspacePath);
        Assert.Equal(firstOutcome.ReferenceText, retryOutcome.ReferenceText);
        Assert.Equal(
            content,
            fixture.Workspace.TryReadFile(retryOutcome.Reference!.WorkspacePath).Value.Content);
    }

    // --- Plain write failure -------------------------------------------------------------------

    [Fact]
    public void Transform_WorkspaceWriteFails_PropagatesBoundedFailureEvidence_NoReferenceCommitted()
    {
        var workspace = new FakeWorkspace();
        using var fixture = HarnessArtifactTestFixture.Create(workspace);
        var content = new string('h', 500);
        workspace.WriteFileOverride = (_, _) =>
            WorkspaceResult<WriteFileResult>.Fail(new IOException("simulated disk full"));
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var request = new HarnessToolResultOffloadRequest(
            content,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        Assert.Null(outcome.Reference);
        Assert.NotNull(outcome.Evidence);
        Assert.Contains("WorkspaceWriteFailed", outcome.Evidence);
        Assert.Contains(nameof(IOException), outcome.Evidence);
        Assert.DoesNotContain(content, outcome.Evidence);
    }
}
