using System.Collections.Concurrent;
using System.Threading.Channels;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// Non-blocking <see cref="IProgressReporter"/> that pushes events to a
/// <see cref="Channel{T}"/> and drains them to sinks on a background task.
/// Use this when sinks do I/O (database, network) and you don't want to
/// block the agent pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Report"/> enqueues to the channel via <see cref="ChannelWriter{T}.WriteAsync"/>
/// (Wait mode — never <c>TryWrite</c>, so a full channel never silently drops an event) and
/// returns immediately whenever that enqueue completes synchronously (the common case: capacity
/// available). When the channel is momentarily full, the pending enqueue is instead handed to a
/// private fire-and-forget observer that awaits it in the background, so <see cref="Report"/>
/// itself never blocks the caller even while capacity is exhausted. A background consumer drains
/// events to all sinks in channel order, including every event whose enqueue only completed once
/// capacity freed up. Concurrent writers may interleave; each event's
/// <see cref="IProgressEvent.SequenceNumber"/> remains the authoritative global ordering key.
/// </para>
/// <para>
/// Any enqueue failure — most notably writing after the channel has already been completed —
/// is surfaced to the configured <see cref="IProgressReporterErrorHandler"/> (once per registered
/// sink) rather than swallowed, mirroring how a sink exception during consumption is reported.
/// </para>
/// <para>
/// <see cref="CreateChild"/> returns a lightweight wrapper that shares
/// the parent's channel — no additional background tasks are created.
/// </para>
/// <para>
/// Call <see cref="DisposeAsync"/> to drain remaining events and stop the background consumer.
/// Disposal first waits for every in-flight enqueue (a <see cref="Report"/> call still waiting for
/// channel capacity) to resolve — either by succeeding as the consumer frees capacity, or by
/// failing and being surfaced through the error handler — before marking the channel complete;
/// completing the channel while a write is still blocked on capacity would otherwise fail that
/// write outright instead of preserving its place in delivery order.
/// </para>
/// </remarks>
public sealed class ChannelProgressReporter : IProgressReporter, IProgressReporterContext, IAsyncDisposable
{
    private readonly Channel<IProgressEvent> _channel;
    private readonly IProgressSequence _sequence;
    private readonly IProgressReporterErrorHandler _errorHandler;
    private readonly IReadOnlyList<IProgressSink> _sinks;
    private readonly ConcurrentDictionary<Task, byte> _pendingWrites = new();
    private readonly Task _consumer;
    private readonly string? _parentAgentId;

    /// <summary>
    /// Creates a channel-based reporter with the given sinks.
    /// Starts a background consumer immediately.
    /// </summary>
    public ChannelProgressReporter(
        string workflowId,
        IReadOnlyList<IProgressSink> sinks,
        IProgressSequence sequence,
        IProgressReporterErrorHandler? errorHandler = null,
        string? agentId = null,
        string? parentAgentId = null,
        int depth = 0,
        int capacity = 1000)
    {
        WorkflowId = workflowId;
        _sequence = sequence;
        _errorHandler = errorHandler ?? new NullProgressReporterErrorHandler();
        _sinks = sinks;
        AgentId = agentId;
        _parentAgentId = parentAgentId;
        Depth = depth;

        _channel = Channel.CreateBounded<IProgressEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        _consumer = Task.Run(() => ConsumeAsync(sinks));
    }

    /// <inheritdoc />
    public string WorkflowId { get; }

    /// <inheritdoc />
    public string? AgentId { get; }

    /// <inheritdoc />
    public int Depth { get; }

    /// <inheritdoc />
    string? IProgressReporterContext.ParentAgentId => _parentAgentId;

    /// <inheritdoc />
    public long NextSequence() => _sequence.Next();

    /// <inheritdoc />
    /// <remarks>
    /// Never uses <c>TryWrite</c>: a full bounded channel would otherwise cause the event to be
    /// silently dropped. Instead this enqueues via <see cref="ChannelWriter{T}.WriteAsync"/> (the
    /// channel is configured with <see cref="BoundedChannelFullMode.Wait"/>) and returns
    /// immediately whenever that enqueue completes synchronously — the common case, since capacity
    /// is normally available. Only when the channel is momentarily full is the pending enqueue
    /// handed off to a private fire-and-forget observer that awaits it in the background, so this
    /// method itself never blocks the caller.
    /// </remarks>
    public void Report(IProgressEvent progressEvent)
    {
        var pendingWrite = _channel.Writer.WriteAsync(progressEvent);
        if (pendingWrite.IsCompletedSuccessfully)
        {
            // Common case: capacity was available, the write already landed synchronously.
            // Observe (and discard) the already-completed result so the ValueTask is considered
            // consumed, then return without ever touching the async observer path.
            pendingWrite.GetAwaiter().GetResult();
            return;
        }

        ObserveEnqueue(pendingWrite, progressEvent);
    }

