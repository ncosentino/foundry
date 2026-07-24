using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

internal static class HarnessCompositionTestFixture
{
    internal const string SessionId = "session-1";

    internal static HarnessCapabilityProfile CreateProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner)
    {
        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g2-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G2,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
    }

    internal static HarnessCapabilityProfile CreateHistoryProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner,
        HarnessHistoryPersistenceMode historyPersistenceMode)
    {
        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g3-history-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.GeneratedTools,
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.MessageInjection,
                    HarnessCapability.OpenTelemetry,
                    HarnessCapability.PerServiceHistory,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: historyPersistenceMode));
    }

    internal static HarnessExecutionBinding CaptureBinding(
        AgentExecutionContextAccessor accessor,
        out IDisposable scope)
    {
        scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: new InMemoryWorkspace()));
        var capture = HarnessExecutionBinding.Capture(
            accessor,
            SessionId,
            requireWorkspace: true);
        Assert.Equal(HarnessExecutionBindingStatus.Valid, capture.Status);
        return Assert.IsType<HarnessExecutionBinding>(capture.Binding);
    }

    internal static HarnessGeneratedToolResolution CreateToolResolution(
        AIFunction function) =>
        new(
            HarnessGeneratedToolResolutionStatus.Success,
            [function],
            [],
            []);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor) =>
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            historyProvider: null,
            metrics: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics) =>
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            metrics,
            historyProvider: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics,
        HarnessHistoryProviderPlugin? historyProvider) =>
        new(
            ChatClient: chatClient,
            Services: services,
            LoggerFactory: NullLoggerFactory.Instance,
            Name: "g2-agent",
            Description: "G2 composition test agent",
            Instructions: "Use the supplied tool.",
            Profile: profile,
            GeneratedTools: tools,
            ExecutionBinding: binding,
            ExecutionContextAccessor: accessor,
            SessionId: SessionId,
            HistoryProvider: historyProvider,
            Metrics: metrics,
            ProgressAccessor: null);

    internal static ServiceProvider CreateServices() =>
        new ServiceCollection().BuildServiceProvider();

    internal static HarnessHistoryProviderPlugin CreateHistoryProviderPlugin(
        HarnessHistoryPersistenceMode persistenceMode,
        ChatHistoryProvider? callerSuppliedHistoryProvider) =>
        HarnessHistoryProviderPlugin.Create(
            persistenceMode,
            callerSuppliedHistoryProvider);

    internal static IChatClient WithFoundryTelemetry(
        IChatClient inner,
        IAgentMetrics metrics) =>
        new DiagnosticsRecordingChatClient(
            inner,
            new DiagnosticsChatClientMiddleware(metrics));
}
