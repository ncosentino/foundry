using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Covers T037-T038: the G3 Skills selected-provider slice composing upstream MAF 1.15
/// <see cref="AgentSkillsProvider"/> from host-authored <see cref="AgentInlineSkill"/>
/// instances only, its two-variant explicit trust policy governing the three
/// <c>load_skill</c>/<c>read_skill_resource</c>/<c>run_skill_script</c> approval gates, its
/// fail-closed capability/plugin/approval coherence guard, its <c>AIContextProviders</c>/
/// provider-state-key union, and its complete absence from
/// <see cref="HarnessGuardedAgent.GetService"/>.
/// </summary>
public sealed class HarnessSkillsTests
{
    // ------------------------------------------------------------------
    // Plugin construction (T037)
    // ------------------------------------------------------------------

    [Fact]
    public void CreateSkillsPlugin_NoSkillsSupplied_FailsClosed() =>
        Assert.Throws<InvalidOperationException>(() =>
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                skills: [],
                HarnessSkillsTrustPolicy.HostTrusted));

    [Fact]
    public void CreateSkillsPlugin_NullSkillEntry_FailsClosed() =>
        Assert.Throws<InvalidOperationException>(() =>
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                skills: [null!],
                HarnessSkillsTrustPolicy.HostTrusted));

    [Fact]
    public void CreateSkillsPlugin_UnknownTrustPolicy_FailsClosed() =>
        Assert.Throws<InvalidOperationException>(() =>
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                (HarnessSkillsTrustPolicy)999));

    [Fact]
    public void CreateSkillsPlugin_HostTrusted_ExposesProviderAndCanonicalStateKey()
    {
        var plugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
            [CreateSkill()],
            HarnessSkillsTrustPolicy.HostTrusted);

        Assert.Equal(HarnessSkillsTrustPolicy.HostTrusted, plugin.TrustPolicy);
        Assert.NotNull(plugin.SkillsProvider);
        Assert.Equal(["AgentSkillsProvider"], plugin.ProviderStateKeys);
    }

    [Fact]
    public void CreateSkillsPlugin_ApprovalRequired_ExposesProviderAndCanonicalStateKey()
    {
        var plugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
            [CreateSkill()],
            HarnessSkillsTrustPolicy.ApprovalRequired);

        Assert.Equal(HarnessSkillsTrustPolicy.ApprovalRequired, plugin.TrustPolicy);
        Assert.NotNull(plugin.SkillsProvider);
        Assert.Equal(["AgentSkillsProvider"], plugin.ProviderStateKeys);
    }

    // ------------------------------------------------------------------
    // File-backed rejection at the type level (T037)
    // ------------------------------------------------------------------

    [Fact]
    public void HarnessSkillsPlugin_PublicSurface_NeverAcceptsFileBackedSkillTypes()
    {
        // G3 supports only host-authored in-memory/inline skills: AgentInlineSkill is the
        // sole skill type this plugin's construction surface accepts. Raw file system
        // paths, AgentFileSkill, AgentFileSkillsSource, AgentFileSkillScriptRunner, and any
        // other AgentSkillsSource must never appear anywhere in this type's member
        // signatures -- verified structurally here since a "does not compile" assertion
        // cannot otherwise be expressed in xUnit.
        var forbiddenTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(IEnumerable<string>),
            typeof(AgentFileSkill),
            typeof(AgentFileSkillsSource),
            typeof(AgentFileSkillsSourceOptions),
            typeof(AgentFileSkillScriptRunner),
            typeof(AgentSkillsSource),
        };

        var type = typeof(HarnessSkillsPlugin);
        var members = type
            .GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Cast<MethodBase>()
            .Concat(type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.DeclaredOnly));

        foreach (var member in members)
        {
            foreach (var parameter in member.GetParameters())
            {
                Assert.False(
                    forbiddenTypes.Contains(parameter.ParameterType),
                    $"{member.Name} must not accept a file-backed skill parameter of type " +
                    $"'{parameter.ParameterType}'.");
            }
        }
    }

    // ------------------------------------------------------------------
    // Fail-closed guard combinations (T037)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_SkillsEnabledWithoutPlugin_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: false),
            skillsPlugin: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.SkillsPluginRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_PluginWhenSkillsCapabilityNotEnabled_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.HostTrusted));

        Assert.Equal(
            HarnessProviderCompositionStatus.SkillsPluginUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_ApprovalRequiredWithoutResponseBindingCapability_FailsClosed()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: false),
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.ApprovalRequired));

        Assert.Equal(
            HarnessProviderCompositionStatus.SkillsApprovalCoherenceRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    /// <summary>
    /// When the profile already selects <c>ApprovalResponseBinding</c> but no approval plugin
    /// is supplied, the pre-existing <see cref="HarnessApprovalCompositionGuard"/> rejects the
    /// request before <see cref="HarnessSkillsCompositionGuard"/> ever runs. This is intentional:
    /// the skills guard must not duplicate the approval layer's own capability/plugin coherence
    /// check -- it only adds the additional, skills-specific requirement that the response
    /// binding capability (and a coherent plugin) is actually selected when the skills plugin's
    /// trust policy is <see cref="HarnessSkillsTrustPolicy.ApprovalRequired"/>.
    /// </summary>
    [Fact]
    public void Compose_ApprovalRequiredWithResponseBindingCapabilityButNoApprovalPlugin_FailsClosedViaApprovalGuard()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: true),
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.ApprovalRequired),
            approvalPlugin: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.ApprovalResponseBindingRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    /// <summary>
    /// Same rationale as
    /// <see cref="Compose_ApprovalRequiredWithResponseBindingCapabilityButNoApprovalPlugin_FailsClosedViaApprovalGuard"/>:
    /// an approval plugin that does not itself enable <c>ResponseBindingEnabled</c> while the
    /// capability is selected fails via the shared approval guard, not a duplicated skills-local
    /// check.
    /// </summary>
    [Fact]
    public void Compose_ApprovalRequiredWithIncoherentApprovalPlugin_FailsClosedViaApprovalGuard()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: true),
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.ApprovalRequired),
            approvalPlugin: HarnessCompositionTestFixture.CreateApprovalPlugin(
                responseBindingEnabled: false,
                notRequiredBypassingEnabled: true,
                toolApprovalOptions: null,
                hostValidator: null));

        Assert.Equal(
            HarnessProviderCompositionStatus.ApprovalResponseBindingRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_ApprovalRequiredWithCoherentApprovalPlugin_Succeeds()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: true),
            HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.ApprovalRequired),
            approvalPlugin: HarnessCompositionTestFixture.CreateApprovalPlugin(
                responseBindingEnabled: true,
                notRequiredBypassingEnabled: false,
                toolApprovalOptions: null,
                hostValidator: null));

        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        Assert.NotNull(result.Agent);
    }

    [Fact]
    public void Compose_CollidingCustomProviderStateKey_FailsClosedBeforeAgentIsBuilt()
    {
        var function = AIFunctionFactory.Create(() => "ok", "SkillsTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var historyPlugin = HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                new HarnessCollidingSkillsChatHistoryProvider());
            var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.HostTrusted);

            var resolver = new HarnessCapabilityResolver();
            var profile = resolver.Resolve(
                new HarnessCapabilityResolutionRequest(
                    ProfileId: "skills-collision-test",
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
                        HarnessCapability.Skills,
                    },
                    ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                    ToolLoopOwner: HarnessToolLoopOwner.Harness,
                    TelemetryOwner: HarnessTelemetryOwner.Harness,
                    HistoryPersistenceMode: HarnessHistoryPersistenceMode.DurableProvider));

            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(function.Name),
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: historyPlugin,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: skillsPlugin);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.ProviderStateKeyCollision,
                result.Status);
            Assert.Null(result.Agent);
        }
    }

    // ------------------------------------------------------------------
    // Host-trusted composition: no unselected capability activates (T037)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_HostTrustedSkillsOnly_ActivatesNoUnselectedCapability()
    {
        var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
            [CreateSkill()],
            HarnessSkillsTrustPolicy.HostTrusted);
        var agent = ComposeSkillsAgent(
            new HarnessScriptedChatClient(
                "unused",
                static () => { },
                requestFunctionCall: false),
            AIFunctionFactory.Create(() => "unused", "SkillsTool"),
            HarnessCompositionTestFixture.CreateServices(),
            out var scope,
            skillsPlugin,
            includeApprovalResponseBinding: false,
            approvalPlugin: null);
        using (scope)
        {
            Assert.Null(agent.GetService<TodoProvider>());
            Assert.Null(agent.GetService<AgentModeProvider>());
            Assert.Null(agent.GetService<IHarnessTodoAccessor>());
            Assert.Null(agent.GetService<IHarnessAgentModeAccessor>());
        }
    }

    // ------------------------------------------------------------------
    // Upstream inline skill load/resource/script conformance, host trusted (T037)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_HostTrusted_LoadResourceAndScript_ExecuteDirectlyViaHostDelegates()
    {
        var function = AIFunctionFactory.Create(() => "unused", "SkillsTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.HostTrusted);
            var chatClient = new HarnessQueuedFunctionCallChatClient(
                ("load_skill", new Dictionary<string, object?>
                {
                    ["skillName"] = "demo-skill",
                }),
                ("read_skill_resource", new Dictionary<string, object?>
                {
                    ["skillName"] = "demo-skill",
                    ["resourceName"] = "demo-resource",
                }),
                ("run_skill_script", new Dictionary<string, object?>
                {
                    ["skillName"] = "demo-skill",
                    ["scriptName"] = "demo-script",
                }));
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateSkillsProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeSkills: true,
                    includeApprovalResponseBinding: false),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: skillsPlugin);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
            var response = await agent.RunAsync(
                "please use the demo skill",
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            // Host trusted disables all three MAF approval gates: no ToolApprovalRequestContent
            // should ever surface, and all three tool results execute in this single run.
            Assert.Empty(
                response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<ToolApprovalRequestContent>());
            var results = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionResultContent>()
                .ToList();
            Assert.Equal(3, results.Count);
        }
    }

    // ------------------------------------------------------------------
    // Trust policy controls the three MAF approval-disable flags (T037)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_ApprovalRequired_LoadSkillSurfacesApprovalRequestAndExecutesOnceApproved()
    {
        var function = AIFunctionFactory.Create(() => "unused", "SkillsTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.ApprovalRequired);
            var approvalPlugin = HarnessCompositionTestFixture.CreateApprovalPlugin(
                responseBindingEnabled: true,
                notRequiredBypassingEnabled: false,
                toolApprovalOptions: null,
                hostValidator: null);
            var chatClient = new HarnessQueuedFunctionCallChatClient(
                ("load_skill", new Dictionary<string, object?>
                {
                    ["skillName"] = "demo-skill",
                }));
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateSkillsProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeSkills: true,
                    includeApprovalResponseBinding: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: approvalPlugin,
                skillsPlugin: skillsPlugin);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
            var requested = await agent.RunAsync(
                "please use the demo skill",
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            var approvalRequest = Assert.Single(
                requested.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>());
            Assert.Empty(
                requested.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>());

            var approved = await agent.RunAsync(
                new ChatMessage(ChatRole.User, [approvalRequest.CreateResponse(true, null)]),
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(
                approved.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        }
    }

    [Fact]
    public async Task Compose_ApprovalRequired_RejectedResponse_InvokesLoadSkillZeroTimes()
    {
        var function = AIFunctionFactory.Create(() => "unused", "SkillsTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.ApprovalRequired);
            var approvalPlugin = HarnessCompositionTestFixture.CreateApprovalPlugin(
                responseBindingEnabled: true,
                notRequiredBypassingEnabled: false,
                toolApprovalOptions: null,
                hostValidator: null);
            var chatClient = new HarnessQueuedFunctionCallChatClient(
                ("load_skill", new Dictionary<string, object?>
                {
                    ["skillName"] = "demo-skill",
                }));
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateSkillsProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeSkills: true,
                    includeApprovalResponseBinding: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: approvalPlugin,
                skillsPlugin: skillsPlugin);

            var result = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
            var requested = await agent.RunAsync(
                "please use the demo skill",
                session,
                cancellationToken: TestContext.Current.CancellationToken);
            var approvalRequest = Assert.Single(
                requested.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>());

            var rejected = await agent.RunAsync(
                new ChatMessage(ChatRole.User, [approvalRequest.CreateResponse(false, "not now")]),
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            var rejectedResult = Assert.Single(
                rejected.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
            Assert.Contains(
                "rejected",
                rejectedResult.Result?.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------
    // Raw skill/provider/options services are hidden by GetService (T037)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_HostTrustedSkills_NeverExposesRawProviderThroughGetService()
    {
        var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
            [CreateSkill()],
            HarnessSkillsTrustPolicy.HostTrusted);
        var agent = ComposeSkillsAgent(
            new HarnessScriptedChatClient(
                "unused",
                static () => { },
                requestFunctionCall: false),
            AIFunctionFactory.Create(() => "unused", "SkillsTool"),
            HarnessCompositionTestFixture.CreateServices(),
            out var scope,
            skillsPlugin,
            includeApprovalResponseBinding: false,
            approvalPlugin: null);
        using (scope)
        {
            Assert.Null(agent.GetService<AgentSkillsProvider>());
            Assert.Null(agent.GetService<AIContextProvider>());
            Assert.Null(agent.GetService<AgentSkillsProviderOptions>());
            Assert.Null(agent.GetService<ChatClientAgentOptions>());
            Assert.Null(agent.GetService<ChatOptions>());
            Assert.Null(agent.GetService<ChatHistoryProvider>());
            Assert.Null(agent.GetService<IDisposable>());
        }
    }

    // ------------------------------------------------------------------
    // Session serialize/deserialize: honest envelope/state-key behavior (T037)
    // ------------------------------------------------------------------

    [Fact]
    public async Task
        DeserializeSession_FreshAgentUnderCurrentWorkspaceRestoresEnvelopeWithSkillsStateKey()
    {
        // AgentSkillsProvider.StateKeys is a constant, non-session-specific key
        // ("AgentSkillsProvider"): MAF exposes no session-scoped skill state to persist or
        // restore. This test therefore proves the honest, provable claim -- the envelope's
        // provider-state-key/enabled-capability union round-trips coherently under a fresh
        // agent/provider instance bound to the current workspace -- rather than fabricating
        // a "restored loaded skill" conversation state that AgentSkillsProvider does not
        // actually carry.
        var function = AIFunctionFactory.Create(() => "unused", "SkillsTool");
        JsonElement serialized;
        var originalWorkspace = new InMemoryWorkspace();

        using (var originalServices = HarnessCompositionTestFixture.CreateServices())
        {
            var originalAccessor = new AgentExecutionContextAccessor();
            using var originalScope = originalAccessor.BeginScope(
                new AgentExecutionContext(
                    "user-1",
                    "orchestration-1",
                    Workspace: originalWorkspace));
            var originalCapture = HarnessExecutionBinding.Capture(
                originalAccessor,
                HarnessCompositionTestFixture.SessionId,
                requireWorkspace: true);
            var originalBinding = Assert.IsType<HarnessExecutionBinding>(
                originalCapture.Binding);
            var originalSkillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.HostTrusted);
            var originalChatClient = new HarnessQueuedFunctionCallChatClient(
                ("load_skill", new Dictionary<string, object?>
                {
                    ["skillName"] = "demo-skill",
                }));
            var originalRequest = HarnessCompositionTestFixture.CreateRequest(
                originalChatClient,
                originalServices,
                HarnessCompositionTestFixture.CreateSkillsProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeSkills: true,
                    includeApprovalResponseBinding: false),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                originalBinding,
                originalAccessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: originalSkillsPlugin);
            var originalAgent = Assert.IsAssignableFrom<AIAgent>(
                new HarnessProviderComposition().Compose(originalRequest).Agent);

            var originalSession = await originalAgent.CreateSessionAsync(
                TestContext.Current.CancellationToken);
            await originalAgent.RunAsync(
                "please use the demo skill",
                originalSession,
                cancellationToken: TestContext.Current.CancellationToken);
            serialized = await originalAgent.SerializeSessionAsync(
                originalSession,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var currentWorkspace = new InMemoryWorkspace();
        using var currentServices = HarnessCompositionTestFixture.CreateServices();
        var currentAccessor = new AgentExecutionContextAccessor();
        using var currentScope = currentAccessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: currentWorkspace));
        var currentCapture = HarnessExecutionBinding.Capture(
            currentAccessor,
            HarnessCompositionTestFixture.SessionId,
            requireWorkspace: true);
        var currentBinding = Assert.IsType<HarnessExecutionBinding>(currentCapture.Binding);
        var currentSkillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
            [CreateSkill()],
            HarnessSkillsTrustPolicy.HostTrusted);
        var currentRequest = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            currentServices,
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: false),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            currentBinding,
            currentAccessor,
            metrics: null,
            historyProvider: null,
            planningProviders: null,
            approvalPlugin: null,
            skillsPlugin: currentSkillsPlugin);
        var currentAgent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(currentRequest).Agent);

        var restoredSession = await currentAgent.DeserializeSessionAsync(
            serialized,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(restoredSession);
        Assert.Same(currentWorkspace, currentBinding.Workspace);
        Assert.NotSame(originalWorkspace, currentWorkspace);
    }

    [Fact]
    public async Task DeserializeSession_ProviderStateKeyProfileMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerializeSkills();

        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node["providerStateKeys"] = new JsonArray("a-different-provider-key");
        var tampered = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("provider state keys", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_EnabledCapabilityProfileMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerializeSkills();

        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node["enabledCapabilities"] = new JsonArray();
        var tampered = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("enabled capabilities", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_IdentityMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerializeSkills();

        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node["userId"] = "a-different-user";
        var tampered = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("identity", exception.Message);
    }

    // ------------------------------------------------------------------
    // Strict no-run-options fail closed (T037)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_SkillsComposed_OtherRunOptions_AreRejectedBeforeModelCall()
    {
        var function = AIFunctionFactory.Create(() => "ok", "SkillsTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
                [CreateSkill()],
                HarnessSkillsTrustPolicy.HostTrusted);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateSkillsProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeSkills: true,
                    includeApprovalResponseBinding: false),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: skillsPlugin);
            var agent = Assert.IsAssignableFrom<AIAgent>(
                new HarnessProviderComposition().Compose(request).Agent);

            var runOptions = new ChatClientAgentRunOptions(
                new ChatOptions
                {
                    MaxOutputTokens = 32,
                });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await agent.RunAsync(
                    "run",
                    options: runOptions,
                    cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(0, chatClient.CallCount);
        }
    }

    // ------------------------------------------------------------------
    // G2/G3 regression: existing prior slices remain unaffected (T037)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_NoSkills_PreservesPriorNonSkillsBehavior()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            skillsPlugin: null);

        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        Assert.NotNull(result.Agent);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AgentInlineSkill CreateSkill(string name = "demo-skill") =>
        new AgentInlineSkill(
            name,
            "A host-authored demo skill used for Harness Skills conformance tests.",
            "Load this skill, then read the referenced resource and run the referenced " +
            "script.")
            .AddResource(
                "demo-resource",
                new Func<string>(() => "resource-content"),
                "A host-authored inline resource delegate.",
                serializerOptions: null)
            .AddScript(
                "demo-script",
                new Func<string>(() => "script-result"),
                "A host-authored inline script delegate.",
                serializerOptions: null);

    private static HarnessProviderCompositionResult ComposeForFailure(
        HarnessCapabilityProfile profile,
        HarnessSkillsPlugin? skillsPlugin,
        HarnessApprovalPlugin? approvalPlugin = null)
    {
        var function = AIFunctionFactory.Create(() => "ok", "SkillsTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(function.Name),
                services,
                profile,
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin,
                skillsPlugin);
            return new HarnessProviderComposition().Compose(request);
        }
    }

    private static AIAgent ComposeSkillsAgent(
        IChatClient chatClient,
        AIFunction generatedTool,
        IServiceProvider services,
        out IDisposable scope,
        HarnessSkillsPlugin skillsPlugin,
        bool includeApprovalResponseBinding,
        HarnessApprovalPlugin? approvalPlugin)
    {
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out scope);
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding),
            HarnessCompositionTestFixture.CreateToolResolution(generatedTool),
            binding,
            accessor,
            metrics: null,
            historyProvider: null,
            planningProviders: null,
            approvalPlugin,
            skillsPlugin);
        var result = new HarnessProviderComposition().Compose(request);
        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        return Assert.IsAssignableFrom<AIAgent>(result.Agent);
    }

    private static (AIAgent Agent, JsonElement Serialized) ComposeAndSerializeSkills()
    {
        // Deliberately synchronous (using GetAwaiter().GetResult()) rather than async: see
        // HarnessHistoryProviderTests.ComposeAndSerialize for the AsyncLocal-scope rationale.
        var function = AIFunctionFactory.Create(() => "unused", "SkillsTool");
        var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out _);
        var skillsPlugin = HarnessCompositionTestFixture.CreateSkillsPlugin(
            [CreateSkill()],
            HarnessSkillsTrustPolicy.HostTrusted);
        var chatClient = new HarnessQueuedFunctionCallChatClient(
            ("load_skill", new Dictionary<string, object?>
            {
                ["skillName"] = "demo-skill",
            }));
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreateSkillsProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeSkills: true,
                includeApprovalResponseBinding: false),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: null,
            planningProviders: null,
            approvalPlugin: null,
            skillsPlugin: skillsPlugin);
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);

        var session = agent.CreateSessionAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        agent.RunAsync(
            "please use the demo skill",
            session,
            cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var serialized = agent.SerializeSessionAsync(
            session,
            cancellationToken: CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return (agent, serialized);
    }
}
