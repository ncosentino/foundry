using System.Threading.Channels;

using Moq;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests;

public class ChannelProgressReporterTests
{
    [Fact]
    public async Task Report_DeliversToSink()
    {
        var received = new List<IProgressEvent>();
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) => received.Add(evt))
            .Returns(ValueTask.CompletedTask);

        await using var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        var evt = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1);

        reporter.Report(evt);

        // Give the consumer time to drain
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Single(received);
        Assert.Same(evt, received[0]);
    }

    [Fact]
    public async Task Report_IsNonBlocking()
    {
        var sinkStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSink = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async (IProgressEvent _, CancellationToken _) =>
            {
                sinkStarted.TrySetResult(true);
                await releaseSink.Task;
            });

        await using var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        try
        {
            var reportTask = Task.Run(() => reporter.Report(
                new WorkflowStartedEvent(
                    DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1)),
                TestContext.Current.CancellationToken);

            await reportTask.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            await sinkStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseSink.TrySetResult(true);
        }
    }

    [Fact]
    public async Task DisposeAsync_DrainsRemainingEvents()
    {
        var count = 0;
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((_, _) => Interlocked.Increment(ref count))
            .Returns(ValueTask.CompletedTask);

        var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        for (int i = 0; i < 10; i++)
        {
            reporter.Report(new WorkflowStartedEvent(
                DateTimeOffset.UtcNow, "wf-1", null, null, 0, i));
        }

        await reporter.DisposeAsync();

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task CreateChild_SetsPropertiesAndSharesParentChannel()
    {
        var received = new List<IProgressEvent>();
        var receivedLock = new object();
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) =>
            {
                lock (receivedLock) received.Add(evt);
            })
            .Returns(ValueTask.CompletedTask);

        var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider());

        var child = reporter.CreateChild("agent-A");

        // Properties carry correct scope.
        Assert.Equal("agent-A", child.AgentId);
        Assert.Equal(1, child.Depth);
        Assert.Equal("wf-1", child.WorkflowId);

        // Behavior: events reported via the CHILD must reach the parent's sink
        // (shared channel). This is the load-bearing assertion — the previous
        // test only checked properties and would pass even if the child spawned
        // its own dropped channel.
        var evtViaChild = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", "agent-A", null, 1, 100);
        var evtViaParent = new WorkflowStartedEvent(
            DateTimeOffset.UtcNow, "wf-1", null, null, 0, 101);

        child.Report(evtViaChild);
        reporter.Report(evtViaParent);

        await reporter.DisposeAsync();

        Assert.Equal(2, received.Count);
        Assert.Contains(received, e => ReferenceEquals(e, evtViaChild));
        Assert.Contains(received, e => ReferenceEquals(e, evtViaParent));
    }

    [Fact]
    public async Task NextSequence_Delegates()
    {
        await using var reporter = new ChannelProgressReporter(
            "wf-1", [], new ProgressSequenceProvider());

        var s1 = reporter.NextSequence();
        var s2 = reporter.NextSequence();

        Assert.True(s2 > s1);
    }

    [Fact]
    public async Task Consumer_SinkThrows_InvokesErrorHandler_AndContinues()
    {
        var boom = new InvalidOperationException("kaboom");
        var throwCountByEvent = new Dictionary<int, bool> { [0] = true, [1] = false, [2] = false };

        var throwingSink = new Mock<IProgressSink>();
        throwingSink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns<IProgressEvent, CancellationToken>((evt, _) =>
            {
                var seq = (int)evt.SequenceNumber;
                if (throwCountByEvent.TryGetValue(seq, out var shouldThrow) && shouldThrow)
                    throw boom;
                return ValueTask.CompletedTask;
            });

        var goodSinkReceived = new List<IProgressEvent>();
        var goodSink = new Mock<IProgressSink>();
        goodSink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IProgressEvent, CancellationToken>((evt, _) =>
            {
                lock (goodSinkReceived) goodSinkReceived.Add(evt);
            })
            .Returns(ValueTask.CompletedTask);

        var handler = new RecordingChannelErrorHandler();

        var reporter = new ChannelProgressReporter(
            "wf-1",
            [throwingSink.Object, goodSink.Object],
            new ProgressSequenceProvider(),
            handler);

        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 0));
        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1));
        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 2));

        await reporter.DisposeAsync();

        Assert.Single(handler.Records);
        Assert.Same(throwingSink.Object, handler.Records[0].Sink);
        Assert.Equal(0L, handler.Records[0].Event.SequenceNumber);
        Assert.Same(boom, handler.Records[0].Exception);

        Assert.Equal(3, goodSinkReceived.Count);
        Assert.Equal(new long[] { 0, 1, 2 }, goodSinkReceived.Select(e => e.SequenceNumber));
    }

    // ================================================================================
    // Full-channel enqueue: WriteAsync (Wait mode) must queue rather than silently drop via
    // TryWrite, deliver every event in channel order once capacity frees, and DisposeAsync must
    // wait for in-flight enqueues to resolve rather than racing the channel's completion against
    // a still-blocked write.
    // ================================================================================

    [Fact]
    public async Task Report_ChannelMomentarilyFull_QueuesRatherThanDrops_AllEventsDeliveredExactlyOnceInOrder()
    {
        var firstCallStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstCall = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<long>();
        var isFirstCall = 0;

        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async (IProgressEvent evt, CancellationToken _) =>
            {
                if (Interlocked.Exchange(ref isFirstCall, 1) == 0)
                {
                    // Only the very first delivered event blocks the consumer loop — long enough
                    // for capacity=1 to force every subsequently-reported event through the
                    // pending-enqueue (WriteAsync) path instead of completing synchronously.
                    firstCallStarted.TrySetResult(true);
                    await releaseFirstCall.Task;
                }

                lock (received)
                {
                    received.Add(evt.SequenceNumber);
                }
            });

        var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider(), capacity: 1);

        // Event 0 fills the sole buffer slot; the consumer immediately dequeues it (freeing the
        // slot) and then blocks inside the sink call above.
        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 0));
        await firstCallStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // The buffer is free again (event 0 was already dequeued), so event 1 enqueues
        // synchronously and re-fills the sole slot.
        reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 1));

        // Events 2..4 each find the channel full: with the old TryWrite-based Report, these would
        // have been silently dropped. They must instead be queued (via the async enqueue
        // observer) and eventually delivered, in order, once capacity frees.
        for (long seq = 2; seq <= 4; seq++)
        {
            reporter.Report(new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, seq));
        }

        releaseFirstCall.TrySetResult(true);

        await reporter.DisposeAsync();

        Assert.Equal(new long[] { 0, 1, 2, 3, 4 }, received);
    }

    [Fact]
    public async Task Report_AfterDisposeAsync_SurfacesEnqueueFailureThroughErrorHandler_DoesNotThrowSynchronously()
    {
        var handler = new RecordingChannelErrorHandler();
        var sink = new Mock<IProgressSink>();
        sink.Setup(s => s.OnEventAsync(It.IsAny<IProgressEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var reporter = new ChannelProgressReporter(
            "wf-1", [sink.Object], new ProgressSequenceProvider(), handler);

        await reporter.DisposeAsync();

        var lateEvent = new WorkflowStartedEvent(DateTimeOffset.UtcNow, "wf-1", null, null, 0, 99);

        // Enqueuing after the channel has been completed must never throw synchronously out of
        // Report — the failure is surfaced asynchronously through the error handler instead.
        var thrown = Record.Exception(() => reporter.Report(lateEvent));
        Assert.Null(thrown);

        // Poll briefly for the fire-and-forget continuation to observe the failed write.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (handler.Records.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        var record = Assert.Single(handler.Records);
        Assert.Same(sink.Object, record.Sink);
        Assert.Same(lateEvent, record.Event);
        Assert.IsType<ChannelClosedException>(record.Exception);
    }

    private sealed class RecordingChannelErrorHandler : IProgressReporterErrorHandler
    {
        private readonly List<(IProgressSink Sink, IProgressEvent Event, Exception Exception)> _records = new();

        public IReadOnlyList<(IProgressSink Sink, IProgressEvent Event, Exception Exception)> Records => _records;

        public void OnSinkException(IProgressSink sink, IProgressEvent progressEvent, Exception exception)
        {
            lock (_records)
            {
                _records.Add((sink, progressEvent, exception));
            }
        }
    }
}
