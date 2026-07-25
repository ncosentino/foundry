// Tests intentionally exercise every overload's explicit CancellationToken parameter (including
// CancellationToken.None) to verify binding/session/workspace validation behavior directly. This
// is the behavior under test, not an oversight of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Isolation tests for <c>WorkspaceAgentFileStore</c> (T049): two independently bound
/// bindings/sessions/workspaces must never cross-read or cross-write, a stale or changed trusted
/// identity/session/workspace must fail closed before the bound workspace is ever touched, and
/// the bridge must retain no static or ambient reference to a workspace beyond what the current
/// execution-context scope authorizes.
/// </summary>
public sealed class HarnessWorkspaceIsolationTests
{
    [Fact]
    public async Task TwoIndependentBindings_CannotCrossReadOrWrite()
    {
        using var harnessA = WorkspaceAgentFileStoreHarness.Create(
            new MutableExecutionContextAccessor(),
            "user-a",
            "orchestration-a",
            "session-a",
            new InMemoryWorkspace(),
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries);
        using var harnessB = WorkspaceAgentFileStoreHarness.Create(
            new MutableExecutionContextAccessor(),
            "user-b",
            "orchestration-b",
            "session-b",
            new InMemoryWorkspace(),
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries);

        await harnessA.Store.WriteAsync("kb/secret.md", "a-secret");
        await harnessB.Store.WriteAsync("kb/secret.md", "b-secret");

        Assert.Equal("a-secret", await harnessA.Store.ReadAsync("kb/secret.md"));
        Assert.Equal("b-secret", await harnessB.Store.ReadAsync("kb/secret.md"));

        await harnessA.Store.WriteAsync("kb/only-a.md", "only for a");

        Assert.True(await harnessA.Store.FileExistsAsync("kb/only-a.md"));
        Assert.False(await harnessB.Store.FileExistsAsync("kb/only-a.md"));
        Assert.Null(await harnessB.Store.ReadAsync("kb/only-a.md"));
    }

    [Fact]
    public async Task ChangedIdentity_FailsBeforeTouchingWorkspace()
    {
        var fake = new FakeWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var harness = WorkspaceAgentFileStoreHarness.Create(
            accessor,
            "user-a",
            "orchestration-a",
            "session-a",
            fake,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries);

        using (accessor.BeginScope(new AgentExecutionContext("user-b", "orchestration-a", Workspace: fake)))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Store.WriteAsync("kb/foo.md", "x"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Store.ReadAsync("kb/foo.md"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Store.ListChildrenAsync(""));
        }

        Assert.Equal(0, fake.WriteFileCallCount);
        Assert.Equal(0, fake.ReadFileCallCount);
        Assert.Equal(0, fake.GetFilePathsCallCount);

        // Once the impersonating nested scope disposes, the original trusted binding is valid
        // again and the operation reaches the workspace.
        await harness.Store.WriteAsync("kb/foo.md", "x");
        Assert.Equal(1, fake.WriteFileCallCount);
    }

    [Fact]
    public void ChangedSessionId_BindingValidationFailsBeforeTouchingWorkspace()
    {
        // WorkspaceAgentFileStore's constructor requires the supplied sessionId to already match
        // the trusted binding's captured session id (see
        // WorkspaceAgentFileStoreTests.Constructor_SessionIdNotMatchingBinding_Throws), so a
        // session mismatch can never be reached through the bridge's own call sites — every
        // EnsureBinding() call always re-supplies the same fixed, already-validated sessionId.
        // The actual runtime check the bridge relies on lives in HarnessExecutionBinding itself;
        // this test proves that check fails closed, before any workspace access, for a session
        // id that was never authorized for this binding.
        var fake = new FakeWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext("user-a", "orchestration-a", Workspace: fake));

        var capture = NexusLabs.Foundry.MicrosoftAgentFramework.Harness.HarnessExecutionBinding.Capture(
            accessor, "session-a", requireWorkspace: true);
        var binding = Assert.IsType<NexusLabs.Foundry.MicrosoftAgentFramework.Harness.HarnessExecutionBinding>(
            capture.Binding);

        var validation = binding.ValidateCurrent(accessor, "session-that-was-never-authorized");

        Assert.Equal(
            NexusLabs.Foundry.MicrosoftAgentFramework.Harness.HarnessExecutionBindingStatus.SessionMismatch,
            validation.Status);
        Assert.Equal(0, fake.ReadFileCallCount);
        Assert.Equal(0, fake.WriteFileCallCount);
    }

    [Fact]
    public async Task ChangedWorkspace_FailsBeforeTouchingWorkspace()
    {
        var fake = new FakeWorkspace();
        var replacement = new InMemoryWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var harness = WorkspaceAgentFileStoreHarness.Create(
            accessor,
            "user-a",
            "orchestration-a",
            "session-a",
            fake,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries);

        using (accessor.BeginScope(new AgentExecutionContext("user-a", "orchestration-a", Workspace: replacement)))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Store.WriteAsync("kb/foo.md", "x"));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Store.ReadAsync("kb/foo.md"));
        }

        Assert.Equal(0, fake.WriteFileCallCount);
        Assert.Equal(0, fake.ReadFileCallCount);
        Assert.Empty(replacement.GetFilePaths());
    }

    [Fact]
    public async Task MissingContext_FailsBeforeTouchingWorkspace()
    {
        var fake = new FakeWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        var harness = WorkspaceAgentFileStoreHarness.Create(
            accessor,
            "user-a",
            "orchestration-a",
            "session-a",
            fake,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries);
        var store = harness.Store;

        // Ends the execution-context scope, as if the request/orchestration had completed.
        harness.Dispose();

        Assert.Null(accessor.Current);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.WriteAsync("kb/foo.md", "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReadAsync("kb/foo.md"));

        Assert.Equal(0, fake.WriteFileCallCount);
        Assert.Equal(0, fake.ReadFileCallCount);
    }

    [Fact]
    public async Task NoStaticOrAmbientWorkspaceRetention_AcrossIndependentHarnesses()
    {
        // A fresh accessor/scope/binding/store built after a prior harness has already disposed
        // must never observe the prior harness's workspace contents — the bridge must not retain
        // any process-wide/static reference to a previously bound workspace.
        var firstWorkspace = new InMemoryWorkspace();
        using (var firstHarness = WorkspaceAgentFileStoreHarness.Create(
            new AgentExecutionContextAccessor(),
            "user-a",
            "orchestration-a",
            "session-a",
            firstWorkspace,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries))
        {
            await firstHarness.Store.WriteAsync("kb/first-only.md", "first");
        }

        using var secondHarness = WorkspaceAgentFileStoreHarness.Create(
            new AgentExecutionContextAccessor(),
            "user-b",
            "orchestration-b",
            "session-b",
            new InMemoryWorkspace(),
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries);

        Assert.Null(await secondHarness.Store.ReadAsync("kb/first-only.md"));
        Assert.False(await secondHarness.Store.FileExistsAsync("kb/first-only.md"));
    }
}
