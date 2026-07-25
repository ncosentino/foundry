using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessProviderComposition
{
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

        var planningGuard = HarnessPlanningCompositionGuard.Validate(
            request.Profile,
            request.PlanningProviders);
        if (planningGuard.Status != HarnessPlanningCompositionGuardStatus.Valid)
        {
            return Failure(
                MapPlanningStatus(planningGuard.Status),
                request.Profile,
                planningGuard.Detail);
        }

        var approvalGuard = HarnessApprovalCompositionGuard.Validate(
            request.Profile,
            request.ApprovalPlugin);
        if (approvalGuard.Status != HarnessApprovalCompositionGuardStatus.Valid)
        {
            return Failure(
                MapApprovalStatus(approvalGuard.Status),
                request.Profile,
                approvalGuard.Detail);
        }

        var skillsGuard = HarnessSkillsCompositionGuard.Validate(
            request.Profile,
            request.SkillsPlugin,
            request.ApprovalPlugin);
        if (skillsGuard.Status != HarnessSkillsCompositionGuardStatus.Valid)
        {
            return Failure(
                MapSkillsStatus(skillsGuard.Status),
                request.Profile,
                skillsGuard.Detail);
        }

        var webSearchGuard = HarnessWebSearchCompositionGuard.Validate(
            request.Profile,
            request.WebSearchPlugin,
            request.GeneratedTools.Functions);
        if (webSearchGuard.Status != HarnessWebSearchCompositionGuardStatus.Valid)
        {
            return Failure(
                MapWebSearchStatus(webSearchGuard.Status),
                request.Profile,
                webSearchGuard.Detail);
        }

        var providerStateKeysResult = BuildProviderStateKeys(
            request.HistoryProvider,
            request.PlanningProviders,
            request.SkillsPlugin);
        if (providerStateKeysResult.Status != HarnessProviderCompositionStatus.Success)
        {
            return Failure(
                providerStateKeysResult.Status,
                request.Profile,
                providerStateKeysResult.Detail);
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

        // This is the one coherent capability profile/composition request: the narrow compaction
        // composer is invoked here, internally, against the exact same request.Profile,
        // request.ChatClient, execution binding/accessor/session this method itself received — never
        // a second, independently-resolved profile. Its statuses are mapped into this method's own
        // HarnessProviderCompositionStatus, and every later step in this method builds the rest of the
        // pipeline from the chat client it returns rather than request.ChatClient directly, so the
        // hybrid compaction node (when installed) always ends up at the innermost, real-provider-facing
        // position beneath every other Foundry-owned middleware layered on below.
        var compactionResult = new HarnessCompactionComposition().Compose(
            new HarnessCompactionCompositionRequest(
                request.ChatClient,
                request.Profile,
                request.HybridProfile,
                request.ExecutionBinding,
                request.ExecutionContextAccessor,
                request.SessionId,
                request.ProgressAccessor));
        if (compactionResult.Status is not (
            HarnessCompactionCompositionStatus.Disabled or HarnessCompactionCompositionStatus.Success))
        {
            return Failure(
                MapCompactionStatus(compactionResult.Status),
                request.Profile,
                compactionResult.Detail);
        }
        var effectiveChatClient = compactionResult.ChatClient!;
        // Non-null only when compaction was actually installed above: this is the exact same
        // coordinator instance HarnessHybridCompactionChatClient reads/marks against, so HarnessGuardedAgent
        // begins the one outer-run scope every nested provider call within this composed agent's run
        // observes and contributes to.
        var compactionRunCoordinator = compactionResult.Coordinator;

        var supportedCapabilities = BuildSupportedCapabilities(
            request.HistoryProvider,
            request.PlanningProviders,
            request.ApprovalPlugin,
            request.SkillsPlugin,
            request.WebSearchPlugin,
            hybridProfileSupplied: request.HybridProfile is not null);
        var enabledCapabilities = request.Profile.Capabilities.Values
            .Where(evidence =>
                evidence.EffectiveState == HarnessCapabilityState.Enabled)
            .Select(evidence => evidence.Capability)
            .OrderBy(capability => capability)
            .ToArray();
        var guard = supportedCapabilities.SetEquals(HarnessCompositionGuard.G2SupportedCapabilities)
            ? HarnessCompositionGuard.Validate(
                effectiveChatClient,
                request.Profile)
            : HarnessCompositionGuard.Validate(
                effectiveChatClient,
                request.Profile,
                supportedCapabilities);
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
        var builder = effectiveChatClient.AsBuilder();

        if (request.ApprovalPlugin is not null)
        {
            builder = builder.Use(innerClient =>
                new HarnessExecutionBindingChatClient(
                    innerClient,
                    request.ExecutionBinding,
                    request.ExecutionContextAccessor,
                    request.SessionId));
        }

        // MAF's canonical pipeline order places approval response binding outermost,
        // then not-required-bypassing, then function invocation (see
        // ChatClientExtensions.WithDefaultAgentMiddleware upstream), so both approval
        // decorators are added before the function-invocation decorator below, using the
        // same public ChatClientBuilderExtensions seam already used for message injection
        // and history persistence elsewhere in this method. The additional binding wrapper
        // above these decorators prevents them from reading or mutating approval session
        // state before the trusted execution context has been validated.
        if (request.ApprovalPlugin?.ResponseBindingEnabled == true)
        {
            builder = builder.UseApprovalResponseBinding(request.LoggerFactory);
        }
        if (request.ApprovalPlugin?.NotRequiredBypassingEnabled == true)
        {
            builder = builder.UseApprovalNotRequiredFunctionBypassing(request.LoggerFactory);
        }

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

        var tools = new List<AITool>(request.GeneratedTools.Functions);
        if (request.WebSearchPlugin is not null)
        {
            tools.Add(request.WebSearchPlugin.Tool);
        }
        var chatOptions = new ChatOptions
        {
            Instructions = request.Instructions,
            Tools = tools,
        };
        var historyOptions = request.HistoryProvider?.GetAgentOptionsConfiguration();
        // Planning and Skills each contribute their own AIContextProvider set independently;
        // this union generalizes across both without a combinatorial static list per
        // plugin-presence combination (mirrors the state-key union in BuildProviderStateKeys).
        var aiContextProviders = new List<AIContextProvider>();
        if (request.PlanningProviders is not null)
        {
            aiContextProviders.AddRange(request.PlanningProviders.AIContextProviders);
        }
        if (request.SkillsPlugin is not null)
        {
            aiContextProviders.Add(request.SkillsPlugin.SkillsProvider);
        }
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
                AIContextProviders = aiContextProviders.Count > 0 ? aiContextProviders : null,
                // These two flags reflect effective profile state for documentation and
                // defense-in-depth, but MAF documents them as having no effect when
                // UseProvidedChatClientAsIs is true (always true above): the real
                // behavioral control is the builder.UseApprovalResponseBinding /
                // .UseApprovalNotRequiredFunctionBypassing calls above.
                DisableApprovalResponseBinding =
                    request.ApprovalPlugin?.ResponseBindingEnabled != true,
                DisableApprovalNotRequiredFunctionBypassing =
                    request.ApprovalPlugin?.NotRequiredBypassingEnabled != true,
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

        // OffloadPlugin only carries the byte-threshold policy inputs; the actual policy reuses
        // this composition request's own existing trusted ExecutionBinding/SessionId rather than
        // minting a separate binding or session identity.
        var offloadPolicy = request.OffloadPlugin is not null
            ? HarnessToolResultOffloadPolicy.Create(
                request.OffloadPlugin.MaximumInlineToolResultBytes,
                request.SessionId,
                request.OffloadPlugin.DescriptionStrategy,
                request.OffloadPlugin.Checkpoint)
            : null;

        functionInvokingChatClient.FunctionInvoker = async (
            context,
            cancellationToken) =>
        {
            // MEAI may convert invocation exceptions into tool results, so the
            // chat-client binding checks must also remain in the pipeline.
            request.ExecutionBinding.EnsureCurrent(
                request.ExecutionContextAccessor,
                request.SessionId);
            var rawResult = await context.Function.InvokeAsync(
                context.Arguments,
                cancellationToken);

            if (offloadPolicy is null)
            {
                return rawResult;
            }

            // Same shared, caller-agnostic transform IterativeAgentLoop uses.
            // Inline -> pass the original raw object through unchanged (FICC's own passthrough
            // check only special-cases an already-constructed FunctionResultContent with a
            // matching CallId, so a plain raw object here still round-trips normally). Every
            // other outcome constructs a matching-call-id FunctionResultContent directly so FICC's
            // passthrough returns it unmodified instead of re-wrapping/re-serializing it.
            var outcome = HarnessToolResultOffloadTransform.Transform(
                new HarnessToolResultOffloadRequest(
                    rawResult,
                    context.CallContent.Name,
                    context.CallContent.CallId,
                    request.ExecutionBinding,
                    request.ExecutionContextAccessor,
                    offloadPolicy,
                    DateTimeOffset.UtcNow,
                    cancellationToken,
                    request.ProgressAccessor));

            return outcome.Status switch
            {
                HarnessToolResultOffloadStatus.Inline => outcome.RawResult,
                HarnessToolResultOffloadStatus.Offloaded or
                HarnessToolResultOffloadStatus.ExistingReference =>
                    new FunctionResultContent(context.CallContent.CallId, outcome.ReferenceText),
                _ => new FunctionResultContent(
                    context.CallContent.CallId,
                    $"Error: {outcome.Evidence}"),
            };
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

        IHarnessTodoAccessor? todoAccessor =
            request.PlanningProviders?.TodoProvider is { } todoProvider
                ? new HarnessTodoAccessor(
                    todoProvider,
                    request.ExecutionBinding,
                    request.ExecutionContextAccessor,
                    request.SessionId)
                : null;
        IHarnessAgentModeAccessor? agentModeAccessor =
            request.PlanningProviders?.AgentModeProvider is { } agentModeProvider
                ? new HarnessAgentModeAccessor(
                    agentModeProvider,
                    request.ExecutionBinding,
                    request.ExecutionContextAccessor,
                    request.SessionId)
                : null;

        // Chat-client middleware services (function invocation, message injection) are
        // resolved above, from the raw ChatClientAgent, before ToolApprovalAgent -- an
        // agent-level (not chat-client-level) decorator -- is layered around it. The
        // ToolApprovalAgent then sits inside HarnessGuardedAgent, which remains the
        // outermost returned surface either way.
        AIAgent runtimeAgent = agent;
        if (request.ApprovalPlugin?.ToolApprovalOptions is not null)
        {
            runtimeAgent = new ToolApprovalAgent(agent, request.ApprovalPlugin.ToolApprovalOptions);
        }

        return new HarnessProviderCompositionResult(
            HarnessProviderCompositionStatus.Success,
            new HarnessGuardedAgent(
                runtimeAgent,
                new HarnessGuardedAgentServices(
                    messageInjector,
                    todoAccessor,
                    agentModeAccessor),
                request.ExecutionBinding,
                request.ExecutionContextAccessor,
                request.SessionId,
                request.HistoryProvider is not null ||
                    request.PlanningProviders is not null ||
                    request.ApprovalPlugin is not null ||
                    request.SkillsPlugin is not null,
                request.HistoryProvider?.PersistenceMode ??
                    HarnessHistoryPersistenceMode.NotApplicable,
                providerStateKeysResult.Keys,
                enabledCapabilities,
                request.ApprovalPlugin?.ToolApprovalOptions is not null,
                request.ApprovalPlugin?.HostValidator,
                request.ProgressAccessor,
                compactionRunCoordinator),
            request.Profile,
            null);
    }

    private static IReadOnlySet<HarnessCapability> BuildSupportedCapabilities(
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders,
        HarnessApprovalPlugin? approvalPlugin,
        HarnessSkillsPlugin? skillsPlugin,
        HarnessWebSearchPlugin? webSearchPlugin,
        bool hybridProfileSupplied)
    {
        var capabilities = new HashSet<HarnessCapability>(
            HarnessCompositionGuard.G2SupportedCapabilities);
        if (hybridProfileSupplied)
        {
            // Compaction is included in the supported set only when a HybridProfile was actually
            // supplied for this composition, keeping capability/profile symmetry fail-closed: a
            // profile that enables Compaction without a HybridProfile is still rejected below by the
            // compaction composer itself, never silently tolerated here.
            capabilities.Add(HarnessCapability.Compaction);
        }
        if (historyProvider is not null)
        {
            capabilities.Add(HarnessCapability.PerServiceHistory);
        }
        if (planningProviders?.TodoProvider is not null)
        {
            capabilities.Add(HarnessCapability.Todo);
        }
        if (planningProviders?.AgentModeProvider is not null)
        {
            capabilities.Add(HarnessCapability.AgentMode);
        }
        if (approvalPlugin?.ResponseBindingEnabled == true)
        {
            capabilities.Add(HarnessCapability.ApprovalResponseBinding);
        }
        if (approvalPlugin?.NotRequiredBypassingEnabled == true)
        {
            capabilities.Add(HarnessCapability.ApprovalNotRequiredBypassing);
        }
        if (approvalPlugin?.ToolApprovalOptions is not null)
        {
            capabilities.Add(HarnessCapability.ToolAutoApproval);
        }
        if (skillsPlugin is not null)
        {
            capabilities.Add(HarnessCapability.Skills);
        }
        if (webSearchPlugin is not null)
        {
            capabilities.Add(HarnessCapability.WebSearch);
        }
        return capabilities;
    }

    private static ProviderStateKeysResult BuildProviderStateKeys(
        HarnessHistoryProviderPlugin? historyProvider,
        HarnessPlanningProvidersPlugin? planningProviders,
        HarnessSkillsPlugin? skillsPlugin)
    {
        var keys = new List<string>();
        if (historyProvider is not null)
        {
            keys.AddRange(historyProvider.ProviderStateKeys);
        }
        if (planningProviders is not null)
        {
            keys.AddRange(planningProviders.ProviderStateKeys);
        }
        if (skillsPlugin is not null)
        {
            keys.AddRange(skillsPlugin.ProviderStateKeys);
        }

        var distinctCount = keys.Distinct(StringComparer.Ordinal).Count();
        if (distinctCount != keys.Count)
        {
            return new ProviderStateKeysResult(
                HarnessProviderCompositionStatus.ProviderStateKeyCollision,
                Array.Empty<string>(),
                "The enabled selected providers contribute colliding session state keys.");
        }

        return new ProviderStateKeysResult(
            HarnessProviderCompositionStatus.Success,
            [.. keys.OrderBy(key => key, StringComparer.Ordinal)],
            null);
    }

    private readonly record struct ProviderStateKeysResult(
        HarnessProviderCompositionStatus Status,
        IReadOnlyList<string> Keys,
        string? Detail);

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

    private static HarnessProviderCompositionStatus MapPlanningStatus(
        HarnessPlanningCompositionGuardStatus status) =>
        status switch
        {
            HarnessPlanningCompositionGuardStatus.PlanningPluginUnexpected =>
                HarnessProviderCompositionStatus.PlanningPluginUnexpected,
            HarnessPlanningCompositionGuardStatus.TodoProviderRequired =>
                HarnessProviderCompositionStatus.TodoProviderRequired,
            HarnessPlanningCompositionGuardStatus.TodoProviderUnexpected =>
                HarnessProviderCompositionStatus.TodoProviderUnexpected,
            HarnessPlanningCompositionGuardStatus.AgentModeProviderRequired =>
                HarnessProviderCompositionStatus.AgentModeProviderRequired,
            HarnessPlanningCompositionGuardStatus.AgentModeProviderUnexpected =>
                HarnessProviderCompositionStatus.AgentModeProviderUnexpected,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static HarnessProviderCompositionStatus MapApprovalStatus(
        HarnessApprovalCompositionGuardStatus status) =>
        status switch
        {
            HarnessApprovalCompositionGuardStatus.ApprovalPluginUnexpected =>
                HarnessProviderCompositionStatus.ApprovalPluginUnexpected,
            HarnessApprovalCompositionGuardStatus.ApprovalResponseBindingRequired =>
                HarnessProviderCompositionStatus.ApprovalResponseBindingRequired,
            HarnessApprovalCompositionGuardStatus.ApprovalResponseBindingUnexpected =>
                HarnessProviderCompositionStatus.ApprovalResponseBindingUnexpected,
            HarnessApprovalCompositionGuardStatus.ApprovalNotRequiredBypassingRequired =>
                HarnessProviderCompositionStatus.ApprovalNotRequiredBypassingRequired,
            HarnessApprovalCompositionGuardStatus.ApprovalNotRequiredBypassingUnexpected =>
                HarnessProviderCompositionStatus.ApprovalNotRequiredBypassingUnexpected,
            HarnessApprovalCompositionGuardStatus.ToolAutoApprovalRequired =>
                HarnessProviderCompositionStatus.ToolAutoApprovalRequired,
            HarnessApprovalCompositionGuardStatus.ToolAutoApprovalUnexpected =>
                HarnessProviderCompositionStatus.ToolAutoApprovalUnexpected,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static HarnessProviderCompositionStatus MapSkillsStatus(
        HarnessSkillsCompositionGuardStatus status) =>
        status switch
        {
            HarnessSkillsCompositionGuardStatus.SkillsPluginUnexpected =>
                HarnessProviderCompositionStatus.SkillsPluginUnexpected,
            HarnessSkillsCompositionGuardStatus.SkillsPluginRequired =>
                HarnessProviderCompositionStatus.SkillsPluginRequired,
            HarnessSkillsCompositionGuardStatus.SkillsApprovalCoherenceRequired =>
                HarnessProviderCompositionStatus.SkillsApprovalCoherenceRequired,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static HarnessProviderCompositionStatus MapWebSearchStatus(
        HarnessWebSearchCompositionGuardStatus status) =>
        status switch
        {
            HarnessWebSearchCompositionGuardStatus.WebSearchPluginUnexpected =>
                HarnessProviderCompositionStatus.WebSearchPluginUnexpected,
            HarnessWebSearchCompositionGuardStatus.WebSearchPluginRequired =>
                HarnessProviderCompositionStatus.WebSearchPluginRequired,
            HarnessWebSearchCompositionGuardStatus.WebSearchToolNameCollision =>
                HarnessProviderCompositionStatus.WebSearchToolNameCollision,
            HarnessWebSearchCompositionGuardStatus.WebSearchToolTypeCollision =>
                HarnessProviderCompositionStatus.WebSearchToolTypeCollision,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static HarnessProviderCompositionStatus MapCompactionStatus(
        HarnessCompactionCompositionStatus status) =>
        status switch
        {
            HarnessCompactionCompositionStatus.CapabilityEnabledWithoutProfile =>
                HarnessProviderCompositionStatus.CompactionCapabilityEnabledWithoutProfile,
            HarnessCompactionCompositionStatus.ProfileSuppliedWithoutCapabilityEnabled =>
                HarnessProviderCompositionStatus.CompactionProfileSuppliedWithoutCapabilityEnabled,
            HarnessCompactionCompositionStatus.ProfileNotExecutable =>
                HarnessProviderCompositionStatus.CompactionProfileNotExecutable,
            HarnessCompactionCompositionStatus.ExistingCompactionComponent =>
                HarnessProviderCompositionStatus.CompactionExistingComponent,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    private static HarnessProviderCompositionResult Failure(
        HarnessProviderCompositionStatus status,
        HarnessCapabilityProfile profile,
        string? detail) =>
        new(status, null, profile, detail);
}
