// Tests intentionally exercise every overload's explicit CancellationToken parameter (including
// CancellationToken.None and pre-canceled tokens) to verify cancellation behavior directly. This
// is the behavior under test, not an oversight of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Workspace;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Behavioral tests for <c>WorkspaceAgentFileStore</c> (T049): the supported subset of MAF 1.15
/// <c>AgentFileStore</c> semantics it bridges onto <see cref="IWorkspace"/>, matching the T020
/// (<c>workspace-identity-feasibility.md</c>) and T041 (<c>harness-lifecycle-feasibility.md</c>)
/// evidence.
/// </summary>
public sealed class WorkspaceAgentFileStoreTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsContent()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await harness.Store.WriteAsync("kb/foo.md", "hello world");
        var content = await harness.Store.ReadAsync("kb/foo.md");

        Assert.Equal("hello world", content);
    }

    [Fact]
    public async Task FileExists_ReflectsWriteState()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        Assert.False(await harness.Store.FileExistsAsync("kb/foo.md"));

        await harness.Store.WriteAsync("kb/foo.md", "hello");

        Assert.True(await harness.Store.FileExistsAsync("kb/foo.md"));
    }

    [Theory]
    [InlineData("kb/foo.md", "./kb/foo.md")]
    [InlineData("kb/foo.md", "kb//foo.md")]
    [InlineData("kb/foo.md", @"kb\foo.md")]
    public async Task ForwardSlashAndRootRelativeSpellings_CanonicalizeToSameFile(
        string writtenAs,
        string readAs)
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await harness.Store.WriteAsync(writtenAs, "same file");

        Assert.Equal("same file", await harness.Store.ReadAsync(readAs));
        Assert.True(await harness.Store.FileExistsAsync(readAs));
    }

    [Theory]
    [InlineData("kb/../foo.md")]
    [InlineData("../foo.md")]
    [InlineData("kb/../../foo.md")]
    public async Task ParentTraversalSegment_IsRejected(string path)
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.WriteAsync(path, "x"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.ReadAsync(path));
        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.FileExistsAsync(path));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("")]
    [InlineData(".")]
    public async Task RootEquivalentPath_IsRejectedForFileOperations(string path)
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.WriteAsync(path, "x"));
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData(@"\etc\passwd")]
    [InlineData(@"C:\Windows\x")]
    [InlineData("C:/Windows/x")]
    [InlineData(@"\\server\share\x")]
    public async Task RootedDriveAndUncPaths_AreRejected(string path)
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.WriteAsync(path, "x"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.ReadAsync(path));
        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.FileExistsAsync(path));
    }

    [Fact]
    public async Task Read_MissingFile_ClassifiedAsMissing_ReturnsNull()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        var content = await harness.Store.ReadAsync("kb/does-not-exist.md");

        Assert.Null(content);
    }

    [Fact]
    public async Task Read_NonMissingFailure_PropagatesUnchanged()
    {
        var fake = new FakeWorkspace();
        var injected = new IOException("disk error");
        fake.ReadFileOverride = _ => WorkspaceResult<ReadFileResult>.Fail(injected);
        using var harness = WorkspaceAgentFileStoreHarness.Create(fake);

        var actual = await Assert.ThrowsAsync<IOException>(
            () => harness.Store.ReadAsync("kb/anything.md"));

        Assert.Same(injected, actual);
    }

    [Fact]
    public async Task ListChildren_ReturnsDirectoriesBeforeFiles_DedupedCaseInsensitively()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await harness.Store.WriteAsync("root-file.md", "x");
        await harness.Store.WriteAsync("A/nested.md", "x");
        await harness.Store.WriteAsync("a/other-nested.md", "x");
        await harness.Store.WriteAsync("B/deep/nested.md", "x");

        var entries = await harness.Store.ListChildrenAsync("");

        // Two directories ("A"/"a" deduped as one, "B"), then one direct file ("root-file.md").
        Assert.Equal(3, entries.Count);
        Assert.All(entries.Take(2), e => Assert.Equal("directory", e.Type));
        Assert.Equal("file", entries[2].Type);
        Assert.Equal("root-file.md", entries[2].Name);

        var directoryNames = entries.Take(2).Select(e => e.Name).ToArray();
        Assert.Contains(directoryNames, n => string.Equals(n, "A", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(directoryNames, n => string.Equals(n, "B", StringComparison.OrdinalIgnoreCase));
        // Only one casing of "A"/"a" survives — case-insensitive dedupe collapsed the pair.
        Assert.Single(directoryNames, n => string.Equals(n, "A", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListChildren_DirectChildrenOnly_ExcludesNestedGrandchildren()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await harness.Store.WriteAsync("kb/direct.md", "x");
        await harness.Store.WriteAsync("kb/nested/deep.md", "x");

        var entries = await harness.Store.ListChildrenAsync("kb");

        Assert.Equal(2, entries.Count);
        Assert.Equal("directory", entries[0].Type);
        Assert.Equal("nested", entries[0].Name);
        Assert.Equal("file", entries[1].Type);
        Assert.Equal("direct.md", entries[1].Name);
    }

    [Fact]
    public async Task ListChildren_CapExceeded_ThrowsExplicitly()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create(
            new InMemoryWorkspace(),
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier,
            maximumListEntries: 2);

        await harness.Store.WriteAsync("kb/one.md", "x");
        await harness.Store.WriteAsync("kb/two.md", "x");
        await harness.Store.WriteAsync("kb/three.md", "x");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.ListChildrenAsync("kb"));
    }

    [Fact]
    public async Task Delete_IsNotSupported()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();
        await harness.Store.WriteAsync("kb/foo.md", "x");

        await Assert.ThrowsAsync<NotSupportedException>(
            () => harness.Store.DeleteAsync("kb/foo.md"));

        // The unsupported call must not have deleted anything either.
        Assert.True(await harness.Store.FileExistsAsync("kb/foo.md"));
    }

    [Fact]
    public async Task Delete_InvalidPathFailsBeforeUnsupportedOutcome()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.DeleteAsync("../escape.md"));
    }

    [Fact]
    public async Task Search_IsNotSupported()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => harness.Store.SearchAsync("kb", "pattern"));
    }

    [Fact]
    public async Task Search_InvalidDirectoryFailsBeforeUnsupportedOutcome()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.SearchAsync("/root", "pattern"));
    }

    [Fact]
    public async Task CreateDirectory_IsAValidatedNoOp_AndInvisibleUntilAFileExistsUnderIt()
    {
        using var harness = WorkspaceAgentFileStoreHarness.Create();

        await harness.Store.CreateDirectoryAsync("kb/empty-dir");

        var entries = await harness.Store.ListChildrenAsync("kb");
        Assert.Empty(entries);

        // Rejecting a bad shape still applies to CreateDirectoryAsync — it validates, it doesn't
        // silently accept anything.
        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Store.CreateDirectoryAsync("kb/../escape"));
    }

    [Fact]
    public void PreCanceledToken_ThrowsSynchronously_BeforeAnyWorkspaceCall()
    {
        var fake = new FakeWorkspace();
        using var harness = WorkspaceAgentFileStoreHarness.Create(fake);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.WriteAsync("kb/foo.md", "x", cts.Token); });
        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.ReadAsync("kb/foo.md", cts.Token); });
        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.FileExistsAsync("kb/foo.md", cts.Token); });
        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.ListChildrenAsync("kb", cts.Token); });
        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.CreateDirectoryAsync("kb", cts.Token); });
        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.DeleteAsync("kb/foo.md", cts.Token); });
        Assert.Throws<OperationCanceledException>(
            () => { _ = harness.Store.SearchAsync("kb", "pattern", cancellationToken: cts.Token); });

        Assert.Equal(0, fake.WriteFileCallCount);
        Assert.Equal(0, fake.ReadFileCallCount);
        Assert.Equal(0, fake.FileExistsCallCount);
        Assert.Equal(0, fake.GetFilePathsCallCount);
    }

    [Fact]
    public void Constructor_NonPositiveMaximumListEntries_Throws()
    {
        var workspace = new InMemoryWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: workspace));
        var capture = HarnessExecutionBinding.Capture(
            accessor, "session-1", requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(
            capture.Binding);

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceAgentFileStore(
            workspace,
            binding,
            accessor,
            "session-1",
            0,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier));
    }

    [Fact]
    public void Constructor_SessionIdNotMatchingBinding_Throws()
    {
        var workspace = new InMemoryWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: workspace));
        var capture = HarnessExecutionBinding.Capture(
            accessor, "session-1", requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(
            capture.Binding);

        Assert.Throws<ArgumentException>(() => new WorkspaceAgentFileStore(
            workspace,
            binding,
            accessor,
            "different-session",
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier));
    }

    [Fact]
    public void Constructor_WorkspaceNotMatchingBinding_Throws()
    {
        var workspace = new InMemoryWorkspace();
        var unauthorizedWorkspace = new InMemoryWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: workspace));
        var capture = HarnessExecutionBinding.Capture(
            accessor, "session-1", requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(
            capture.Binding);

        Assert.Throws<ArgumentException>(() => new WorkspaceAgentFileStore(
            unauthorizedWorkspace,
            binding,
            accessor,
            "session-1",
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries,
            WorkspaceAgentFileStoreHarness.DefaultMissingFileClassifier));
    }

    [Fact]
    public void Constructor_NullMissingFileClassifier_Throws()
    {
        var workspace = new InMemoryWorkspace();
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1", "orchestration-1", Workspace: workspace));
        var capture = HarnessExecutionBinding.Capture(
            accessor, "session-1", requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(
            capture.Binding);

        Assert.Throws<ArgumentNullException>(() => new WorkspaceAgentFileStore(
            workspace,
            binding,
            accessor,
            "session-1",
            WorkspaceAgentFileStoreHarness.DefaultMaximumListEntries,
            null!));
    }
}
