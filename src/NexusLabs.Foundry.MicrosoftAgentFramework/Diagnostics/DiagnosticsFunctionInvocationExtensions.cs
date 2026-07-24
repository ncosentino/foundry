using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Extension methods for wiring <see cref="DiagnosticsFunctionInvokingChatClient"/>
/// into an MEAI chat client pipeline.
/// </summary>
public static class DiagnosticsFunctionInvocationExtensions
{
    /// <summary>
    /// Inserts a <see cref="DiagnosticsFunctionInvokingChatClient"/> into the pipeline
    /// that records per-tool-call diagnostics, OTel metrics, and Activity spans.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces the standard <c>UseFunctionInvocation()</c> call. Do not use both —
    /// it would create two <c>FunctionInvokingChatClient</c> layers and produce
    /// duplicate tool calls.
    /// </para>
    /// </remarks>
    /// <param name="builder">The chat client builder.</param>
    /// <param name="metrics">Optional OTel metrics recorder.</param>
    /// <param name="progressAccessor">Optional progress reporter for real-time events.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ChatClientBuilder UseDiagnosticsFunctionInvocation(
        this ChatClientBuilder builder,
        IAgentMetrics? metrics = null,
        IProgressReporterAccessor? progressAccessor = null)
        => UseDiagnosticsFunctionInvocation(
            builder,
            loggerFactory: null,
            metrics,
            progressAccessor);

    internal static ChatClientBuilder UseDiagnosticsFunctionInvocation(
        this ChatClientBuilder builder,
        ILoggerFactory? loggerFactory,
        IAgentMetrics? metrics,
        IProgressReporterAccessor? progressAccessor)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Use((innerClient, services) =>
            new DiagnosticsFunctionInvokingChatClient(
                innerClient,
                loggerFactory ?? services.GetService<ILoggerFactory>(),
                services,
                metrics,
                progressAccessor));
    }
}
