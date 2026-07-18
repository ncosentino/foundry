using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Budget;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Budget;

/// <summary>
/// Extension methods for wiring token-budget enforcement into the agent framework.
/// </summary>
public static class TokenBudgetExtensions
{
    /// <summary>
    /// Wires <see cref="TokenUsageRecordingMiddleware"/> to record token usage from
    /// every LLM call into <see cref="ITokenBudgetTracker"/>. This enables
    /// <see cref="ITokenBudgetTracker.CurrentTokens"/> without enforcing any budget.
    /// </summary>
    /// <remarks>
    /// Idempotent — calling this multiple times (or via both <c>UsingTokenBudget()</c>
    /// and <c>UsingDiagnostics()</c>) wires the recording middleware exactly once.
    /// </remarks>
    public static AgentFrameworkBuilder UsingTokenTracking(
        this AgentFrameworkBuilder syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        if (syringe.TokenTrackingWired) return syringe;

        var result = syringe.Configure(opts =>
        {
            var tracker = opts.ServiceProvider.GetRequiredService<ITokenBudgetTracker>();
            var existingFactory = opts.ChatClientFactory;
            opts.ChatClientFactory = sp =>
            {
                var innerClient = existingFactory?.Invoke(sp)
                    ?? sp.GetRequiredService<IChatClient>();
                return new TokenUsageRecordingMiddleware(innerClient, tracker);
            };
        });

        return result with { TokenTrackingWired = true };
    }

    /// <summary>
    /// Wraps the configured <see cref="IChatClient"/> with <see cref="TokenBudgetChatMiddleware"/>,
    /// enabling per-pipeline token budgets via <see cref="ITokenBudgetTracker"/>.
    /// Automatically includes <see cref="UsingTokenTracking"/> for token recording.
    /// </summary>
    public static AgentFrameworkBuilder UsingTokenBudget(
        this AgentFrameworkBuilder syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        syringe = syringe.UsingTokenTracking();

        return syringe.Configure(opts =>
        {
            var tracker = opts.ServiceProvider.GetRequiredService<ITokenBudgetTracker>();
            var progressAccessor = opts.ServiceProvider.GetRequiredService<IProgressReporterAccessor>();

            var existingFactory = opts.ChatClientFactory;
            opts.ChatClientFactory = sp =>
            {
                var innerClient = existingFactory?.Invoke(sp)
                    ?? sp.GetRequiredService<IChatClient>();

                return new TokenBudgetChatMiddleware(innerClient, tracker, progressAccessor);
            };
        });
    }
}
