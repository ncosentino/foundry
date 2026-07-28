using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp.Tests;

public sealed class CopilotSdkProviderProbeTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_AcceptsExpectedDeclarationToolCall()
    {
        var calls = 0;
        CopilotSdkTurnRequest? secondRequest = null;
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (request, _) =>
            {
                calls++;
                if (calls == 1)
                {
                    return Task.FromResult(new CopilotSdkTurnResult(
                        ResponseId: "probe-response-1",
                        ModelId: "gpt-5-mini",
                        Text: "",
                        ToolCalls:
                        [
                            new CopilotSdkToolCall(
                                "probe-call",
                                "foundry_harness_probe",
                                new Dictionary<string, object?> { ["value"] = "ready" }),
                        ],
                        InputTokens: 10,
                        OutputTokens: 2,
                        FinishReason: ChatFinishReason.ToolCalls));
                }

                secondRequest = request;
                return Task.FromResult(new CopilotSdkTurnResult(
                    ResponseId: "probe-response-2",
                    ModelId: "gpt-5-mini",
                    Text: "probe-complete",
                    ToolCalls: [],
                    InputTokens: 20,
                    OutputTokens: 3,
                    FinishReason: ChatFinishReason.Stop));
            });

        await CopilotSdkProviderProbe.RunAsync(client, _ct);

        Assert.Equal(2, calls);
        var request = Assert.IsType<CopilotSdkTurnRequest>(secondRequest);
        using var transcript = System.Text.Json.JsonDocument.Parse(request.TranscriptJson);
        var messages = transcript.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(["user", "assistant", "tool"], messages
            .Select(message => message.GetProperty("role").GetString()!)
            .ToArray());
        Assert.Equal(
            "probe-complete",
            messages[2]
                .GetProperty("toolResults")[0]
                .GetProperty("result")
                .GetString());
    }

    [Fact]
    public async Task RunAsync_RejectsSuccessShapedTextWithoutToolCall()
    {
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (_, _) => Task.FromResult(new CopilotSdkTurnResult(
                ResponseId: "probe-response",
                ModelId: "gpt-5-mini",
                Text: "ready",
                ToolCalls: [],
                InputTokens: 10,
                OutputTokens: 2,
                FinishReason: ChatFinishReason.Stop)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CopilotSdkProviderProbe.RunAsync(client, _ct));
    }
}
