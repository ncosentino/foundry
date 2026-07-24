using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessProviderCompositionTests
{
    [Fact]
    public void Compose_ExpiredExecutionScope_FailsClosed()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        scope.Dispose();
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessScriptedChatClient(function.Name),
            services,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor);

        var result = new HarnessProviderComposition().Compose(request);

        Assert.Equal(
            HarnessProviderCompositionStatus.ExecutionBindingInvalid,
            result.Status);
        Assert.Null(result.Agent);
    }

    [Fact]
    public void Compose_FailedToolResolution_FailsClosedWithoutAgent()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient("unused"),
                services,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness),
                new HarnessGeneratedToolResolution(
                    HarnessGeneratedToolResolutionStatus.MissingGeneratedFunctionType,
                    [],
                    [typeof(string)],
                    []),
                binding,
                accessor);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.GeneratedToolResolutionFailed,
                result.Status);
            Assert.Null(result.Agent);
        }
    }

    [Fact]
    public void Compose_EnabledGeneratedToolsWithEmptyResult_FailsClosed()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient("unused"),
                services,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness),
                new HarnessGeneratedToolResolution(
                    HarnessGeneratedToolResolutionStatus.Success,
                    [],
                    [],
                    []),
                binding,
                accessor);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.GeneratedToolsEmpty,
                result.Status);
            Assert.Null(result.Agent);
        }
    }

    [Fact]
    public async Task Compose_AgentRunAfterScopeEnds_FailsBeforeModelCall()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
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
        var composition = new HarnessProviderComposition().Compose(request);
        var agent = Assert.IsAssignableFrom<Microsoft.Agents.AI.AIAgent>(
            composition.Agent);
        scope.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task Compose_ScopeExpiresAfterModelResponse_FailsBeforeToolInvocation()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "ok";
            },
            "G2Tool");
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
        var chatClient = new HarnessScriptedChatClient(
            function.Name,
            accessor.Clear);
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
        var agent = Assert.IsAssignableFrom<Microsoft.Agents.AI.AIAgent>(
            composition.Agent);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, invocationCount);
        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task Compose_ContextExpiresDuringFinalModelResponse_FailsClosed()
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
        var chatClient = new HarnessScriptedChatClient(
            function.Name,
            accessor.Clear,
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

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task Compose_RunTimeChatClientFactory_IsRejectedBeforeReplacement()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
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
            var composition = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
            var replacementUsed = false;
            var runOptions = new ChatClientAgentRunOptions
            {
                ChatClientFactory = _ =>
                {
                    replacementUsed = true;
                    return new HarnessScriptedChatClient(function.Name);
                },
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await agent.RunAsync(
                    "run",
                    options: runOptions,
                    cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(replacementUsed);
            Assert.Equal(0, chatClient.CallCount);
        }
    }

    [Fact]
    public async Task Compose_RunTimeToolInjection_IsRejectedBeforeModelCall()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        var injectedInvocationCount = 0;
        var injectedFunction = AIFunctionFactory.Create(
            () =>
            {
                injectedInvocationCount++;
                return "injected";
            },
            "InjectedTool");
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
            var composition = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
            var runOptions = new ChatClientAgentRunOptions(
                new ChatOptions
                {
                    Tools = [injectedFunction],
                });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await agent.RunAsync(
                    "run",
                    options: runOptions,
                    cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(0, injectedInvocationCount);
            Assert.Equal(0, chatClient.CallCount);
        }
    }

    [Fact]
    public async Task Compose_OtherRunOptions_AreRejectedBeforeModelCall()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
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
            var composition = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
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

    [Fact]
    public async Task Compose_StreamingRunOptions_AreRejectedBeforeModelCall()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var chatClient = new HarnessInjectionLoopStreamingChatClient(
                static () => { });
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
            var runOptions = new ChatClientAgentRunOptions(
                new ChatOptions
                {
                    MaxOutputTokens = 32,
                });

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var _ in agent.RunStreamingAsync(
                    "run",
                    options: runOptions,
                    cancellationToken: TestContext.Current.CancellationToken))
                {
                }
            });
            Assert.Equal(0, chatClient.CallCount);
        }
    }

    [Fact]
    public async Task Compose_MessageInjectorRejectsExpiredExecutionScope()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
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
        var composition = new HarnessProviderComposition().Compose(request);
        var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
        var injector = Assert.IsAssignableFrom<IHarnessMessageInjector>(
            agent.GetService<IHarnessMessageInjector>());
        var session = await agent.CreateSessionAsync(
            TestContext.Current.CancellationToken);
        scope.Dispose();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await injector.EnqueueMessagesAsync(
                session,
                [new ChatMessage(ChatRole.User, "injected")],
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Compose_FoundryOwnedPipeline_PassesServicesToToolInvocation()
    {
        using var services = HarnessCompositionTestFixture.CreateServices();
        var function = new HarnessServiceInspectingFunction(services);
        using var metrics = new AgentMetrics();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(
            accessor,
            out var scope);
        using (scope)
        {
            var request = HarnessCompositionTestFixture.CreateRequest(
                new HarnessScriptedChatClient(function.Name),
                services,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Foundry,
                    HarnessTelemetryOwner.Foundry),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics);
            var composition = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);

            var response = await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("service-result", response.GetText());
        }
    }

    [Fact]
    public void Compose_FoundryTelemetryWithoutMetrics_FailsClosed()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
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
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Foundry,
                    HarnessTelemetryOwner.Foundry),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor);

            var result = new HarnessProviderComposition().Compose(request);

            Assert.Equal(
                HarnessProviderCompositionStatus.FoundryMetricsMissing,
                result.Status);
            Assert.Null(result.Agent);
        }
    }

    [Fact]
    public async Task Compose_StreamingContextExpiresBetweenUpdates_FailsBeforeSecondChunk()
    {
        var function = AIFunctionFactory.Create(() => "ok", "G2Tool");
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
        var request = HarnessCompositionTestFixture.CreateRequest(
            new HarnessStreamingChatClient(accessor.Clear),
            services,
            HarnessCompositionTestFixture.CreateProfile(
                HarnessToolLoopOwner.Harness,
                HarnessTelemetryOwner.Harness),
            HarnessCompositionTestFixture.CreateToolResolution(function),
            binding,
            accessor);
        var composition = new HarnessProviderComposition().Compose(request);
        var agent = Assert.IsAssignableFrom<Microsoft.Agents.AI.AIAgent>(
            composition.Agent);
        var updates = new List<Microsoft.Agents.AI.AgentResponseUpdate>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var update in agent.RunStreamingAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken))
            {
                updates.Add(update);
            }
        });
        Assert.Single(updates);
    }
}
