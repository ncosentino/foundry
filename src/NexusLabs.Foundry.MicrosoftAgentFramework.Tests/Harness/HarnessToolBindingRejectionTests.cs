using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Covers rejection of a tool call whose trusted execution binding expires between the model
/// response and function invocation, including the streaming path, and pins the upstream
/// function-invocation behavior that makes a second guard necessary.
/// </summary>
public sealed class HarnessToolBindingRejectionTests
{
    /// <summary>
    /// The composed pipeline reads the execution context once when the guard revalidates the
    /// delivered model response, so allowing exactly that one read places the next read — the
    /// pre-invocation check inside the function invoker — on an invalidated binding.
    /// </summary>
    private const int ReadsBeforeFunctionInvocation = 1;

    [Fact]
    public async Task Compose_StreamingToolCallScopeExpiresAtInvocation_NeverInvokesTool()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                return "ok";
            },
            "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new HarnessDeferredInvalidationAccessor(ReadsBeforeFunctionInvocation);
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
        var chatClient = new HarnessStreamingToolCallChatClient(function.Name, accessor.Arm);
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
        var agent = Assert.IsAssignableFrom<Microsoft.Agents.AI.AIAgent>(composition.Agent);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var update in agent.RunStreamingAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Equal(0, invocationCount);
        Assert.Equal(1, chatClient.CallCount);
        Assert.Equal(ReadsBeforeFunctionInvocation, accessor.ReadsServedAfterArm);
    }

    [Fact]
    public async Task Compose_ToolCallScopeExpiresAtInvocation_SendsNoFurtherProviderRequest()
    {
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                return "ok";
            },
            "G2Tool");
        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new HarnessDeferredInvalidationAccessor(ReadsBeforeFunctionInvocation);
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
        var chatClient = new HarnessScriptedChatClient(function.Name, accessor.Arm);
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
        var agent = Assert.IsAssignableFrom<Microsoft.Agents.AI.AIAgent>(composition.Agent);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, invocationCount);
        Assert.Equal(1, chatClient.CallCount);
        Assert.Equal(ReadsBeforeFunctionInvocation, accessor.ReadsServedAfterArm);
    }

    [Fact]
    public async Task UpstreamFunctionInvoker_ThrownException_BecomesToolErrorAndContinuesLoop()
    {
        var leaf = new HarnessFunctionResultRecordingChatClient("ProbeTool");
        var function = AIFunctionFactory.Create(() => "tool-ran", "ProbeTool");
        using var functionInvokingChatClient = new FunctionInvokingChatClient(leaf)
        {
            FunctionInvoker = (_, _) =>
                throw new InvalidOperationException("binding-rejected"),
        };

        var response = await functionInvokingChatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            new ChatOptions { Tools = [function] },
            TestContext.Current.CancellationToken);

        Assert.Equal("model-result", response.Text);
        Assert.Equal(2, leaf.CallCount);
        var observed = Assert.Single(leaf.ObservedResults);
        Assert.Equal("g2-call", observed.CallId);
        Assert.Equal("binding-rejected", observed.Exception?.Message);
    }

    [Fact]
    public async Task UpstreamStreamingFunctionInvoker_ThrownException_BecomesToolErrorAndContinuesLoop()
    {
        var leaf = new HarnessFunctionResultRecordingChatClient("ProbeTool");
        var function = AIFunctionFactory.Create(() => "tool-ran", "ProbeTool");
        using var functionInvokingChatClient = new FunctionInvokingChatClient(leaf)
        {
            FunctionInvoker = (_, _) =>
                throw new InvalidOperationException("binding-rejected"),
        };

        await foreach (var update in functionInvokingChatClient.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "go")],
            new ChatOptions { Tools = [function] },
            TestContext.Current.CancellationToken))
        {
        }

        Assert.Equal(2, leaf.CallCount);
        var observed = Assert.Single(leaf.ObservedResults);
        Assert.Equal("binding-rejected", observed.Exception?.Message);
    }
}
