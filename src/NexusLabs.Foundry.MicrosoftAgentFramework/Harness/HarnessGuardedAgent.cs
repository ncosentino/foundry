using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessGuardedAgent(
    AIAgent innerAgent,
    IHarnessMessageInjector? messageInjector)
    : DelegatingAIAgent(innerAgent)
{
    /// <inheritdoc />
    public override object? GetService(
        Type serviceType,
        object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is null &&
            serviceType == typeof(IHarnessMessageInjector))
        {
            return messageInjector;
        }

        if (typeof(IChatClient).IsAssignableFrom(serviceType) ||
            serviceType == typeof(IDisposable))
        {
            return null;
        }

        if (typeof(AIAgent).IsAssignableFrom(serviceType))
        {
            return serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;
        }

        return base.GetService(serviceType, serviceKey);
    }

    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(options);
        return base.RunCoreAsync(messages, session, options, cancellationToken);
    }

    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(options);
        return base.RunCoreStreamingAsync(
            messages,
            session,
            options,
            cancellationToken);
    }

    private static void EnsureSupported(AgentRunOptions? options)
    {
        if (options is not null)
        {
            throw new InvalidOperationException(
                "Harness composition does not allow per-run agent options.");
        }
    }
}
