using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessGuardedAgent(
    AIAgent innerAgent,
    IHarnessMessageInjector? messageInjector,
    HarnessExecutionBinding executionBinding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId,
    bool sessionContinuityEnabled,
    HarnessHistoryPersistenceMode historyPersistenceMode,
    IReadOnlyList<string> historyProviderStateKeys)
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

    protected override async ValueTask<AgentSession> CreateSessionCoreAsync(
        CancellationToken cancellationToken = default)
    {
        if (!sessionContinuityEnabled)
        {
            return await base
                .CreateSessionCoreAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        var session = await base
            .CreateSessionCoreAsync(cancellationToken)
            .ConfigureAwait(false);
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        return session;
    }

    protected override async ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!sessionContinuityEnabled)
        {
            return await base
                .SerializeSessionCoreAsync(session, jsonSerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        var innerSession = await base
            .SerializeSessionCoreAsync(session, jsonSerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        // Re-validate after the (synchronous-in-practice but awaited) inner call so a
        // context swap mid-operation cannot smuggle a stale identity into the envelope.
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        var envelope = new HarnessSessionEnvelope(
            HarnessSessionEnvelope.CurrentSchemaVersion,
            executionBinding.UserId,
            executionBinding.OrchestrationId,
            sessionId,
            historyPersistenceMode,
            historyProviderStateKeys,
            innerSession);
        return JsonSerializer.SerializeToElement(
            envelope,
            HarnessSessionEnvelopeJsonContext.Default.HarnessSessionEnvelope);
    }

    protected override async ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (!sessionContinuityEnabled)
        {
            return await base
                .DeserializeSessionCoreAsync(
                    serializedState,
                    jsonSerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        // Binding is validated before the serialized envelope is even inspected: a
        // deserialize attempt outside the authorized context fails closed regardless of
        // what the payload contains, and the active, host-authorized workspace is never
        // read from serialized state.
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        var envelope = DeserializeEnvelope(serializedState);
        ValidateEnvelope(envelope);

        var session = await base
            .DeserializeSessionCoreAsync(
                envelope.InnerSession,
                jsonSerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        return session;
    }

    private static void EnsureSupported(AgentRunOptions? options)
    {
        if (options is not null)
        {
            throw new InvalidOperationException(
                "Harness composition does not allow per-run agent options.");
        }
    }

    private static HarnessSessionEnvelope DeserializeEnvelope(JsonElement serializedState)
    {
        try
        {
            return JsonSerializer.Deserialize(
                serializedState,
                HarnessSessionEnvelopeJsonContext.Default.HarnessSessionEnvelope)
                ?? throw new InvalidOperationException(
                    "The serialized session envelope deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The serialized session is not a valid Harness session envelope.",
                exception);
        }
    }

    private void ValidateEnvelope(HarnessSessionEnvelope envelope)
    {
        if (envelope.SchemaVersion != HarnessSessionEnvelope.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The serialized session envelope schema version '{envelope.SchemaVersion}' " +
                $"does not match the supported version " +
                $"'{HarnessSessionEnvelope.CurrentSchemaVersion}'.");
        }

        if (!string.Equals(envelope.UserId, executionBinding.UserId, StringComparison.Ordinal) ||
            !string.Equals(
                envelope.OrchestrationId,
                executionBinding.OrchestrationId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The serialized session envelope identity does not match the trusted binding.");
        }

        if (!string.Equals(envelope.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The serialized session envelope session identifier does not match the " +
                "trusted binding.");
        }

        if (envelope.PersistenceMode != historyPersistenceMode)
        {
            throw new InvalidOperationException(
                $"The serialized session envelope persistence mode " +
                $"'{envelope.PersistenceMode}' does not match the configured mode " +
                $"'{historyPersistenceMode}'.");
        }

        if (envelope.ProviderStateKeys is null ||
            !envelope.ProviderStateKeys.SequenceEqual(
            historyProviderStateKeys,
            StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The serialized session envelope provider state keys do not match the " +
                "currently configured history provider.");
        }

        if (envelope.InnerSession.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException(
                "The serialized session envelope does not contain inner MAF session state.");
        }
    }
}
