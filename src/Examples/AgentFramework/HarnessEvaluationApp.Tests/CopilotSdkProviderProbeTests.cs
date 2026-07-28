using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp.Tests;

public sealed class CopilotSdkProviderProbeTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_AcceptsExpectedDeclarationToolCall()
    {
        using var client = new CopilotSdkChatClient(
            "gpt-5-mini",
            (_, _) => Task.FromResult(new CopilotSdkTurnResult(
                ResponseId: "probe-response",
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
                FinishReason: ChatFinishReason.ToolCalls)));

        await CopilotSdkProviderProbe.RunAsync(client, _ct);
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
