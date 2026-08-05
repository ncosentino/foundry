using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.Copilot;

var transport = new ScriptedCopilotTransport();
using var httpClient = new HttpClient(transport);
using var providerClient = new CopilotChatClient(
    new StaticTokenProvider(),
    new CopilotChatClientOptions(),
    httpClient);
using var client = new FunctionInvokingChatClient(providerClient)
{
    MaximumIterationsPerRequest = 3,
};

var response = await client.GetResponseAsync(
    [new ChatMessage(ChatRole.User, "Double 7.")],
    new ChatOptions { Tools = [new DoubleValueFunction()] });

if (!string.Equals(response.Text, "AOT_COPILOT_COMPLETE", StringComparison.Ordinal))
{
    Console.Error.WriteLine("The final Copilot response was not mapped correctly.");
    return 1;
}

if (transport.RequestCount != 2 || transport.DoubledResult != 14)
{
    Console.Error.WriteLine(
        $"The native tool loop was incomplete: requests={transport.RequestCount}, " +
        $"doubled={transport.DoubledResult}.");
    return 2;
}

Console.WriteLine("AotCopilotApp:tool-result:14");
Console.WriteLine("AotCopilotApp:completed");
return 0;

internal sealed class StaticTokenProvider : ICopilotTokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult("aot-test-token");
}

internal sealed class DoubleValueFunction : AIFunction
{
    private static readonly JsonElement Schema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "value": { "type": "integer" }
          },
          "required": ["value"]
        }
        """).RootElement.Clone();

    public override string Name => "double_value";

    public override string Description => "Doubles an integer.";

    public override JsonElement JsonSchema => Schema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var value = arguments["value"] switch
        {
            JsonElement json => json.GetInt32(),
            int integer => integer,
            var other => Convert.ToInt32(other, System.Globalization.CultureInfo.InvariantCulture),
        };

        return ValueTask.FromResult<object?>(
            new Dictionary<string, object?> { ["doubled"] = value * 2 });
    }
}

internal sealed class ScriptedCopilotTransport : HttpMessageHandler
{
    internal int RequestCount { get; private set; }

    internal int? DoubledResult { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;

        if (RequestCount == 1)
        {
            var firstBody = await request.Content!
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            using var firstRequest = JsonDocument.Parse(firstBody);
            var function = firstRequest.RootElement
                .GetProperty("tools")[0]
                .GetProperty("function");

            if (!string.Equals(
                function.GetProperty("name").GetString(),
                "double_value",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The function name was not serialized.");
            }

            if (!string.Equals(
                function
                    .GetProperty("parameters")
                    .GetProperty("properties")
                    .GetProperty("value")
                    .GetProperty("type")
                    .GetString(),
                "integer",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The function schema was not serialized.");
            }

            return JsonResponse(
                """
                {
                  "id": "aot-call",
                  "object": "chat.completion",
                  "created": 1700000000,
                  "model": "gpt-4.1",
                  "choices": [{
                    "index": 0,
                    "message": {
                      "role": "assistant",
                      "content": null,
                      "tool_calls": [{
                        "id": "call_1",
                        "type": "function",
                        "function": {
                          "name": "double_value",
                          "arguments": "{\"value\":7}"
                        }
                      }]
                    },
                    "finish_reason": "tool_calls"
                  }]
                }
                """);
        }

        var secondBody = await request.Content!
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        using var secondRequest = JsonDocument.Parse(secondBody);
        var toolMessage = secondRequest.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "tool");
        using var toolResult = JsonDocument.Parse(
            toolMessage.GetProperty("content").GetString()!);
        DoubledResult = toolResult.RootElement.GetProperty("doubled").GetInt32();

        return JsonResponse(
            """
            {
              "id": "aot-final",
              "object": "chat.completion",
              "created": 1700000001,
              "model": "gpt-4.1",
              "choices": [{
                "index": 0,
                "message": {
                  "role": "assistant",
                  "content": "AOT_COPILOT_COMPLETE"
                },
                "finish_reason": "stop"
              }]
            }
            """);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}
