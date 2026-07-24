using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;
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

    internal static HarnessCapabilityProfile CreatePlanningProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner,
        bool includeTodo,
        bool includeAgentMode)
    {
        var requestedCapabilities = new HashSet<HarnessCapability>
        {
            HarnessCapability.GeneratedTools,
            HarnessCapability.FunctionInvocation,
            HarnessCapability.MessageInjection,
            HarnessCapability.OpenTelemetry,
        };
        if (includeTodo)
        {
            requestedCapabilities.Add(HarnessCapability.Todo);
        }
        if (includeAgentMode)
        {
            requestedCapabilities.Add(HarnessCapability.AgentMode);
        }

        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g3-planning-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: requestedCapabilities,
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
    }

    internal static HarnessCapabilityProfile CreateApprovalProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner,
        bool includeResponseBinding,
        bool includeNotRequiredBypassing,
        bool includeToolAutoApproval)
    {
        var requestedCapabilities = new HashSet<HarnessCapability>
        {
            HarnessCapability.GeneratedTools,
            HarnessCapability.FunctionInvocation,
            HarnessCapability.MessageInjection,
            HarnessCapability.OpenTelemetry,
        };
        if (includeResponseBinding)
        {
            requestedCapabilities.Add(HarnessCapability.ApprovalResponseBinding);
        }
        if (includeNotRequiredBypassing)
        {
            requestedCapabilities.Add(HarnessCapability.ApprovalNotRequiredBypassing);
        }
        if (includeToolAutoApproval)
        {
            requestedCapabilities.Add(HarnessCapability.ToolAutoApproval);
        }

        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g3-approval-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: requestedCapabilities,
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
    }

    internal static HarnessCapabilityProfile CreateSkillsProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner,
        bool includeSkills,
        bool includeApprovalResponseBinding)
    {
        var requestedCapabilities = new HashSet<HarnessCapability>
        {
            HarnessCapability.GeneratedTools,
            HarnessCapability.FunctionInvocation,
            HarnessCapability.MessageInjection,
            HarnessCapability.OpenTelemetry,
        };
        if (includeSkills)
        {
            requestedCapabilities.Add(HarnessCapability.Skills);
        }
        if (includeApprovalResponseBinding)
        {
            requestedCapabilities.Add(HarnessCapability.ApprovalResponseBinding);
        }

        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g3-skills-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: requestedCapabilities,
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
    }

    internal static HarnessCapabilityProfile CreateWebSearchProfile(
        HarnessToolLoopOwner toolLoopOwner,
        HarnessTelemetryOwner telemetryOwner,
        bool includeWebSearch,
        bool includeHostedWebSearchEvidence)
    {
        var requestedCapabilities = new HashSet<HarnessCapability>
        {
            HarnessCapability.GeneratedTools,
            HarnessCapability.FunctionInvocation,
            HarnessCapability.MessageInjection,
            HarnessCapability.OpenTelemetry,
        };
        if (includeWebSearch)
        {
            requestedCapabilities.Add(HarnessCapability.WebSearch);
        }

        var providerCapabilities = new HashSet<HarnessProviderCapability>();
        if (includeHostedWebSearchEvidence)
        {
            providerCapabilities.Add(HarnessProviderCapability.HostedWebSearch);
        }

        var resolver = new HarnessCapabilityResolver();
        return resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "g3-web-search-test",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: requestedCapabilities,
                ProviderCapabilities: providerCapabilities,
                ToolLoopOwner: toolLoopOwner,
                TelemetryOwner: telemetryOwner,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));
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
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            metrics,
            historyProvider,
            planningProviders: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics,
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders) =>
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            metrics,
            historyProvider,
            planningProviders,
            approvalPlugin: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics,
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders,
        HarnessApprovalPlugin? approvalPlugin) =>
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            metrics,
            historyProvider,
            planningProviders,
            approvalPlugin,
            skillsPlugin: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics,
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders,
        HarnessApprovalPlugin? approvalPlugin,
        HarnessSkillsPlugin? skillsPlugin) =>
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            metrics,
            historyProvider,
            planningProviders,
            approvalPlugin,
            skillsPlugin,
            progressAccessor: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics,
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders,
        HarnessApprovalPlugin? approvalPlugin,
        HarnessSkillsPlugin? skillsPlugin,
        IProgressReporterAccessor? progressAccessor) =>
        CreateRequest(
            chatClient,
            services,
            profile,
            tools,
            binding,
            accessor,
            metrics,
            historyProvider,
            planningProviders,
            approvalPlugin,
            skillsPlugin,
            progressAccessor,
            webSearchPlugin: null);

    internal static HarnessProviderCompositionRequest CreateRequest(
        IChatClient chatClient,
        IServiceProvider services,
        HarnessCapabilityProfile profile,
        HarnessGeneratedToolResolution tools,
        HarnessExecutionBinding binding,
        IAgentExecutionContextAccessor accessor,
        IAgentMetrics? metrics,
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders,
        HarnessApprovalPlugin? approvalPlugin,
        HarnessSkillsPlugin? skillsPlugin,
        IProgressReporterAccessor? progressAccessor,
        HarnessWebSearchPlugin? webSearchPlugin) =>
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
            PlanningProviders: planningProviders,
            ApprovalPlugin: approvalPlugin,
            SkillsPlugin: skillsPlugin,
            WebSearchPlugin: webSearchPlugin,
            Metrics: metrics,
            ProgressAccessor: progressAccessor);

    internal static ServiceProvider CreateServices() =>
        new ServiceCollection().BuildServiceProvider();

    internal static HarnessHistoryProviderPlugin CreateHistoryProviderPlugin(
        HarnessHistoryPersistenceMode persistenceMode,
        ChatHistoryProvider? callerSuppliedHistoryProvider) =>
        HarnessHistoryProviderPlugin.Create(
            persistenceMode,
            callerSuppliedHistoryProvider);

    internal static HarnessPlanningProvidersPlugin CreatePlanningProvidersPlugin(
        TodoProvider? todoProvider,
        AgentModeProvider? agentModeProvider) =>
        HarnessPlanningProvidersPlugin.Create(todoProvider, agentModeProvider);

    internal static HarnessApprovalPlugin CreateApprovalPlugin(
        bool responseBindingEnabled,
        bool notRequiredBypassingEnabled,
        ToolApprovalAgentOptions? toolApprovalOptions,
        HarnessApprovalHostValidator? hostValidator) =>
        HarnessApprovalPlugin.Create(
            responseBindingEnabled,
            notRequiredBypassingEnabled,
            toolApprovalOptions,
            hostValidator);

    internal static HarnessSkillsPlugin CreateSkillsPlugin(
        IReadOnlyList<AgentInlineSkill> skills,
        HarnessSkillsTrustPolicy trustPolicy) =>
        HarnessSkillsPlugin.Create(skills, trustPolicy);

    internal static HarnessWebSearchPlugin CreateWebSearchPlugin() =>
        HarnessWebSearchPlugin.Create();

    internal static IChatClient WithFoundryTelemetry(
        IChatClient inner,
        IAgentMetrics metrics) =>
        new DiagnosticsRecordingChatClient(
            inner,
            new DiagnosticsChatClientMiddleware(metrics));
}
