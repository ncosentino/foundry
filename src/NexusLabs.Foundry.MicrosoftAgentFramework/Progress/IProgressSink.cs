namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// Receives progress events as they occur during agent/workflow execution.
/// Implement this interface to build SSE streams, console displays, trace diagrams, etc.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Auto-discovery (simple apps):</strong> If Foundry's source generator or
/// reflection-based scanning is active, any class implementing
/// <see cref="IProgressSink"/> is automatically registered in DI as a singleton.
/// These auto-discovered sinks become the <em>default sinks</em> used by
/// <see cref="IProgressReporterFactory.Create(string)"/>.
/// You can also register sinks manually via
/// <c>services.AddSingleton&lt;IProgressSink, MySink&gt;()</c> — all DI-registered
/// <see cref="IProgressSink"/> instances are treated as defaults.
/// </para>
/// <para>
/// <strong>Per-orchestration sinks (complex apps):</strong> Use
/// <see cref="IProgressReporterFactory.Create(string, IEnumerable{IProgressSink})"/>
/// to pass sinks explicitly for a specific workflow run. This overload ignores the
/// default sinks entirely, giving multi-tenant or multi-workflow applications full
/// control over which sinks receive events for each orchestration.
/// </para>
/// <para>
/// <strong>Performance:</strong> Implementations should be fast — a slow sink delays
/// the agent pipeline. Use <c>ChannelProgressReporter</c> for non-blocking delivery
/// when sinks perform I/O.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Auto-discovered by Foundry — no explicit registration needed.
/// public sealed class ConsoleSink : IProgressSink
/// {
///     public ValueTask OnEventAsync(IProgressEvent e, CancellationToken ct)
///     {
///         Console.WriteLine($"[{e.WorkflowId}] {e.GetType().Name}");
///         return ValueTask.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IProgressSink
{
    /// <summary>
    /// Called for each progress event. Implementations should be fast — a slow sink
    /// delays the agent pipeline (use <c>ChannelProgressReporter</c> for non-blocking delivery).
    /// </summary>
    ValueTask OnEventAsync(IProgressEvent progressEvent, CancellationToken cancellationToken);
}
