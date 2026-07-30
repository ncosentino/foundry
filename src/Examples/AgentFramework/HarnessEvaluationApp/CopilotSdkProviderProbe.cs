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
            "After its result, return exactly the tool result and nothing else.");
        var firstResponse = await chatClient.GetResponseAsync(
            [userMessage],
            new ChatOptions
            {
                Tools = [tool],
                MaxOutputTokens = 128,
            },
            cancellationToken).ConfigureAwait(false);
        var firstContents = firstResponse.Messages
            .SelectMany(message => message.Contents)
            .ToArray();
        var call = firstContents.Length == 1
            ? firstContents[0] as FunctionCallContent
            : null;
        if (firstResponse.Messages.Count != 1 ||
            call is null ||
            !string.Equals(call.Name, "foundry_harness_probe", StringComparison.Ordinal) ||
            !string.Equals(call.Arguments?["value"]?.ToString(), "ready", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Copilot SDK provider probe did not return the required declaration-only tool call.");
        }

        var expectedResult = $"foundry-probe-{Guid.NewGuid():N}";
        var finalResponse = await chatClient.GetResponseAsync(
            [
                userMessage,
                .. firstResponse.Messages,
                new ChatMessage(
                    ChatRole.Tool,
                    [new FunctionResultContent(call.CallId, expectedResult)]),
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
        if (repeatedCalls.Length > 0)
        {
            throw new InvalidOperationException(
                "Copilot SDK provider probe repeated the declaration-only tool call after receiving its external result.");
        }

        if (!string.Equals(
            finalResponse.Text?.Trim(),
            expectedResult,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Copilot SDK provider probe did not reproduce the external tool result across turns.");
        }
    }
}
