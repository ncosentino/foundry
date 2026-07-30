using System.Text.Json;

using GitHub.Copilot;

using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp.Tests;

public sealed class CopilotSdkTurnCollectorTests
{
    [Fact]
    public async Task Completion_WaitsForUsageAndExternalToolCorrelation()
    {
        var collector = new CopilotSdkTurnCollector("fallback-model");
        collector.Observe(new AssistantMessageEvent
        {
            Data = new AssistantMessageData
            {
                MessageId = "message-1",
                Model = "gpt-5-mini",
                Content = "",
                ToolRequests =
                [
                    new AssistantMessageToolRequest
                    {
                        ToolCallId = "call-1",
                        Name = "lookup_value",
                        Arguments = JsonDocument.Parse("""{"id":"alpha"}""").RootElement.Clone(),
                    },
                ],
            },
        });
        collector.Observe(new ExternalToolRequestedEvent
        {
            Data = new ExternalToolRequestedData
            {
                RequestId = "request-1",
                SessionId = "session-1",
                ToolCallId = "call-1",
                ToolName = "lookup_value",
            },
        });

        Assert.False(collector.Completion.IsCompleted);
        collector.Observe(new AssistantUsageEvent
        {
            Data = new AssistantUsageData
            {
                Model = "gpt-5-mini",
                InputTokens = 80,
                OutputTokens = 10,
                FinishReason = "tool_calls",
            },
        });

        var result = await collector.Completion;
        Assert.Equal(ChatFinishReason.ToolCalls, result.FinishReason);
        Assert.Equal(90, result.InputTokens + result.OutputTokens);
        var call = Assert.Single(result.ToolCalls);
        Assert.Equal("lookup_value", call.Name);
        Assert.Equal("alpha", call.Arguments["id"]?.ToString());
    }

    [Fact]
    public async Task Completion_CompletesAfterMessageAndUsageWhenNoToolIsRequested()
    {
        var collector = new CopilotSdkTurnCollector("fallback-model");
        collector.Observe(new AssistantUsageEvent
        {
            Data = new AssistantUsageData
            {
                Model = "gpt-5-mini",
                InputTokens = 20,
                OutputTokens = 5,
                FinishReason = "stop",
            },
        });
        collector.Observe(new AssistantMessageEvent
        {
            Data = new AssistantMessageData
            {
                MessageId = "message-2",
                Model = "gpt-5-mini",
                Content = "done",
            },
        });

        var result = await collector.Completion;
        Assert.Equal("done", result.Text);
        Assert.Equal(ChatFinishReason.Stop, result.FinishReason);
        Assert.Empty(result.ToolCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("future_reason")]
    public async Task Completion_RejectsUnsupportedFinishReason(string? finishReason)
    {
        var collector = new CopilotSdkTurnCollector("fallback-model");
        collector.Observe(new AssistantMessageEvent
        {
            Data = new AssistantMessageData
            {
                MessageId = "message-3",
                Model = "gpt-5-mini",
                Content = "done",
            },
        });
        collector.Observe(new AssistantUsageEvent
        {
            Data = new AssistantUsageData
            {
                Model = "gpt-5-mini",
                InputTokens = 20,
                OutputTokens = 5,
                FinishReason = finishReason,
            },
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await collector.Completion);
        Assert.Contains("unsupported finish reason", exception.Message);
    }
}
