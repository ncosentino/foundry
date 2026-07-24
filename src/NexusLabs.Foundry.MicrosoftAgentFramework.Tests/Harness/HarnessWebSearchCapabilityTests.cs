using System.Reflection;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Covers T039-T040: the G3 <c>WebSearch</c> selected-provider slice composing exactly one
/// public MEAI 10.6 <see cref="HostedWebSearchTool"/> marker into <c>ChatOptions.Tools</c>
/// when the pre-existing, provider-dependent <see cref="HarnessCapability.WebSearch"/>
/// capability is coherently enabled by host-supplied
/// <see cref="HarnessProviderCapability.HostedWebSearch"/> evidence -- its fail-closed
/// capability/plugin symmetry, its name/type collision guard, the complete absence of any
/// runtime provider/model-name auto-detection, and its complete non-participation in
/// session-state keys or the trusted schema-v2 session envelope.
/// </summary>
public sealed class HarnessWebSearchCapabilityTests
{
    // ------------------------------------------------------------------
    // Plugin construction (T039)
    // ------------------------------------------------------------------

    [Fact]
    public void CreateWebSearchPlugin_Default_WrapsHostedWebSearchToolMarker()
    {
        var plugin = HarnessCompositionTestFixture.CreateWebSearchPlugin();

        var tool = Assert.IsType<HostedWebSearchTool>(plugin.Tool);
        Assert.Equal("web_search", tool.Name);
    }

    [Fact]
    public void HarnessWebSearchPlugin_Create_NullTool_FailsClosed() =>
        Assert.Throws<ArgumentNullException>(() =>
            HarnessWebSearchPlugin.Create(null!));

    // ------------------------------------------------------------------
    // Resolver/composition-level defer/enable confirmation (T039)
    //
    // HarnessCapabilityProfileTests already covers Resolve_ProviderDependentWithoutEvidence_
    // DefersIt / Resolve_ProviderDependentWithEvidence_EnablesIt at the resolver level for
    // WebSearch specifically; these two tests re-confirm the identical behavior through this
    // slice's own CreateWebSearchProfile fixture helper, since that helper (not the resolver
    // itself) is what every other test in this file depends on for profile construction.
    // ------------------------------------------------------------------

    [Fact]
    public void Profile_WebSearchRequestedWithoutProviderEvidence_RemainsDeferredNonExecutable()
    {
        var profile = HarnessCompositionTestFixture.CreateWebSearchProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness,
            includeWebSearch: true,
            includeHostedWebSearchEvidence: false);

