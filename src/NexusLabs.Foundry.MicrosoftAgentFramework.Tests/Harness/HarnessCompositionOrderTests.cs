using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessCompositionOrderTests
{
    [Fact]
    public async Task Compose_HarnessOwnedPipeline_ExecutesGeneratedToolWithExpectedMiddleware()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "tool-result";
            },
            "G2Tool");
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
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            Assert.Same(request.Profile, result.Profile);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
            Assert.Null(agent.GetService<FunctionInvokingChatClient>());
            Assert.Null(agent.GetService<ChatClientAgent>());
            Assert.Null(agent.GetService<IChatClient>());
            Assert.Null(agent.GetService<IDisposable>());
            Assert.Null(agent.GetService<MessageInjectingChatClient>());
            Assert.Null(agent.GetService<OpenTelemetryChatClient>());
            Assert.NotNull(agent.GetService<IHarnessMessageInjector>());

            var response = await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("tool-result", response.GetText());
            Assert.Equal(1, invocationCount);
            Assert.Equal(2, chatClient.CallCount);
        }
    }

    [Fact]
    public async Task Compose_FoundryOwnedPipeline_UsesDiagnosticsLoopWithoutUpstreamTelemetry()
    {
        var function = AIFunctionFactory.Create(() => "foundry-result", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var meterName = $"Foundry.G2.{Guid.NewGuid():N}";
            using var metricCapture = new HarnessMetricCapture(meterName);
            using var metrics = new AgentMetrics(
                new AgentFrameworkMetricsOptions
                {
                    MeterName = meterName,
                    ActivitySourceName = meterName,
                });
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Foundry,
                    HarnessTelemetryOwner.Foundry),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(HarnessProviderCompositionStatus.Success, result.Status);
            var agent = Assert.IsAssignableFrom<AIAgent>(result.Agent);
            Assert.Null(agent.GetService<DiagnosticsFunctionInvokingChatClient>());
            Assert.Null(agent.GetService<ChatClientAgent>());
            Assert.Null(agent.GetService<IChatClient>());
            Assert.Null(agent.GetService<IDisposable>());
            Assert.Null(agent.GetService<DiagnosticsRecordingChatClient>());
            Assert.Null(agent.GetService<OpenTelemetryChatClient>());
            Assert.NotNull(agent.GetService<IHarnessMessageInjector>());

            var response = await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("foundry-result", response.GetText());
            Assert.Equal(1, metricCapture.Count("agent.tool.completed"));
            Assert.Equal(2, metricCapture.Count("agent.chat.duration"));
        }
    }

    [Fact]
    public async Task Compose_NonStreamingInjectionLoop_RevalidatesBeforeEveryModelCall()
    {
        var function = AIFunctionFactory.Create(() => "unused", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new MutableExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: new InMemoryWorkspace()));
        var capture = HarnessExecutionBinding.Capture(
            accessor,
            HarnessCompositionTestFixture.SessionId,
            requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);
        IHarnessMessageInjector? injector = null;
        AgentSession? session = null;
        var chatClient = new HarnessScriptedChatClient(
            function.Name,
            () =>
            {
                injector!.EnqueueMessagesAsync(
                    session!,
                    [new ChatMessage(ChatRole.User, "injected")],
                    CancellationToken.None).GetAwaiter().GetResult();
                accessor.Clear();
            },
            requestFunctionCall: false);
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor);
        var composition = new HarnessProviderComposition().Compose(request);
        var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
        injector = Assert.IsAssignableFrom<IHarnessMessageInjector>(
            agent.GetService<IHarnessMessageInjector>());
        session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(
                "run",
                session,
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task Compose_StreamingInjectionLoop_RevalidatesBeforeEveryModelCall()
    {
        var function = AIFunctionFactory.Create(() => "unused", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new MutableExecutionContextAccessor();
        using var scope = accessor.BeginScope(
            new AgentExecutionContext(
                "user-1",
                "orchestration-1",
                Workspace: new InMemoryWorkspace()));
        var capture = HarnessExecutionBinding.Capture(
            accessor,
            HarnessCompositionTestFixture.SessionId,
            requireWorkspace: true);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);
        IHarnessMessageInjector? injector = null;
        AgentSession? session = null;
        var chatClient = new HarnessInjectionLoopStreamingChatClient(
            () =>
            {
                injector!.EnqueueMessagesAsync(
                    session!,
                    [new ChatMessage(ChatRole.User, "injected")],
                    CancellationToken.None).GetAwaiter().GetResult();
                accessor.Clear();
            });
        var request = HarnessCompositionTestFixture.CreateRequest(
            chatClient,
            services,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor);
        var composition = new HarnessProviderComposition().Compose(request);
        var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
        injector = Assert.IsAssignableFrom<IHarnessMessageInjector>(
            agent.GetService<IHarnessMessageInjector>());
        session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        var updates = new List<AgentResponseUpdate>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var update in agent.RunStreamingAsync(
                "run",
                session,
                cancellationToken: TestContext.Current.CancellationToken))
            {
                updates.Add(update);
            }
        });
        Assert.Single(updates);
        Assert.Equal(1, chatClient.CallCount);
    }
}
