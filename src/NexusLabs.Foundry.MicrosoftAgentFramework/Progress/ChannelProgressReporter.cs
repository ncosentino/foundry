using System.Threading.Channels;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// Bounded, backpressured <see cref="IProgressReporter"/> that pushes events to a
/// <see cref="Channel{T}"/> and drains them to sinks on a background task.
/// Use this when sinks do I/O (database, network) and you want production decoupled
/// from consumption while capacity is available, without letting an unbounded backlog
/// of undelivered events accumulate in memory.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Report"/> enqueues to the channel via <see cref="ChannelWriter{T}.WriteAsync"/>
/// (Wait mode — never <c>TryWrite</c>, so a full channel never silently drops an event).
/// This is <strong>not fully non-blocking</strong>: whenever capacity is available — the
/// common case — the enqueue completes synchronously and <see cref="Report"/> returns
/// immediately. Only when the channel is momentarily saturated (the background consumer is
/// still busy with a slow sink and the buffer is full) does <see cref="Report"/> itself
/// synchronously wait for <see cref="ChannelWriter{T}.WriteAsync"/> to complete, applying
/// backpressure directly to the caller instead of accumulating an unbounded set of
/// fire-and-forget pending-write tasks. A background consumer drains events to all sinks in
/// channel order. Concurrent writers may interleave; each event's
/// <see cref="IProgressEvent.SequenceNumber"/> remains the authoritative global ordering key.
/// </para>
/// <para>
/// A <see cref="ChannelClosedException"/> from enqueueing after the channel has already been
/// completed (e.g. after <see cref="DisposeAsync"/>) — the one specific, expected failure mode
/// for a saturated <see cref="ChannelWriter{T}.WriteAsync"/> wait — is surfaced to the
/// configured <see cref="IProgressReporterErrorHandler"/> (once per registered sink) rather
/// than thrown out of <see cref="Report"/>, mirroring how a sink exception during consumption
/// is reported. Any other, unexpected exception from that wait propagates directly out of
/// <see cref="Report"/> instead of being reshaped into a handled error.
/// </para>
/// <para>
/// <see cref="CreateChild"/> returns a lightweight wrapper that shares
/// the parent's channel — no additional background tasks are created.
/// </para>
/// <para>
/// Call <see cref="DisposeAsync"/> to complete the writer and drain the background consumer.
/// Because <see cref="Report"/> never leaves an enqueue running in the background unobserved —
/// it either completes synchronously or the caller's own call is the one waiting on it —
/// there is no separate pending-write set for disposal to wait on: completing the writer and
/// awaiting the consumer task is sufficient.
/// </para>
/// </remarks>
public sealed class ChannelProgressReporter : IProgressReporter, IProgressReporterContext, IAsyncDisposable
{
    private readonly Channel<IProgressEvent> _channel;
    private readonly IProgressSequence _sequence;
    private readonly IProgressReporterErrorHandler _errorHandler;
    private readonly IReadOnlyList<IProgressSink> _sinks;
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
    /// is normally available. When the channel is momentarily saturated, this method synchronously
    /// blocks the caller until capacity frees, applying backpressure directly to the producer
    /// rather than growing an unbounded set of pending background writes. Writing after the
    /// channel has already been completed (e.g. after <see cref="DisposeAsync"/>) never throws out
    /// of this method: the resulting <see cref="ChannelClosedException"/> is instead surfaced
    /// through <see cref="IProgressReporterErrorHandler"/>, exactly like a sink exception during
    /// consumption. Any other, unexpected exception from the wait propagates directly out of this
    /// method instead of being caught and hidden.
    /// </remarks>
    public void Report(IProgressEvent progressEvent)
    {
        var pendingWrite = _channel.Writer.WriteAsync(progressEvent);
        if (pendingWrite.IsCompletedSuccessfully)
        {
            // Fast path: capacity was available, the write already landed synchronously. Observe
            // (and discard) the already-completed result so the ValueTask is considered consumed.
            pendingWrite.GetAwaiter().GetResult();
            return;
        }

        // Saturated path: the channel is momentarily full (or already completed). Block the
        // caller — deliberate producer backpressure — until capacity frees or the write
        // definitively fails, rather than handing the wait off to an unbounded background task
        // set. Only the specific, expected ChannelClosedException (writing after the channel has
        // completed) is surfaced through the error handler instead of propagating synchronously
        // out of Report; any other, unexpected exception propagates to the caller unchanged.
        try
        {
            pendingWrite.AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException ex)
        {
            NotifyEnqueueFailure(progressEvent, ex);
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
    /// Completes the channel and waits for the background consumer to drain all remaining events
    /// to sinks.
    /// </summary>
    /// <remarks>
    /// No in-flight enqueue can ever be orphaned by completing the channel here: every
    /// <see cref="Report"/> call that observed a saturated channel is, by construction, still
    /// synchronously blocked inside that same call waiting on its own <see cref="ChannelWriter{T}.WriteAsync"/>
    /// — there is no separate background task whose completion this method would otherwise need
    /// to await first.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _consumer.ConfigureAwait(false);
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