    /// <summary>
    /// Fires off a background await of a not-yet-synchronously-completed enqueue attempt, tracking
    /// it so <see cref="DisposeAsync"/> can wait for it to resolve before completing the channel,
    /// and surfacing any failure (including writing after the channel has already been completed)
    /// through <see cref="IProgressReporterErrorHandler"/> instead of letting it disappear
    /// unobserved.
    /// </summary>
    private void ObserveEnqueue(ValueTask pendingWrite, IProgressEvent progressEvent)
    {
        var task = pendingWrite.AsTask();
        _pendingWrites[task] = 0;
        _ = AwaitEnqueueAsync(task, progressEvent);
    }

    private async Task AwaitEnqueueAsync(Task pendingWrite, IProgressEvent progressEvent)
    {
        try
        {
            await pendingWrite.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            NotifyEnqueueFailure(progressEvent, ex);
        }
        finally
        {
            _pendingWrites.TryRemove(pendingWrite, out _);
        }
    }

    private void NotifyEnqueueFailure(IProgressEvent progressEvent, Exception exception)
    {
        foreach (var sink in _sinks)
        {
            _errorHandler.OnSinkException(sink, progressEvent, exception);
        }
    }

    /// <inheritdoc />
    public IProgressReporter CreateChild(string agentId) =>
        new ChannelChildReporter(this, agentId, parentAgentId: AgentId, depth: Depth + 1);

    /// <summary>
    /// Waits for every in-flight enqueue to resolve, completes the channel, and waits for the
    /// background consumer to drain all remaining events to sinks.
    /// </summary>
    /// <remarks>
    /// Draining in-flight enqueues first is deliberate: completing the channel while a
    /// <see cref="Report"/> call is still blocked waiting for capacity would fail that write with
    /// a channel-closed exception (surfaced, but never delivered) rather than letting it land in
    /// order once the consumer frees capacity. Looping is required because awaiting the current
    /// snapshot of pending writes can itself free capacity for further writers already racing with
    /// this call.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        while (!_pendingWrites.IsEmpty)
        {
            var pending = _pendingWrites.Keys.ToArray();
            await Task.WhenAll(pending.Select(WithoutException)).ConfigureAwait(false);
        }

        _channel.Writer.TryComplete();
        await _consumer.ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits <paramref name="task"/> while swallowing its exception — already surfaced through
    /// <see cref="NotifyEnqueueFailure"/> by <see cref="AwaitEnqueueAsync"/> — so
    /// <see cref="Task.WhenAll(IEnumerable{Task})"/> in <see cref="DisposeAsync"/> never re-throws
    /// an already-handled enqueue failure.
    /// </summary>
    private static async Task WithoutException(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Already reported via the error handler in AwaitEnqueueAsync.
        }
    }

    private async Task ConsumeAsync(IReadOnlyList<IProgressSink> sinks)
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync())
            {
                for (int i = 0; i < sinks.Count; i++)
                {
                    try
                    {
                        await sinks[i].OnEventAsync(evt, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _errorHandler.OnSinkException(sinks[i], evt, ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
    }

    /// <summary>
    /// Lightweight child reporter that shares the parent's channel.
    /// No background task — writes go directly to the parent's channel.
    /// </summary>
    private sealed class ChannelChildReporter : IProgressReporter, IProgressReporterContext
    {
        private readonly ChannelProgressReporter _root;

        internal ChannelChildReporter(
            ChannelProgressReporter root,
            string agentId,
            string? parentAgentId,
            int depth)
        {
            _root = root;
            WorkflowId = root.WorkflowId;
            AgentId = agentId;
            ParentAgentId = parentAgentId;
            Depth = depth;
        }

        public string WorkflowId { get; }
        public string? AgentId { get; }
        public string? ParentAgentId { get; }
        public int Depth { get; }

        public long NextSequence() => _root.NextSequence();

        public void Report(IProgressEvent progressEvent) =>
            _root.Report(progressEvent);

        public IProgressReporter CreateChild(string agentId) =>
            new ChannelChildReporter(_root, agentId, parentAgentId: AgentId, depth: Depth + 1);
    }
}
