using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp.Tests;

public sealed class CopilotSdkProviderProbeTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_AcceptsExpectedDeclarationToolCall()
    {
        var calls = 0;
        CopilotSdkTurnRequest? firstRequest = null;
        CopilotSdkTurnRequest? secondRequest = null;
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (request, _) =>
            {
                calls++;
                if (calls == 1)
                {
                    firstRequest = request;
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
                using var transcript = System.Text.Json.JsonDocument.Parse(
                    request.TranscriptJson);
                var toolResult = transcript.RootElement
                    .GetProperty("messages")[2]
                    .GetProperty("toolResults")[0]
                    .GetProperty("result")
                    .GetString();
                return Task.FromResult(new CopilotSdkTurnResult(
                    ResponseId: "probe-response-2",
                    ModelId: "gpt-5-mini",
                    Text: $"\n{toolResult}\n",
                    ToolCalls: [],
                    InputTokens: 20,
                    OutputTokens: 3,
                    FinishReason: ChatFinishReason.Stop));
            });

        await CopilotSdkProviderProbe.RunAsync(client, _ct);

        Assert.Equal(2, calls);
        var initialRequest = Assert.IsType<CopilotSdkTurnRequest>(firstRequest);
        var request = Assert.IsType<CopilotSdkTurnRequest>(secondRequest);
        using var transcript = System.Text.Json.JsonDocument.Parse(request.TranscriptJson);
        var messages = transcript.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(["user", "assistant", "tool"], messages
            .Select(message => message.GetProperty("role").GetString()!)
            .ToArray());
        var result = messages[2]
            .GetProperty("toolResults")[0]
            .GetProperty("result")
            .GetString();
        Assert.StartsWith("foundry-probe-", result);
        Assert.DoesNotContain(result!, initialRequest.TranscriptJson);
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

    [Fact]
    public async Task RunAsync_RejectsTextAlongsideDeclarationToolCall()
    {
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (_, _) => Task.FromResult(new CopilotSdkTurnResult(
                ResponseId: "probe-response",
                ModelId: "gpt-5-mini",
                Text: "Calling the probe.",
                ToolCalls:
                [
                    new CopilotSdkToolCall(
                        "probe-call",
                        "foundry_harness_probe",
                        new Dictionary<string, object?> { ["value"] = "ready" }),
                ],
                InputTokens: 10,
                OutputTokens: 2,
                FinishReason: ChatFinishReason.ToolCalls)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CopilotSdkProviderProbe.RunAsync(client, _ct));
    }

    [Fact]
    public async Task RunAsync_RejectsRepeatedToolCallAfterExternalResult()
    {
        var calls = 0;
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (_, _) =>
            {
                calls++;
                return Task.FromResult(new CopilotSdkTurnResult(
                    ResponseId: $"probe-response-{calls}",
                    ModelId: "gpt-5-mini",
                    Text: "",
                    ToolCalls:
                    [
                        new CopilotSdkToolCall(
                            $"probe-call-{calls}",
                            "foundry_harness_probe",
                            new Dictionary<string, object?> { ["value"] = "ready" }),
                    ],
                    InputTokens: 10,
                    OutputTokens: 2,
                    FinishReason: ChatFinishReason.ToolCalls));
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CopilotSdkProviderProbe.RunAsync(client, _ct));

        Assert.Contains("repeated", exception.Message);
    }
}
