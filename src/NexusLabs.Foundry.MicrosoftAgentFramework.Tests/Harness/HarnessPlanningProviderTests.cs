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

public sealed class HarnessPlanningProviderTests
{
    // ------------------------------------------------------------------
    // Plugin construction (T033)
    // ------------------------------------------------------------------

    [Fact]
    public void CreatePlanningProvidersPlugin_NeitherProviderSupplied_FailsClosed() =>
        Assert.Throws<InvalidOperationException>(() =>
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                todoProvider: null,
                agentModeProvider: null));

    [Fact]
    public void CreatePlanningProvidersPlugin_TodoOnly_ExposesOnlyTodoProvider()
    {
        var todoProvider = new TodoProvider(new TodoProviderOptions());
        var plugin = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
            todoProvider,
            agentModeProvider: null);

        Assert.Same(todoProvider, plugin.TodoProvider);
        Assert.Null(plugin.AgentModeProvider);
        Assert.Equal(["TodoProvider"], plugin.ProviderStateKeys);
        Assert.Same(todoProvider, Assert.Single(plugin.AIContextProviders));
    }

    [Fact]
    public void CreatePlanningProvidersPlugin_AgentModeOnly_ExposesOnlyAgentModeProvider()
    {
        var agentModeProvider = CreateAgentModeProvider();
        var plugin = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
            todoProvider: null,
            agentModeProvider);

        Assert.Null(plugin.TodoProvider);
        Assert.Same(agentModeProvider, plugin.AgentModeProvider);
        Assert.Equal(["AgentModeProvider"], plugin.ProviderStateKeys);
        Assert.Same(agentModeProvider, Assert.Single(plugin.AIContextProviders));
    }

    [Fact]
    public void CreatePlanningProvidersPlugin_Combined_ExposesBothProvidersWithUnionKeys()
    {
        var todoProvider = new TodoProvider(new TodoProviderOptions());
        var agentModeProvider = CreateAgentModeProvider();
        var plugin = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
            todoProvider,
            agentModeProvider);

        Assert.Same(todoProvider, plugin.TodoProvider);
        Assert.Same(agentModeProvider, plugin.AgentModeProvider);
        Assert.Equal(["AgentModeProvider", "TodoProvider"], plugin.ProviderStateKeys);
        Assert.Equal(2, plugin.AIContextProviders.Count);
    }

    // ------------------------------------------------------------------
    // Independent selectability through composition (T032)
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_TodoOnly_ExposesOnlyTodoProviderAndActivatesNoUnselectedProvider()
    {
        var todoProvider = new TodoProvider(new TodoProviderOptions());
        var result = ComposeForSuccess(
            includeTodo: true,
            includeAgentMode: false,
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                todoProvider,
                agentModeProvider: null));

        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
        Assert.Null(agent.GetService<TodoProvider>());
        Assert.NotNull(agent.GetService<IHarnessTodoAccessor>());
        Assert.Null(agent.GetService<AgentModeProvider>());
        Assert.Null(agent.GetService<IHarnessAgentModeAccessor>());
    }

    [Fact]
    public void Compose_AgentModeOnly_ExposesOnlyAgentModeProviderAndActivatesNoUnselectedProvider()
    {
        var agentModeProvider = CreateAgentModeProvider();
        var result = ComposeForSuccess(
            includeTodo: false,
            includeAgentMode: true,
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                todoProvider: null,
                agentModeProvider));

        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
        Assert.Null(agent.GetService<TodoProvider>());
        Assert.Null(agent.GetService<IHarnessTodoAccessor>());
        Assert.Null(agent.GetService<AgentModeProvider>());
        Assert.NotNull(agent.GetService<IHarnessAgentModeAccessor>());
    }

    [Fact]
    public void Compose_TodoAndAgentMode_ExposesBothProviders()
    {
        var todoProvider = new TodoProvider(new TodoProviderOptions());
        var agentModeProvider = CreateAgentModeProvider();
        var result = ComposeForSuccess(
            includeTodo: true,
            includeAgentMode: true,
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                todoProvider,
                agentModeProvider));

        Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
        var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
        Assert.Null(agent.GetService<TodoProvider>());
        Assert.Null(agent.GetService<AgentModeProvider>());
        Assert.NotNull(agent.GetService<IHarnessTodoAccessor>());
        Assert.NotNull(agent.GetService<IHarnessAgentModeAccessor>());
    }

    // ------------------------------------------------------------------
    // Fail-closed guard combinations
    // ------------------------------------------------------------------

    [Fact]
    public void Compose_TodoEnabledWithoutPlugin_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: true,
                includeAgentMode: false),
            planningProviders: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.TodoProviderRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_AgentModeEnabledWithoutPlugin_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: false,
                includeAgentMode: true),
            planningProviders: null);

        Assert.Equal(
            HarnessProviderCompositionStatus.AgentModeProviderRequired,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_PluginWhenNeitherCapabilityEnabled_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                new TodoProvider(new TodoProviderOptions()),
                agentModeProvider: null));

        Assert.Equal(
            HarnessProviderCompositionStatus.PlanningPluginUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_TodoProviderSuppliedWhileTodoDisabled_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: false,
                includeAgentMode: true),
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                new TodoProvider(new TodoProviderOptions()),
                CreateAgentModeProvider()));

        Assert.Equal(
            HarnessProviderCompositionStatus.TodoProviderUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_AgentModeProviderSuppliedWhileAgentModeDisabled_FailsClosedWithoutAgent()
    {
        var result = ComposeForFailure(
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: true,
                includeAgentMode: false),
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                new TodoProvider(new TodoProviderOptions()),
                CreateAgentModeProvider()));

        Assert.Equal(
            HarnessProviderCompositionStatus.AgentModeProviderUnexpected,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_CollidingCustomProviderStateKey_FailsClosedBeforeAgentIsBuilt()
    {
        var function = AIFunctionFactory.Create(() => "ok", "PlanningTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var historyPlugin = HarnessCompositionTestFixture.CreateHistoryProviderPlugin(
                HarnessHistoryPersistenceMode.DurableProvider,
                new HarnessCollidingChatHistoryProvider());
            var planningProviders = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                new TodoProvider(new TodoProviderOptions()),
                agentModeProvider: null);

            var resolver = new HarnessCapabilityResolver();
            var profile = resolver.Resolve(
                new HarnessCapabilityResolutionRequest(
                    ProfileId: "planning-collision-test",
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
                        HarnessCapability.Todo,
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
                planningProviders: planningProviders);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.ProviderStateKeyCollision,
                result.Status);
            Assert.Null(result.Agent);
        }
    }

    // ------------------------------------------------------------------
    // Todo tool conformance (MAF 1.15 todos_add / todos_complete)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_TodoAddThenComplete_ReflectsCompletionStateViaProvider()
    {
        var function = AIFunctionFactory.Create(() => "unused", "PlanningTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var todoProvider = new TodoProvider(new TodoProviderOptions());
            var planningProviders = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                todoProvider,
                agentModeProvider: null);
            var chatClient = new HarnessQueuedFunctionCallChatClient(
                ("todos_add", ParseArguments(
                    """{"todos":[{"title":"Write tests","description":"Add planning tests"}]}""")),
                ("todos_complete", ParseArguments(
                    """{"items":[{"id":1,"reason":"done"}]}""")));
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreatePlanningProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeTodo: true,
                    includeAgentMode: false),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: planningProviders);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
            var todoAccessor = Assert.IsAssignableFrom<IHarnessTodoAccessor>(
                agent.GetService<IHarnessTodoAccessor>());

            var session = await agent.CreateSessionAsync(
                TestContext.Current.CancellationToken);
            await agent.RunAsync(
                "please add and complete a todo",
                session,
                cancellationToken: TestContext.Current.CancellationToken);

            var allTodos = await todoAccessor.GetAllTodosAsync(
                session,
                TestContext.Current.CancellationToken);
            var remaining = await todoAccessor.GetRemainingTodosAsync(
                session,
                TestContext.Current.CancellationToken);

            var todo = Assert.Single(allTodos);
            Assert.Equal("Write tests", todo.Title);
            Assert.Equal("Add planning tests", todo.Description);
            Assert.True(todo.IsComplete);
            Assert.Empty(remaining);
        }
    }

    // ------------------------------------------------------------------
    // AgentMode conformance (default mode, transition, invalid mode)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_AgentModeDefaultAndTransition_ConformsToProviderContract()
    {
        var function = AIFunctionFactory.Create(() => "unused", "PlanningTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var agentModeProvider = CreateAgentModeProvider();
            var planningProviders = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                todoProvider: null,
                agentModeProvider);
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(
                    function.Name,
                    static () => { },
                    requestFunctionCall: false),
                services,
                HarnessCompositionTestFixture.CreatePlanningProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeTodo: false,
                    includeAgentMode: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: planningProviders);

            var result = new HarnessProviderComposition().Compose(request);
            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
            var modeAccessor = Assert.IsAssignableFrom<IHarnessAgentModeAccessor>(
                agent.GetService<IHarnessAgentModeAccessor>());

            var session = await agent.CreateSessionAsync(
                TestContext.Current.CancellationToken);

            var defaultMode = await modeAccessor.GetModeAsync(
                session,
                TestContext.Current.CancellationToken);
            Assert.Equal("default", defaultMode);

            await modeAccessor.SetModeAsync(
                session,
                "focused",
                TestContext.Current.CancellationToken);
            var transitionedMode = await modeAccessor.GetModeAsync(
                session,
                TestContext.Current.CancellationToken);
            Assert.Equal("focused", transitionedMode);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                modeAccessor.SetModeAsync(
                    session,
                    "not-a-real-mode",
                    TestContext.Current.CancellationToken));
            Assert.Contains("Invalid mode", exception.Message);
        }
    }

    // ------------------------------------------------------------------
    // Session serialize/deserialize round trip and tampering (T032)
    // ------------------------------------------------------------------

    [Fact]
    public async Task
        DeserializeSession_FreshAgentUnderCurrentWorkspaceRestoresTodoAndModeState()
    {
        var function = AIFunctionFactory.Create(() => "unused", "PlanningTool");
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
            var originalTodoProvider = new TodoProvider(new TodoProviderOptions());
            var originalAgentModeProvider = CreateAgentModeProvider();
            var originalPlanningProviders =
                HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                    originalTodoProvider,
                    originalAgentModeProvider);
            var originalChatClient = new HarnessQueuedFunctionCallChatClient(
                ("todos_add", ParseArguments(
                    """{"todos":[{"title":"Write tests","description":"Add planning tests"}]}""")));
            var originalRequest = HarnessCompositionTestFixture.CreateRequest(
                originalChatClient,
                originalServices,
                HarnessCompositionTestFixture.CreatePlanningProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeTodo: true,
                    includeAgentMode: true),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                originalBinding,
                originalAccessor,
                metrics: null,
                historyProvider: null,
                planningProviders: originalPlanningProviders);
            var originalAgent = Assert.IsAssignableFrom<AIAgent>(
                new HarnessProviderComposition().Compose(originalRequest).Agent);
            var originalModeAccessor =
                Assert.IsAssignableFrom<IHarnessAgentModeAccessor>(
                    originalAgent.GetService<IHarnessAgentModeAccessor>());
            var originalSession = await originalAgent.CreateSessionAsync(
                TestContext.Current.CancellationToken);
            await originalModeAccessor.SetModeAsync(
                originalSession,
                "focused",
                TestContext.Current.CancellationToken);
            await originalAgent.RunAsync(
                "add a todo",
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
        var currentTodoProvider = new TodoProvider(new TodoProviderOptions());
        var currentAgentModeProvider = CreateAgentModeProvider();
        var currentPlanningProviders =
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                currentTodoProvider,
                currentAgentModeProvider);
        var currentRequest = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            currentServices,
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: true,
                includeAgentMode: true),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            currentBinding,
            currentAccessor,
            metrics: null,
            historyProvider: null,
            planningProviders: currentPlanningProviders);
        var currentAgent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(currentRequest).Agent);
        var currentTodoAccessor = Assert.IsAssignableFrom<IHarnessTodoAccessor>(
            currentAgent.GetService<IHarnessTodoAccessor>());
        var currentModeAccessor = Assert.IsAssignableFrom<IHarnessAgentModeAccessor>(
            currentAgent.GetService<IHarnessAgentModeAccessor>());

        var restoredSession = await currentAgent.DeserializeSessionAsync(
            serialized,
            cancellationToken: TestContext.Current.CancellationToken);

        var restoredTodos = await currentTodoAccessor.GetAllTodosAsync(
            restoredSession,
            TestContext.Current.CancellationToken);
        var restoredMode = await currentModeAccessor.GetModeAsync(
            restoredSession,
            TestContext.Current.CancellationToken);

        Assert.Equal("Write tests", Assert.Single(restoredTodos).Title);
        Assert.Equal("focused", restoredMode);
        Assert.Same(currentWorkspace, currentBinding.Workspace);
        Assert.NotSame(originalWorkspace, currentWorkspace);
    }

    [Fact]
    public async Task DeserializeSession_ProviderStateKeyProfileMismatch_FailsClosed()
    {
        var (agent, serialized) = ComposeAndSerializePlanning();

        var node = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        node["providerStateKeys"] = new JsonArray("a-different-provider-key");
        var tampered = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.DeserializeSessionAsync(
                tampered,
                cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("provider state keys", exception.Message);
    }

    // ------------------------------------------------------------------
    // G2 regression: no history, no planning
    // ------------------------------------------------------------------

    [Fact]
    public async Task Compose_NoHistoryNoPlanning_PreservesRawMafSessionLifecycle()
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
            accessor,
            metrics: null,
            historyProvider: null,
            planningProviders: null);
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

    [Fact]
    public async Task Compose_PlanningAccessorsRejectExpiredExecutionContext()
    {
        var function = AIFunctionFactory.Create(() => "unused", "PlanningTool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        var planningProviders =
            HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
                new TodoProvider(new TodoProviderOptions()),
                CreateAgentModeProvider());
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: true,
                includeAgentMode: true),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: null,
            planningProviders: planningProviders);
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);
        var session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        var todoAccessor = Assert.IsAssignableFrom<IHarnessTodoAccessor>(
            agent.GetService<IHarnessTodoAccessor>());
        var modeAccessor = Assert.IsAssignableFrom<IHarnessAgentModeAccessor>(
            agent.GetService<IHarnessAgentModeAccessor>());
        scope.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            todoAccessor.GetAllTodosAsync(
                session,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            modeAccessor.GetModeAsync(
                session,
                TestContext.Current.CancellationToken));
        Assert.Null(agent.GetService<TodoProvider>());
        Assert.Null(agent.GetService<AgentModeProvider>());
        Assert.Null(agent.GetService<AIContextProvider>());
        Assert.Null(agent.GetService<ChatClientAgentOptions>());
        Assert.Null(agent.GetService<ChatOptions>());
        Assert.Null(agent.GetService<ChatHistoryProvider>());
        Assert.Null(agent.GetService<IDisposable>());
    }

    private static AgentModeProvider CreateAgentModeProvider() =>
        new(new AgentModeProviderOptions
        {
            Modes =
            [
                new AgentModeProviderOptions.AgentMode(
                    "default",
                    "Default mode instructions"),
                new AgentModeProviderOptions.AgentMode(
                    "focused",
                    "Focused mode instructions"),
            ],
            DefaultMode = "default",
        });

    private static IReadOnlyDictionary<string, object?> ParseArguments(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;

    private static HarnessProviderCompositionResult ComposeForSuccess(
        bool includeTodo,
        bool includeAgentMode,
        HarnessPlanningProvidersPlugin planningProviders)
    {
        var function = AIFunctionFactory.Create(() => "ok", "PlanningTool");
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
                HarnessCompositionTestFixture.CreatePlanningProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness,
                    includeTodo,
                    includeAgentMode),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: planningProviders);
            return new HarnessProviderComposition().Compose(request);
        }
    }

    private static HarnessProviderCompositionResult ComposeForFailure(
        HarnessCapabilityProfile profile,
        HarnessPlanningProvidersPlugin? planningProviders)
    {
        var function = AIFunctionFactory.Create(() => "ok", "PlanningTool");
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
                planningProviders: planningProviders);
            return new HarnessProviderComposition().Compose(request);
        }
    }

    private static (AIAgent Agent, JsonElement Serialized) ComposeAndSerializePlanning()
    {
        // Deliberately synchronous (using GetAwaiter().GetResult()) rather than async: see
        // HarnessHistoryProviderTests.ComposeAndSerialize for the AsyncLocal-scope rationale.
        var function = AIFunctionFactory.Create(() => "unused", "PlanningTool");
        var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out _);
        var todoProvider = new TodoProvider(new TodoProviderOptions());
        var planningProviders = HarnessCompositionTestFixture.CreatePlanningProvidersPlugin(
            todoProvider,
            agentModeProvider: null);
        var chatClient = new HarnessQueuedFunctionCallChatClient(
            ("todos_add", ParseArguments(
                """{"todos":[{"title":"Write tests","description":"Add planning tests"}]}""")));
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreatePlanningProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness,
                includeTodo: true,
                includeAgentMode: false),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor,
            metrics: null,
            historyProvider: null,
            planningProviders: planningProviders);
        var agent = Assert.IsAssignableFrom<AIAgent>(
            new HarnessProviderComposition().Compose(request).Agent);

        var session = agent.CreateSessionAsync(CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        agent.RunAsync("add a todo", session, cancellationToken: CancellationToken.None)
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
