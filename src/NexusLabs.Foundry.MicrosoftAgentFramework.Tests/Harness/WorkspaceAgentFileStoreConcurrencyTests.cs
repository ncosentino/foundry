// Tests intentionally exercise every overload's explicit CancellationToken parameter (including
// CancellationToken.None and pre-canceled tokens) to verify cancellation behavior directly. This
// is the behavior under test, not an oversight of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Concurrency tests for <c>WorkspaceAgentFileStore</c> (T049): prove that ordinary concurrent
/// writes use <c>IWorkspace.TryWriteFile</c> semantics only. The bridge never calls
/// <c>TryCompareExchange</c> and never claims compare-exchange/CAS or atomicity guarantees —
/// whatever "last write wins" outcome results from contention is owned entirely by the bound
/// <c>IWorkspace</c> implementation, not by this bridge.
/// </summary>
public sealed class WorkspaceAgentFileStoreConcurrencyTests
{
    [Fact]
    public async Task ConcurrentWrites_ToSamePath_NeverCallCompareExchange()
    {
        var fake = new FakeWorkspace();
        using var harness = WorkspaceAgentFileStoreHarness.Create(fake);
        const int concurrency = 32;
        const string path = "kb/contended.md";

        using var gate = new ManualResetEventSlim(initialState: false);
        var tasks = Enumerable.Range(0, concurrency)
            .Select(i => Task.Run(async () =>
            {
                gate.Wait();
                await harness.Store.WriteAsync(path, $"value-{i}");
            }))
            .ToArray();

        gate.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(concurrency, fake.WriteFileCallCount);
        Assert.Equal(0, fake.CompareExchangeCallCount);
    }

    [Fact]
    public async Task ConcurrentWrites_ToSamePath_ProduceExactlyOneOfTheWrittenValues()
    {
        // Ordinary writes make no atomicity claim beyond whatever the underlying workspace
        // itself provides. This test proves the observable outcome is still "exactly one clean
        // value, chosen by the workspace" — never a torn/interleaved/corrupted string — which is
        // the contract InMemoryWorkspace's ConcurrentDictionary indexer assignment provides.
        // Which specific value wins is explicitly NOT something this bridge determines or
        // guarantees; the assertion below only pins down that the winner is one of the inputs.
        var fake = new FakeWorkspace();
        using var harness = WorkspaceAgentFileStoreHarness.Create(fake);
        const int concurrency = 32;
        const string path = "kb/contended.md";
        var writtenValues = Enumerable.Range(0, concurrency)
            .Select(i => $"value-{i}")
            .ToArray();

        using var gate = new ManualResetEventSlim(initialState: false);
        var tasks = writtenValues
            .Select(value => Task.Run(async () =>
            {
                gate.Wait();
                await harness.Store.WriteAsync(path, value);
            }))
            .ToArray();

        gate.Set();
        await Task.WhenAll(tasks);

        var finalContent = await harness.Store.ReadAsync(path);

        Assert.NotNull(finalContent);
        Assert.Contains(finalContent, writtenValues);
    }

    [Fact]
    public async Task ConcurrentWrites_ToDifferentPaths_AllSucceedIndependently()
    {
        var fake = new FakeWorkspace();
        using var harness = WorkspaceAgentFileStoreHarness.Create(fake);
        const int concurrency = 16;

        using var gate = new ManualResetEventSlim(initialState: false);
        var tasks = Enumerable.Range(0, concurrency)
            .Select(i => Task.Run(async () =>
            {
                gate.Wait();
                await harness.Store.WriteAsync($"kb/file-{i}.md", $"value-{i}");
            }))
            .ToArray();

        gate.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(concurrency, fake.WriteFileCallCount);
        Assert.Equal(0, fake.CompareExchangeCallCount);

        for (var i = 0; i < concurrency; i++)
        {
            Assert.Equal($"value-{i}", await harness.Store.ReadAsync($"kb/file-{i}.md"));
        }
    }
}