        Assert.Equal(
            HarnessCapabilityState.Deferred,
            profile.Capabilities[HarnessCapability.WebSearch].EffectiveState);
    }

    [Fact]
    public void Profile_WebSearchRequestedWithProviderEvidence_ResolvesEnabled()
    {
        var profile = HarnessCompositionTestFixture.CreateWebSearchProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness,
            includeWebSearch: true,
            includeHostedWebSearchEvidence: true);

        Assert.Equal(
            HarnessCapabilityState.Enabled,
            profile.Capabilities[HarnessCapability.WebSearch].EffectiveState);
    }

    [Fact]
    public void Profile_WebSearchEvidence_TrustBoundaryAotDiagnosticsRemainAsPreRegistered()
    {
        // Pre-existing HarnessCapabilityResolver registration for WebSearch (unmodified by
        // this slice): ExternalContent trust boundary, Unverified AOT status, Partial
        // diagnostics status. This slice only adds composition wiring; it must never alter
        // these truthful, pre-registered facts.
        var profile = HarnessCompositionTestFixture.CreateWebSearchProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness,
            includeWebSearch: true,
            includeHostedWebSearchEvidence: true);

        var evidence = profile.Capabilities[HarnessCapability.WebSearch];
        Assert.Equal(HarnessCapabilityTrustBoundary.ExternalContent, evidence.TrustBoundary);
        Assert.Equal(HarnessCapabilityAotStatus.Unverified, evidence.AotStatus);
        Assert.Equal(HarnessCapabilityDiagnosticsStatus.Partial, evidence.DiagnosticsStatus);
        Assert.Equal(
            HarnessProviderCapability.HostedWebSearch,
            evidence.RequiredProviderCapability);
    }

    // ------------------------------------------------------------------
    // Fail-closed guard combinations (T039)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_WebSearchEnabledWithoutPlugin_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateWebSearchProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeWebSearch: true,
                includeHostedWebSearchEvidence: true),
            webSearchPlugin: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.WebSearchPluginRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_PluginWhenWebSearchNotRequested_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateWebSearchPlugin());

        Assert.Equal(
            HarnessProviderCompositionStatus.WebSearchPluginUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_PluginWhenWebSearchDeferredWithoutProviderEvidence_FailsClosedWithoutAgent()
    {
        // The capability was explicitly requested but remains Deferred (non-executable)
        // because no host-supplied HostedWebSearch provider evidence was proven -- a
        // supplied plugin must be rejected exactly as if the capability had never been
        // requested at all: host-supplied provider capability evidence remains the sole,
        // authoritative source of truth, and a WebSearch capability without provider
        // evidence must remain non-executable.
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateWebSearchProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeWebSearch: true,
                includeHostedWebSearchEvidence: false),
            HarnessCompositionTestFixture.CreateWebSearchPlugin());

        Assert.Equal(
            HarnessProviderCompositionStatus.WebSearchPluginUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_GeneratedToolNameCollidesWithHostedMarker_FailsClosedBeforeAgentIsBuilt()
    {
        var collidingFunction = AIFunctionFactory.Create(() => "ok", "web_search");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(collidingFunction.Name),
                services,
                HarnessCompositionTestFixture.CreateWebSearchProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeWebSearch: true,
                    includeHostedWebSearchEvidence: true),
                HarnessCompositionTestFixture.CreateToolResolution(collidingFunction),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: HarnessCompositionTestFixture.CreateWebSearchPlugin());

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.WebSearchToolNameCollision,
                result.Status);
            Assert.Null(result.Agent);
        }
    }

    [Fact]
    public void HostedWebSearchTool_and_AIFunction_AreDisjointTypes_TypeCollisionIsUnreachable()
    {
        // WebSearchToolTypeCollision is retained in HarnessWebSearchCompositionGuard as
        // defensive dead code: every generated tool resolved by HarnessGeneratedToolResolution
        // is an AIFunction, and AIFunction/HostedWebSearchTool are disjoint AITool subtypes,
        // so no generated function can ever report HostedWebSearchTool's runtime type through
        // today's public surface. This test proves that structural fact directly rather than
        // asserting an unreachable runtime scenario.
        Assert.False(typeof(AIFunction).IsAssignableFrom(typeof(HostedWebSearchTool)));
        Assert.False(typeof(HostedWebSearchTool).IsAssignableFrom(typeof(AIFunction)));
        Assert.NotEqual(typeof(AIFunction), typeof(HostedWebSearchTool));
    }

    // ------------------------------------------------------------------
    // Coherent composition: exactly one marker, no unselected activation (T039)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_WebSearchEnabled_AppendsExactlyOneHostedWebSearchToolMarker()
    {
        var function = AIFunctionFactory.Create(() => "ok", "WebSearchGeneratedTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessScriptedChatClient(
                function.Name,
                static () => { },
                requestFunctionCall: false);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateWebSearchProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeWebSearch: true,
                    includeHostedWebSearchEvidence: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: HarnessCompositionTestFixture.CreateWebSearchPlugin());

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
            await agent.RunAsync(
                "search the web",
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            var tools = chatClient.LastOptions?.Tools;
            Assert.NotNull(tools);
            var hostedMarker = Assert.Single(tools!.OfType<HostedWebSearchTool>());
            Assert.Equal("web_search", hostedMarker.Name);
            Assert.Contains(
                tools!,
                tool => tool is AIFunction aiFunction && aiFunction.Name == function.Name);
            Assert.Equal(2, tools!.Count);

            // No unselected capability/provider was activated by composing WebSearch alone.
            Assert.Null(agent.GetService<TodoProvider>());
            Assert.Null(agent.GetService<AgentModeProvider>());
            Assert.Null(agent.GetService<IHarnessTodoAccessor>());
            Assert.Null(agent.GetService<IHarnessAgentModeAccessor>());
            Assert.Null(agent.GetService<AgentSkillsProvider>());
        }
    }

    // ------------------------------------------------------------------
    // Raw hosted marker/options are hidden by GetService (T039)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_WebSearchComposed_NeverExposesRawToolOrOptionsThroughGetService()
    {
        var agent = ComposeWebSearchAgent(
            new HarnessScriptedChatClient(
                "unused",
                static () => { },
                requestFunctionCall: false),
            AIFunctionFactory.Create(() => "unused", "WebSearchGeneratedTool"),
            HarnessCompositionTestFixture.CreateServices(),
            out var scope);
        using (scope)
        {
            Assert.Null(agent.GetService<HostedWebSearchTool>());
            Assert.Null(agent.GetService<HarnessWebSearchPlugin>());
            Assert.Null(agent.GetService<ChatOptions>());
            Assert.Null(agent.GetService<ChatClientAgentOptions>());
            Assert.Null(agent.GetService<IDisposable>());
        }
    }

    // ------------------------------------------------------------------
    // Session-state/envelope non-activation: hosted web search is stateless (T039)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_WebSearchOnly_NoProviderStateKeysAndNoSessionEnvelopeActivation()
    {
        var function = AIFunctionFactory.Create(() => "ok", "WebSearchGeneratedTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(
                    function.Name,
                    static () => { },
                    requestFunctionCall: false),
                services,
                HarnessCompositionTestFixture.CreateWebSearchProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeWebSearch: true,
                    includeHostedWebSearchEvidence: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: HarnessCompositionTestFixture.CreateWebSearchPlugin());

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);
            var serialized = await agent.SerializeSessionAsync(
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            // sessionContinuityEnabled is deliberately false for a WebSearch-only
            // composition (no history/planning/approval/skills plugin present), so
            // HarnessGuardedAgent.SerializeSessionCoreAsync bypasses HarnessSessionEnvelope
            // entirely and returns the raw inner MAF session -- proving the plugin never
            // adds a provider state key or activates the trusted schema-v2 envelope by
            // itself.
            var json = serialized.GetRawText();
            Assert.DoesNotContain("providerStateKeys", json, StringComparison.Ordinal);
            Assert.DoesNotContain("enabledCapabilities", json, StringComparison.Ordinal);
            Assert.DoesNotContain("schemaVersion", json, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------
    // Strict no-run-options fail closed (T039)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_WebSearchComposed_OtherRunOptions_AreRejectedBeforeModelCall()
    {
        var function = AIFunctionFactory.Create(() => "ok", "WebSearchGeneratedTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateWebSearchProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeWebSearch: true,
                    includeHostedWebSearchEvidence: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: HarnessCompositionTestFixture.CreateWebSearchPlugin());
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
    // G2/G3 regression: existing prior slices remain unaffected (T039)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_NoWebSearch_PreservesPriorNonWebSearchBehavior()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            webSearchPlugin: null);

        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        Assert.NotNull(result.Agent);
    }

    // ------------------------------------------------------------------
    // Structural: no runtime provider/model-name auto-detection path exists (T039)
    // ------------------------------------------------------------------

    [Fact]
    public void HarnessWebSearchPlugin_and_Guard_NeverReferenceProviderNameOrChatClientMetadata()
    {
        // Enablement is driven solely by host-supplied HarnessProviderCapability evidence,
        // resolved by the pre-existing HarnessCapabilityResolver -- never by inspecting
        // ChatClientMetadata, a provider or model name string, or any callback/delegate
        // hook. This is verified structurally (reflection over the plugin/guard/result
        // surfaces) rather than via a runtime scenario, since there is no runtime scenario
        // to exercise: the code path simply does not exist.
        var types = new[]
        {
            typeof(HarnessWebSearchPlugin),
            typeof(HarnessWebSearchCompositionGuard),
            typeof(HarnessWebSearchCompositionGuardResult),
            typeof(HarnessWebSearchCompositionGuardStatus),
        };

        foreach (var type in types)
        {
            foreach (var (memberName, memberType) in CollectMemberTypeSurface(type))
            {
                Assert.False(
                    memberType == typeof(ChatClientMetadata),
                    $"{type.Name}.{memberName} must never reference ChatClientMetadata: " +
                    "hosted web search enablement must be decided solely by host-supplied " +
                    "HarnessProviderCapability evidence, never runtime provider " +
                    "introspection.");
                Assert.False(
                    typeof(Delegate).IsAssignableFrom(memberType),
                    $"{type.Name}.{memberName} must never expose a callback/delegate " +
                    "surface for runtime provider/model detection.");
                Assert.DoesNotContain(
                    "providername",
                    memberName.Replace("_", string.Empty, StringComparison.Ordinal),
                    StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(
                    "modelname",
                    memberName.Replace("_", string.Empty, StringComparison.Ordinal),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IEnumerable<(string Name, Type Type)> CollectMemberTypeSurface(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var ctor in type.GetConstructors(flags))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                yield return (parameter.Name ?? ctor.Name, parameter.ParameterType);
            }
        }

        foreach (var method in type.GetMethods(flags))
        {
            foreach (var parameter in method.GetParameters())
            {
                yield return ($"{method.Name}({parameter.Name})", parameter.ParameterType);
            }
            yield return (method.Name, method.ReturnType);
        }

        foreach (var property in type.GetProperties(flags))
        {
            yield return (property.Name, property.PropertyType);
        }

        foreach (var field in type.GetFields(flags))
        {
            yield return (field.Name, field.FieldType);
        }
    }

    private static HarnessProviderCompositionResult ComposeForFailure(
        HarnessCapabilityProfile profile,
        HarnessWebSearchPlugin? webSearchPlugin)
    {
        var function = AIFunctionFactory.Create(() => "ok", "WebSearchGeneratedTool");
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
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: webSearchPlugin);
            return new HarnessProviderComposition().Compose(request);
        }
    }

    private static AIAgent ComposeWebSearchAgent(
        IChatClient chatClient,
        AIFunction generatedTool,
        IServiceProvider services,
        out IDisposable scope)
    {
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out scope);
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreateWebSearchProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeWebSearch: true,
                includeHostedWebSearchEvidence: true),
            HarnessCompositionTestFixture.CreateToolResolution(generatedTool),
            binding,
            accessor,
            metrics: null,
            historyProvider: null,
            planningProviders: null,
            approvalPlugin: null,
            skillsPlugin: null,
            progressAccessor: null,
            webSearchPlugin: HarnessCompositionTestFixture.CreateWebSearchPlugin());
        var result = new HarnessProviderComposition().Compose(request);
        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        return Assert.IsAssignableFrom<AIAgent>(result.Agent);
    }
}
