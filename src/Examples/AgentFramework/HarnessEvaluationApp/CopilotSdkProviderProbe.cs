using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal static class CopilotSdkProviderProbe
{
    internal static async Task RunAsync(
        IChatClient chatClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        var tool = AIFunctionFactory.Create(
            ([System.ComponentModel.Description("Probe value")] string value) => value,
            new AIFunctionFactoryOptions
            {
                Name = "foundry_harness_probe",
                Description = "Verifies declaration-only tool calling through the Copilot SDK.",
            });
        var userMessage = new ChatMessage(
            ChatRole.User,
            "Call foundry_harness_probe exactly once with value ready. " +
            "After its result, return exactly probe-complete.");
        var firstResponse = await chatClient.GetResponseAsync(
            [userMessage],
            new ChatOptions
            {
                Tools = [tool],
                MaxOutputTokens = 128,
            },
            cancellationToken).ConfigureAwait(false);
        var calls = firstResponse.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .ToArray();
        if (calls.Length != 1 ||
            !string.Equals(calls[0].Name, "foundry_harness_probe", StringComparison.Ordinal) ||
            !string.Equals(calls[0].Arguments?["value"]?.ToString(), "ready", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Copilot SDK provider probe did not return the required declaration-only tool call.");
        }

        var finalResponse = await chatClient.GetResponseAsync(
            [
                userMessage,
                .. firstResponse.Messages,
                new ChatMessage(
                    ChatRole.Tool,
                    [new FunctionResultContent(calls[0].CallId, "probe-complete")]),
            ],
            new ChatOptions
            {
                Tools = [tool],
                MaxOutputTokens = 128,
            },
            cancellationToken).ConfigureAwait(false);
        var repeatedCalls = finalResponse.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .ToArray();
        if (repeatedCalls.Length > 0 ||
            !string.Equals(finalResponse.Text, "probe-complete", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Copilot SDK provider probe did not preserve the tool result across turns.");
        }
    }
}
