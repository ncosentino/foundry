using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessProviderComposition
{
    private static readonly IReadOnlySet<HarnessCapability> HistorySupportedCapabilities =
        new HashSet<HarnessCapability>
        {
            HarnessCapability.GeneratedTools,
            HarnessCapability.FunctionInvocation,
            HarnessCapability.MessageInjection,
            HarnessCapability.OpenTelemetry,
            HarnessCapability.PerServiceHistory,
        };

    internal HarnessProviderCompositionResult Compose(
        HarnessProviderCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ChatClient);
        ArgumentNullException.ThrowIfNull(request.Services);
        ArgumentNullException.ThrowIfNull(request.LoggerFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.GeneratedTools);
        ArgumentNullException.ThrowIfNull(request.ExecutionBinding);
        ArgumentNullException.ThrowIfNull(request.ExecutionContextAccessor);

        var historyGuard = HarnessHistoryCompositionGuard.Validate(
            request.Profile,
            request.HistoryProvider);
        if (historyGuard.Status != HarnessHistoryCompositionGuardStatus.Valid)
        {
            return Failure(
                MapHistoryStatus(historyGuard.Status),
                request.Profile,
                historyGuard.Detail);
        }

        var bindingValidation = request.ExecutionBinding.ValidateCurrent(
            request.ExecutionContextAccessor,
            request.SessionId);
        if (bindingValidation.Status != HarnessExecutionBindingStatus.Valid)
        {
            return Failure(
                HarnessProviderCompositionStatus.ExecutionBindingInvalid,
                request.Profile,
                bindingValidation.Detail);
        }

        var guard = request.HistoryProvider is null
            ? HarnessCompositionGuard.Validate(
                request.ChatClient,
                request.Profile)
            : HarnessCompositionGuard.Validate(
                request.ChatClient,
                request.Profile,
                HistorySupportedCapabilities);
        if (guard.Status != HarnessCompositionGuardStatus.Valid)
        {
            return Failure(
                HarnessProviderCompositionStatus.CompositionGuardRejected,
                request.Profile,
                guard.Detail);
        }

        if (request.GeneratedTools.Status !=
            HarnessGeneratedToolResolutionStatus.Success)
        {
            return Failure(
                HarnessProviderCompositionStatus.GeneratedToolResolutionFailed,
                request.Profile,
                $"Generated tool resolution failed with status '{request.GeneratedTools.Status}'.");
        }

        var generatedToolsState =
            request.Profile.Capabilities[HarnessCapability.GeneratedTools].EffectiveState;
        if (generatedToolsState == HarnessCapabilityState.Enabled &&
            request.GeneratedTools.Functions.Count == 0)
        {
            return Failure(
                HarnessProviderCompositionStatus.GeneratedToolsEmpty,
                request.Profile,
                "The profile enables generated tools but no generated tools were resolved.");
        }

        if (generatedToolsState != HarnessCapabilityState.Enabled &&
            request.GeneratedTools.Functions.Count > 0)
        {
            return Failure(
                HarnessProviderCompositionStatus.GeneratedToolsNotEnabled,
                request.Profile,
                "Generated tools were supplied while the capability profile disables them.");
        }

        var telemetryEnabled =
            request.Profile.Capabilities[HarnessCapability.OpenTelemetry].EffectiveState ==
            HarnessCapabilityState.Enabled;
        if (telemetryEnabled &&
            request.Profile.TelemetryOwner == HarnessTelemetryOwner.Foundry &&
            request.Metrics is null)
        {
            return Failure(
                HarnessProviderCompositionStatus.FoundryMetricsMissing,
                request.Profile,
                "Foundry telemetry ownership requires an explicit metrics owner.");
        }

        var foundryMetrics = telemetryEnabled &&
            request.Profile.TelemetryOwner == HarnessTelemetryOwner.Foundry
                ? request.Metrics
                : null;
        var builder = request.ChatClient.AsBuilder();
        builder = request.Profile.ToolLoopOwner == HarnessToolLoopOwner.Foundry
            ? builder.UseDiagnosticsFunctionInvocation(
                request.LoggerFactory,
                foundryMetrics,
                request.ProgressAccessor)
            : builder.UseFunctionInvocation(request.LoggerFactory);

        var messageInjectionEnabled =
            request.Profile.Capabilities[HarnessCapability.MessageInjection].EffectiveState ==
            HarnessCapabilityState.Enabled;
        if (messageInjectionEnabled)
        {
            builder = builder.UseMessageInjection();
        }

        builder = builder.Use(innerClient =>
            new HarnessExecutionBindingChatClient(
                innerClient,
                request.ExecutionBinding,
                request.ExecutionContextAccessor,
                request.SessionId));

        if (request.HistoryProvider is not null)
        {
            builder = request.HistoryProvider.UsePerServiceCallChatHistoryPersistence(
                builder);
        }

        if (telemetryEnabled &&
            request.Profile.TelemetryOwner == HarnessTelemetryOwner.Foundry)
        {
            builder = builder.Use(innerClient =>
                new DiagnosticsRecordingChatClient(
                    innerClient,
                    new DiagnosticsChatClientMiddleware(
                        foundryMetrics,
                        request.ProgressAccessor)));
        }
        else if (telemetryEnabled &&
            request.Profile.TelemetryOwner == HarnessTelemetryOwner.Harness)
        {
            builder = builder.UseOpenTelemetry();
        }

        var chatOptions = new ChatOptions
        {
            Instructions = request.Instructions,
            Tools = [.. request.GeneratedTools.Functions],
        };
        var historyOptions = request.HistoryProvider?.GetAgentOptionsConfiguration();
        var agent = builder.BuildAIAgent(
            new ChatClientAgentOptions
            {
                Name = request.Name,
                Description = request.Description,
                ChatOptions = chatOptions,
                UseProvidedChatClientAsIs = true,
                ChatHistoryProvider = historyOptions?.ChatHistoryProvider,
                RequirePerServiceCallChatHistoryPersistence =
                    historyOptions?.RequirePerServiceCallChatHistoryPersistence ?? false,
            },
            request.LoggerFactory,
            request.Services);
        var functionInvokingChatClient =
            agent.GetService<FunctionInvokingChatClient>();
        if (functionInvokingChatClient is null)
        {
            return Failure(
                HarnessProviderCompositionStatus.FunctionInvocationLoopMissing,
                request.Profile,
                "The composed agent did not expose its function invocation loop.");
        }

        functionInvokingChatClient.FunctionInvoker = async (
            context,
            cancellationToken) =>
        {
            // MEAI may convert invocation exceptions into tool results, so the
            // chat-client binding checks must also remain in the pipeline.
            request.ExecutionBinding.EnsureCurrent(
                request.ExecutionContextAccessor,
                request.SessionId);
            return await context.Function.InvokeAsync(
                context.Arguments,
                cancellationToken);
        };

        IHarnessMessageInjector? messageInjector = null;
        if (messageInjectionEnabled)
        {
            var messageInjectingChatClient =
                agent.GetService<MessageInjectingChatClient>();
            if (messageInjectingChatClient is null)
            {
                return Failure(
                    HarnessProviderCompositionStatus.MessageInjectionMissing,
                    request.Profile,
                    "The composed agent did not expose its message injection middleware.");
            }

            messageInjector = new HarnessMessageInjector(
                messageInjectingChatClient,
                request.ExecutionBinding,
                request.ExecutionContextAccessor,
                request.SessionId);
        }

        return new HarnessProviderCompositionResult(
            HarnessProviderCompositionStatus.Success,
            new HarnessGuardedAgent(
                agent,
                messageInjector,
                request.ExecutionBinding,
                request.ExecutionContextAccessor,
                request.SessionId,
                request.HistoryProvider is not null,
                request.HistoryProvider?.PersistenceMode ??
                    HarnessHistoryPersistenceMode.NotApplicable,
                request.HistoryProvider?.ProviderStateKeys ?? Array.Empty<string>()),
            request.Profile,
            null);
    }

    private static HarnessProviderCompositionStatus MapHistoryStatus(
        HarnessHistoryCompositionGuardStatus status) =>
        status switch
        {
            HarnessHistoryCompositionGuardStatus.HistoryProviderRequired =>
                HarnessProviderCompositionStatus.HistoryProviderRequired,
            HarnessHistoryCompositionGuardStatus.HistoryProviderUnexpected =>
                HarnessProviderCompositionStatus.HistoryProviderUnexpected,
            HarnessHistoryCompositionGuardStatus.PersistenceModeMismatch =>
                HarnessProviderCompositionStatus.HistoryProviderModeMismatch,
            HarnessHistoryCompositionGuardStatus.UnsupportedPersistenceMode =>
                HarnessProviderCompositionStatus.HistoryProviderUnsupportedPersistenceMode,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static HarnessProviderCompositionResult Failure(
        HarnessProviderCompositionStatus status,
        HarnessCapabilityProfile profile,
        string? detail) =>
        new(status, null, profile, detail);
}
