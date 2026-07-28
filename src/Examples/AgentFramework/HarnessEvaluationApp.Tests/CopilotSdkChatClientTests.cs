using System.Text.Json;

using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp.Tests;

public sealed class CopilotSdkChatClientTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetResponseAsync_SerializesCompleteTranscriptAndMapsToolCalls()
    {
        CopilotSdkTurnRequest? captured = null;
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (request, _) =>
            {
                captured = request;
                return Task.FromResult(new CopilotSdkTurnResult(
                    ResponseId: "response-1",
                    ModelId: "gpt-5-mini",
                    Text: "Using the lookup.",
                    ToolCalls:
                    [
                        new CopilotSdkToolCall(
                            "call-2",
                            "lookup_value",
                            new Dictionary<string, object?> { ["id"] = "beta" }),
                    ],
                    InputTokens: 100,
                    OutputTokens: 20,
                    FinishReason: ChatFinishReason.ToolCalls));
            });
        var tool = AIFunctionFactory.Create(
            ([System.ComponentModel.Description("Identifier")] string id) => id,
            new AIFunctionFactoryOptions
            {
                Name = "lookup_value",
                Description = "Looks up a deterministic value.",
            });
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "System contract."),
            new ChatMessage(ChatRole.User, "Look up alpha."),
            new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent(
                    "call-1",
                    "lookup_value",
                    new Dictionary<string, object?> { ["id"] = "alpha" })]),
            new ChatMessage(
                ChatRole.Tool,
                [new FunctionResultContent("call-1", "value-alpha")]),
        };

        var response = await client.GetResponseAsync(
            messages,
            new ChatOptions
            {
                Tools = [tool],
                MaxOutputTokens = 2000,
            },
            _ct);

        var request = Assert.IsType<CopilotSdkTurnRequest>(captured);
        Assert.Equal("gpt-5-mini", request.ModelId);
        Assert.Equal(2000, request.MaximumOutputTokens);
        Assert.Equal("lookup_value", Assert.Single(request.Tools).Name);
        using var transcript = JsonDocument.Parse(request.TranscriptJson);
        var serializedMessages = transcript.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(["system", "user", "assistant", "tool"], serializedMessages
            .Select(message => message.GetProperty("role").GetString()!)
            .ToArray());
        Assert.Equal(
            "lookup_value",
            serializedMessages[2]
                .GetProperty("toolCalls")[0]
                .GetProperty("name")
                .GetString());
        Assert.Equal(
            "value-alpha",
            serializedMessages[3]
                .GetProperty("toolResults")[0]
                .GetProperty("result")
                .GetString());

        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal("response-1", response.ResponseId);
        Assert.Equal(120, response.Usage?.TotalTokenCount);
        var assistant = Assert.Single(response.Messages);
        Assert.Equal("Using the lookup.", assistant.Text);
        var call = Assert.Single(assistant.Contents.OfType<FunctionCallContent>());
        Assert.Equal("call-2", call.CallId);
        Assert.Equal("lookup_value", call.Name);
        Assert.Equal("beta", call.Arguments?["id"]?.ToString());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_EmitsFinalTextWithoutStartingAnotherTurn()
    {
        var calls = 0;
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (_, _) =>
            {
                calls++;
                return Task.FromResult(new CopilotSdkTurnResult(
                    ResponseId: "response-2",
                    ModelId: "gpt-5-mini",
                    Text: "complete",
                    ToolCalls: [],
                    InputTokens: 30,
                    OutputTokens: 5,
                    FinishReason: ChatFinishReason.Stop));
            });

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Finish.")],
            cancellationToken: _ct))
        {
            updates.Add(update);
        }

        Assert.Equal(1, calls);
        var singleUpdate = Assert.Single(updates);
        Assert.Equal("complete", singleUpdate.Text);
        Assert.Equal("response-2", singleUpdate.ResponseId);
        Assert.Equal("gpt-5-mini", singleUpdate.ModelId);
    }
}
