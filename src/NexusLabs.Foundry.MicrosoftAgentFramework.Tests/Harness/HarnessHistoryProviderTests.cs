using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessHistoryProviderTests
{
    // ------------------------------------------------------------------
    // Capability resolution coherence (T031)
    // ------------------------------------------------------------------

    [Fact]
    public void Resolve_HistorySelectedWithoutPersistenceMode_DefersUntilModeIsExplicit()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "no-mode",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.PerServiceHistory,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: HarnessToolLoopOwner.Harness,
                TelemetryOwner: HarnessTelemetryOwner.Harness,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.NotApplicable));

        var evidence = profile.Capabilities[HarnessCapability.PerServiceHistory];
        Assert.Equal(HarnessCapabilityState.Deferred, evidence.EffectiveState);
        Assert.Contains("explicit", evidence.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.False(profile.IsExecutable);
    }

    [Fact]
    public void Resolve_PersistenceModeWithoutHistorySelected_IsNotExecutable()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "mode-without-capability",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.FunctionInvocation,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: HarnessToolLoopOwner.Harness,
                TelemetryOwner: HarnessTelemetryOwner.Harness,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.InMemory));

        Assert.False(profile.IsExecutable);
    }

    [Fact]
    public void Resolve_ServiceManagedMode_DefersWithoutProviderSpecificEvidence()
    {
        var resolver = new HarnessCapabilityResolver();

        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "service-managed",
                Lane: HarnessConstructionLane.SelectedProviders,
                Acceptance: HarnessCapabilityAcceptance.StableOnly,
                EvidenceThroughPhase: HarnessDeliveryPhase.G3,
                RequestedCapabilities: new HashSet<HarnessCapability>
                {
                    HarnessCapability.FunctionInvocation,
                    HarnessCapability.PerServiceHistory,
                },
                ProviderCapabilities: new HashSet<HarnessProviderCapability>(),
                ToolLoopOwner: HarnessToolLoopOwner.Harness,
                TelemetryOwner: HarnessTelemetryOwner.Harness,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.ServiceManaged));

        var evidence = profile.Capabilities[HarnessCapability.PerServiceHistory];
        Assert.Equal(HarnessCapabilityState.Deferred, evidence.EffectiveState);
        Assert.Contains("MAF 1.15 supports", evidence.Rationale);
        Assert.Contains("provider-specific", evidence.Rationale);
        Assert.False(profile.IsExecutable);
    }

    [Fact]
    public void Resolve_HistorySelectedWithInMemoryMode_EnablesItWithNonDurableRationale() =>
        AssertHistoryModeCoherent(
            HarnessHistoryPersistenceMode.InMemory,
            "non-durable in-memory");

    [Fact]
    public void Resolve_HistorySelectedWithSerializedMode_ReportsCallerOwnedDurability() =>
        AssertHistoryModeCoherent(
            HarnessHistoryPersistenceMode.Serialized,
            "durability depends on caller-owned persistence");

    [Fact]
    public void Resolve_HistorySelectedWithDurableProviderMode_EnablesItWithDurableRationale() =>
        AssertHistoryModeCoherent(
            HarnessHistoryPersistenceMode.DurableProvider,
            "caller-supplied durable provider");

    private static void AssertHistoryModeCoherent(
        HarnessHistoryPersistenceMode mode,
        string expectedRationale)
    {
        var profile = HarnessCompositionTestFixture.CreateHistoryProfile(
            HarnessToolLoopOwner.Harness,
            HarnessTelemetryOwner.Harness,
            mode);

        var evidence = profile.Capabilities[HarnessCapability.PerServiceHistory];
        Assert.Equal(HarnessCapabilityState.Enabled, evidence.EffectiveState);
        Assert.Equal(mode, profile.HistoryPersistenceMode);
        Assert.Contains(expectedRationale, evidence.Rationale);
        Assert.True(profile.IsExecutable);
    }

    // ------------------------------------------------------------------
    // Fail-closed composition tests
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_HistoryEnabledWithoutPlugin_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.InMemory),
            historyProvider: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.HistoryProviderRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_PluginWhenHistoryDisabled_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.InMemory,
                callerSuppliedHistoryProvider: null));

        Assert.Equal(
            HarnessProviderCompositionStatus.HistoryProviderUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_ProfilePersistenceModeMismatch_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.InMemory),
            HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.Serialized,
                callerSuppliedHistoryProvider: null));

        Assert.Equal(
            HarnessProviderCompositionStatus.HistoryProviderModeMismatch,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_UnsupportedPersistenceMode_FailsClosedWithoutAgent()
    {
        var resolver = new HarnessCapabilityResolver();
        var profile = resolver.Resolve(
            new HarnessCapabilityResolutionRequest(
                ProfileId: "service-managed",
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
                ToolLoopOwner: HarnessToolLoopOwner.Harness,
                TelemetryOwner: HarnessTelemetryOwner.Harness,
                HistoryPersistenceMode: HarnessHistoryPersistenceMode.ServiceManaged));

        var result = ComposeForFailure(profile, historyProvider: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.HistoryProviderUnsupportedPersistenceMode,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_DurableProviderModeWithoutPlugin_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider),
            historyProvider: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.HistoryProviderRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void CreateHistoryProviderPlugin_DurableProviderWithoutCallerSuppliedProvider_FailsClosed() =>
        Assert.Throws<InvalidOperationException>(() =>
            HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                callerSuppliedHistoryProvider: null));

    [Fact]
    public void Compose_ExpiredExecutionScope_FailsClosed()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        scope.Dispose();
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.InMemory),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.InMemory,
                callerSuppliedHistoryProvider: null));

        var result = new HarnessProviderComposition().Compose(request);

        Assert.Equal(
            HarnessProviderCompositionStatus.ExecutionBindingInvalid,
            result.Status);
        Assert.Null(result.Agent);
    }

    // ------------------------------------------------------------------
    // Successful composition + runtime order + persistence behavior (T030)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_InMemoryPersistence_RunsToolAndReportsHarnessOwnedMiddleware()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "tool-result";
            },
            "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateHistoryProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    HarnessHistoryPersistenceMode.InMemory),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.InMemory,
                    callerSuppliedHistoryProvider: null));

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
            Assert.Null(agent.GetService<FunctionInvokingChatClient>());
            Assert.Null(agent.GetService<ChatClientAgent>());
            Assert.Null(agent.GetService<IChatClient>());
            Assert.Null(agent.GetService<IDisposable>());
            Assert.NotNull(agent.GetService<IHarnessMessageInjector>());

            var response = await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(1, invocationCount);
        }
    }

    [Fact]
    public async Task
        DeserializeSession_FreshAgentUsesCurrentHostAuthorizedWorkspace()
    {
        var function = AIFunctionFactory.Create(() => "serialized-result", "G3Tool");
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
            var originalPlugin =
                HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.Serialized,
                    callerSuppliedHistoryProvider: null);
            var originalRequest = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(function.Name),
                originalServices,
                HarnessCompositionTestFixture.CreateHistoryProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    HarnessHistoryPersistenceMode.Serialized),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                originalBinding,
                originalAccessor,
                metrics: null,
                historyProvider: originalPlugin);
            var originalAgent = Assert.IsAssignableFrom<AIAgent>(
                new HarnessProviderComposition().Compose(originalRequest).Agent);
            var originalSession = await originalAgent.CreateSessionAsync(
                TestContext.Current.CancellationToken);
            await originalAgent.RunAsync(
                "run",
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
        var currentBinding = Assert.IsType<HarnessExecutionBinding>(
            currentCapture.Binding);
        var currentPlugin = HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
            HarnessHistoryPersistenceMode.Serialized,
            callerSuppliedHistoryProvider: null);
        var currentRequest = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            currentServices,
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.Serialized),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            currentBinding,
            currentAccessor,
            metrics: null,
            historyProvider: currentPlugin);
        var currentAgent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(currentRequest).Agent);

        var restoredSession = await currentAgent.DeserializeSessionAsync(
            serialized,
            cancellationToken: TestContext.Current.CancellationToken);
        var currentProvider = Assert.IsType<InMemoryChatHistoryProvider>(
            currentPlugin.ChatHistoryProvider);
        var restoredContents = currentProvider
            .GetMessages(restoredSession)
            .SelectMany(message => message.Contents)
            .ToList();

        Assert.Contains(restoredContents, content => content is FunctionCallContent);
        Assert.Contains(restoredContents, content => content is FunctionResultContent);
        Assert.Same(currentWorkspace, currentBinding.Workspace);
        Assert.NotSame(originalWorkspace, currentWorkspace);
    }

    [Fact]
    public async Task Compose_DurableProviderMode_PersistsFunctionCallAndResultInSuppliedProvider()
    {
        var function = AIFunctionFactory.Create(() => "durable-result", "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var historyProvider = new InMemoryChatHistoryProvider(
                new InMemoryChatHistoryProviderOptions());
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateHistoryProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    HarnessHistoryPersistenceMode.DurableProvider),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.DurableProvider,
                    historyProvider));

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);

            var session = await agent.CreateSessionAsync(
                TestContext.Current.CancellationToken);
            var response = await agent.RunAsync(
                "run",
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("durable-result", response.GetText());
            var persisted = historyProvider.GetMessages(session);
            Assert.Contains(
                persisted.SelectMany(message => message.Contents),
                content => content is FunctionCallContent);
            Assert.Contains(
                persisted.SelectMany(message => message.Contents),
                content => content is FunctionResultContent);
        }
    }

    [Fact]
    public async Task
        SerializeAndDeserializeSession_RoundTripsInnerSessionWithoutSelectingAWorkspace()
    {
        var function = AIFunctionFactory.Create(() => "durable-result", "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var historyProvider = new InMemoryChatHistoryProvider(
                new InMemoryChatHistoryProviderOptions());
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateHistoryProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    HarnessHistoryPersistenceMode.DurableProvider),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                    HarnessHistoryPersistenceMode.DurableProvider,
                    historyProvider));
            var agent = Assert.IsAssignableFrom<AIAgent>(
                new HarnessProviderComposition().Compose(request).Agent);

            var session = await agent.CreateSessionAsync(
                TestContext.Current.CancellationToken);
            await agent.RunAsync(
                "run",
                session,
                cancellationToken: TestContext.Current.CancellationToken);
            var expectedMessages = historyProvider.GetMessages(session);

            var serialized = await agent.SerializeSessionAsync(
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            AssertEnvelopeNeverCarriesAWorkspace(serialized);

            var restoredSession = await agent.DeserializeSessionAsync(
                serialized,
                cancellationToken: TestContext.Current.CancellationToken);
            var restoredMessages = historyProvider.GetMessages(restoredSession);

            Assert.Equal(expectedMessages.Count, restoredMessages.Count);
            for (var index = 0; index < expectedMessages.Count; index++)
            {
                Assert.Equal(expectedMessages[index].Text, restoredMessages[index].Text);
                Assert.Equal(expectedMessages[index].Role, restoredMessages[index].Role);
            }
        }
    }

    [Fact]
    public async Task DeserializeSession_MismatchedSessionId_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();

        var tampered = TamperEnvelope(serialized, "sessionId", "a-different-session");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("session identifier", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_MismatchedUserIdentity_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();

        var tampered = TamperEnvelope(serialized, "userId", "a-different-user");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("identity", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_SchemaVersionMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();

        var tampered = TamperEnvelope(serialized, "schemaVersion", 999);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("schema version", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_PersistenceModeMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();

        // 1 == HarnessHistoryPersistenceMode.InMemory; the envelope was produced with
        // DurableProvider.
        var tampered = TamperEnvelope(serialized, "persistenceMode", 1);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("persistence mode", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_ProviderStateKeyMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();

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
    public async Task DeserializeSession_MissingProviderStateKeys_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();
        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node.Remove("providerStateKeys");
        var tampered = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("provider state keys", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_MissingInnerSession_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerialize();
        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node.Remove("innerSession");
        var tampered = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("inner MAF session state", exception.Message);
    }

    [Fact]
    public async Task DeserializeSession_MalformedEnvelope_FailsClosed()
    {
        var (agent, _) = ComposeAndSerialize();
        var malformed = JsonDocument.Parse("{}").RootElement;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                malformed,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("schema version", exception.Message);
    }

    [Fact]
    public async Task
        DeserializeSession_ActiveWorkspaceChangedSinceSerialize_FailsClosedWithoutReadingEnvelope()
    {
        var function = AIFunctionFactory.Create(() => "unused", "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new MutableExecutionContextAccessor();
        var originalWorkspace = new InMemoryWorkspace();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: originalWorkspace));
        var capture = HarnessExecutionBinding.Capture(
            accessor,
            HarnessCompositionTestFixture.SessionId,
            requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);
        var historyProvider = new InMemoryChatHistoryProvider(
            new InMemoryChatHistoryProviderOptions());
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                historyProvider));
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);
        var session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        var serialized = await agent.SerializeSessionAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);

        // Rebinding to a new (different, host-authorized) workspace under the same
        // identity/session must never be satisfied by anything in the serialized payload;
        // it is revalidated against the ambient execution context alone.
        accessor.Clear();
        using var rebindScope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: new InMemoryWorkspace()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                serialized,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("binding", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSession_OutsideAuthorizedContext_FailsClosed()
    {
        var function = AIFunctionFactory.Create(() => "unused", "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        var historyProvider = new InMemoryChatHistoryProvider(
            new InMemoryChatHistoryProviderOptions());
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                historyProvider));
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);
        scope.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.CreateSessionAsync(
                TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task SerializeSession_OutsideAuthorizedContext_FailsClosed()
    {
        var function = AIFunctionFactory.Create(() => "unused", "G3Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        var historyProvider = new InMemoryChatHistoryProvider(
            new InMemoryChatHistoryProviderOptions());
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                historyProvider));
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);
        var session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        scope.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.SerializeSessionAsync(
                session,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task Compose_NoHistoryPreservesExistingSessionLifecycleBehavior()
    {
        var function = AIFunctionFactory.Create(() => "unused", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor);
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);
        scope.Dispose();

        var session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        var serialized = await agent.SerializeSessionAsync(
            session,
            cancellationToken: TestContext.Current.CancellationToken);
        var restored = await agent.DeserializeSessionAsync(
            serialized,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(restored);
    }

    private static HarnessProviderCompositionResult ComposeForFailure(
        HarnessCapabilityProfile profile,
        HarnessHistoryProviderPlugin? historyProvider)
    {
        var function = AIFunctionFactory.Create(() => "ok", "G3Tool");
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
                historyProvider: historyProvider);
            return new HarnessProviderComposition().Compose(request);
        }
    }

    private static (AIAgent Agent, JsonElement Serialized) ComposeAndSerialize()
    {
        // Deliberately synchronous (using GetAwaiter().GetResult()) rather than async:
        // IAgentExecutionContextAccessor.BeginScope relies on AsyncLocal, and AsyncLocal
        // mutations made inside a Task-returning method are never visible to its caller
        // once that method returns. Keeping this helper synchronous lets the scope it
        // opens remain visible to the calling test method's own subsequent awaits.
        var function = AIFunctionFactory.Create(() => "durable-result", "G3Tool");
        var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        var historyProvider = new InMemoryChatHistoryProvider(
            new InMemoryChatHistoryProviderOptions());
        var chatClient = new HarnessScriptedChatClient(function.Name);
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreateHistoryProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                HarnessHistoryPersistenceMode.DurableProvider),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                historyProvider));
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);

        var session = agent.CreateSessionAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        agent.RunAsync("run", session, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var serialized = agent.SerializeSessionAsync(
                session,
                cancellationToken: CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        // Keep the scope alive for the caller — deserialize tests run within the same
        // authorized context that produced the serialized envelope unless they
        // deliberately change it.
        return (agent, serialized);
    }

    private static JsonElement TamperEnvelope(
        JsonElement serialized,
        string propertyName,
        object replacementValue)
    {
        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node[propertyName] = JsonValue.Create(replacementValue);
        return JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
    }

    private static void AssertEnvelopeNeverCarriesAWorkspace(JsonElement serialized)
    {
        AssertNoWorkspaceProperty(serialized);
    }

    private static void AssertNoWorkspaceProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(
                    "workspace",
                    property.Name,
                    StringComparison.OrdinalIgnoreCase);
                AssertNoWorkspaceProperty(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoWorkspaceProperty(item);
            }
        }
    }
}
