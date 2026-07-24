using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessGuardedAgent(
    AIAgent innerAgent,
    HarnessGuardedAgentServices services,
    HarnessExecutionBinding executionBinding,
    IAgentExecutionContextAccessor executionContextAccessor,
    string sessionId,
    bool sessionContinuityEnabled,
    HarnessHistoryPersistenceMode historyPersistenceMode,
    IReadOnlyList<string> providerStateKeys,
    IReadOnlyList<HarnessCapability> enabledCapabilities,
    bool toolAutoApprovalEnabled,
    HarnessApprovalHostValidator? approvalHostValidator,
    IProgressReporterAccessor? progressAccessor)
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
            return services.MessageInjector;
        }

        if (serviceKey is null &&
            serviceType == typeof(IHarnessTodoAccessor))
        {
            return services.TodoAccessor;
        }

        if (serviceKey is null &&
            serviceType == typeof(IHarnessAgentModeAccessor))
        {
            return services.AgentModeAccessor;
        }

        if (typeof(IChatClient).IsAssignableFrom(serviceType) ||
            typeof(AIContextProvider).IsAssignableFrom(serviceType) ||
            typeof(ChatHistoryProvider).IsAssignableFrom(serviceType) ||
            serviceType == typeof(ChatClientAgentOptions) ||
            serviceType == typeof(ChatOptions) ||
            serviceType == typeof(IDisposable) ||
            serviceType == typeof(ToolApprovalAgentOptions))
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

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSupported(options);
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        var materializedMessages = AsReadOnlyList(messages);
        await EnsureApprovalReauthorizedAsync(materializedMessages, session, cancellationToken)
            .ConfigureAwait(false);

        var response = await base
            .RunCoreAsync(materializedMessages, session, options, cancellationToken)
            .ConfigureAwait(false);
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        ReportApprovalResponses(materializedMessages);
        ReportApprovalRequests(response.Messages, new HashSet<string>(StringComparer.Ordinal));
        return response;
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureSupported(options);
        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        var materializedMessages = AsReadOnlyList(messages);
        await EnsureApprovalReauthorizedAsync(materializedMessages, session, cancellationToken)
            .ConfigureAwait(false);
        var reportedApprovalRequests = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var update in base
            .RunCoreStreamingAsync(materializedMessages, session, options, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            ReportApprovalRequests(update.Contents.Count == 0
                ? []
                : [new ChatMessage(update.Role ?? ChatRole.Assistant, update.Contents)],
                reportedApprovalRequests);
            yield return update;
        }

        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
        ReportApprovalResponses(materializedMessages);
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
            providerStateKeys,
            enabledCapabilities,
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

    /// <summary>
    /// Requires successful host reauthorization, under
    /// <see cref="HarnessExecutionBinding.EnsureCurrent"/>, before a run that could create
    /// or rely on a standing ("always approve") tool approval is allowed to proceed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MAF 1.15's <c>ToolApprovalAgent</c> persists standing approval rules into
    /// <c>AgentSession.StateBag</c> with no public API to detect whether a restored session
    /// already carries one. This method therefore requires reauthorization conservatively
    /// whenever <paramref name="session"/> is non-<see langword="null"/> (a continued
    /// session that could carry a previously recorded rule) in addition to whenever the
    /// inbound <paramref name="messages"/> newly supply an
    /// <c>AlwaysApproveToolApprovalResponseContent</c>. A restored or newly supplied
    /// standing approval never becomes trusted solely because it exists: the host must
    /// affirmatively reauthorize it every time, and a missing validator, a declined
    /// reauthorization, or the delegate throwing all fail the run closed with zero tool
    /// invocations.
    /// </para>
    /// </remarks>
    private async ValueTask EnsureApprovalReauthorizedAsync(
        IReadOnlyList<ChatMessage> messages,
        AgentSession? session,
        CancellationToken cancellationToken)
    {
        if (!toolAutoApprovalEnabled)
        {
            return;
        }

        var standingToolNames = FindStandingApprovalToolNames(messages);
        if (standingToolNames.Count > 1)
        {
            throw new InvalidOperationException(
                "Standing tool approvals must be submitted one at a time for host " +
                "reauthorization.");
        }

        var hasNewStandingApproval = standingToolNames.Count == 1;
        var standingToolName = hasNewStandingApproval
            ? standingToolNames[0]
            : null;
        var isRestoredContinuation = session is not null;
        if (!hasNewStandingApproval && !isRestoredContinuation)
        {
            return;
        }

        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);

        if (approvalHostValidator is null)
        {
            throw new InvalidOperationException(
                "Standing tool approvals require a host reauthorization validator, but none " +
                "is configured.");
        }

        var reason = hasNewStandingApproval
            ? HarnessApprovalHostValidationReason.NewlySuppliedStandingApproval
            : HarnessApprovalHostValidationReason.ContinuedSessionReauthorization;
        var context = new HarnessApprovalHostValidationContext(
            executionBinding.UserId,
            executionBinding.OrchestrationId,
            sessionId,
            reason,
            standingToolName);

        var granted = await approvalHostValidator(context, cancellationToken)
            .ConfigureAwait(false);
        ReportStandingReauthorized(standingToolName, granted);

        if (!granted)
        {
            throw new InvalidOperationException(
                "Standing tool approval reauthorization was declined by the host.");
        }

        executionBinding.EnsureCurrent(executionContextAccessor, sessionId);
    }

    private void ReportApprovalRequests(
        IEnumerable<ChatMessage> messages,
        ISet<string> reportedRequestIds)
    {
        if (progressAccessor is null)
        {
            return;
        }

        var reporter = progressAccessor.Current;
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is ToolApprovalRequestContent request)
                {
                    if (!reportedRequestIds.Add(request.RequestId))
                    {
                        continue;
                    }

                    reporter.Report(new HarnessApprovalRequestedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: reporter.WorkflowId,
                        AgentId: reporter.AgentId,
                        ParentAgentId: null,
                        Depth: reporter.Depth,
                        SequenceNumber: reporter.NextSequence(),
                        RequestId: request.RequestId,
                        ToolName: ToolName(request.ToolCall)));
                }
            }
        }
    }

    private void ReportApprovalResponses(IReadOnlyList<ChatMessage> messages)
    {
        if (progressAccessor is null)
        {
            return;
        }

        var reporter = progressAccessor.Current;
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                // Standing responses are reported via ReportStandingReauthorized instead,
                // never both.
                if (content is AlwaysApproveToolApprovalResponseContent)
                {
                    continue;
                }

                if (content is not ToolApprovalResponseContent response)
                {
                    continue;
                }

                if (response.Approved)
                {
                    reporter.Report(new HarnessApprovalApprovedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: reporter.WorkflowId,
                        AgentId: reporter.AgentId,
                        ParentAgentId: null,
                        Depth: reporter.Depth,
                        SequenceNumber: reporter.NextSequence(),
                        RequestId: response.RequestId,
                        ToolName: ToolName(response.ToolCall)));
                }
                else
                {
                    reporter.Report(new HarnessApprovalRejectedEvent(
                        Timestamp: DateTimeOffset.UtcNow,
                        WorkflowId: reporter.WorkflowId,
                        AgentId: reporter.AgentId,
                        ParentAgentId: null,
                        Depth: reporter.Depth,
                        SequenceNumber: reporter.NextSequence(),
                        RequestId: response.RequestId,
                        ToolName: ToolName(response.ToolCall),
                        Reason: response.Reason));
                }
            }
        }
    }

    private void ReportStandingReauthorized(string? toolName, bool granted)
    {
        if (progressAccessor is null)
        {
            return;
        }

        var reporter = progressAccessor.Current;
        reporter.Report(new HarnessApprovalStandingReauthorizedEvent(
            Timestamp: DateTimeOffset.UtcNow,
            WorkflowId: reporter.WorkflowId,
            AgentId: reporter.AgentId,
            ParentAgentId: null,
            Depth: reporter.Depth,
            SequenceNumber: reporter.NextSequence(),
            ToolName: toolName,
            Granted: granted));
    }

    private static string ToolName(ToolCallContent? toolCall) =>
        (toolCall as FunctionCallContent)?.Name ?? "unknown";

    private static IReadOnlyList<string?> FindStandingApprovalToolNames(
        IReadOnlyList<ChatMessage> messages)
    {
        var toolNames = new List<string?>();
        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is AlwaysApproveToolApprovalResponseContent always)
                {
                    toolNames.Add(
                        always.InnerResponse.ToolCall is FunctionCallContent function
                            ? function.Name
                            : null);
                }
            }
        }

        return toolNames;
    }

    private static IReadOnlyList<ChatMessage> AsReadOnlyList(IEnumerable<ChatMessage> messages) =>
        messages as IReadOnlyList<ChatMessage> ?? [.. messages];

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
            providerStateKeys,
            StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The serialized session envelope provider state keys do not match the " +
                "currently configured selected providers.");
        }

        if (envelope.EnabledCapabilities is null ||
            !envelope.EnabledCapabilities.SequenceEqual(enabledCapabilities))
        {
            throw new InvalidOperationException(
                "The serialized session envelope enabled capabilities do not match the " +
                "currently configured Harness profile.");
        }

        if (envelope.InnerSession.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException(
                "The serialized session envelope does not contain inner MAF session state.");
        }
    }
}
