using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed class CopilotSdkChatClient : IChatClient
{
    private readonly string _modelId;
    private readonly Func<
        CopilotSdkTurnRequest,
        CancellationToken,
        Task<CopilotSdkTurnResult>> _executeTurn;

    internal CopilotSdkChatClient(
        string modelId,
        Func<
            CopilotSdkTurnRequest,
            CancellationToken,
            Task<CopilotSdkTurnResult>> executeTurn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(executeTurn);
        _modelId = modelId;
        _executeTurn = executeTurn;
    }

    public ChatClientMetadata Metadata => new("github-copilot-sdk");

    public object? GetService(Type serviceType, object? key = null) =>
        serviceType == typeof(IChatClient)
            ? this
            : null;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToArray();
        var tools = options?.Tools?
            .OfType<AIFunction>()
            .ToArray() ?? [];
        var result = await _executeTurn(
            new CopilotSdkTurnRequest(
                options?.ModelId ?? _modelId,
                BuildTranscriptJson(materialized),
                tools,
                options?.MaxOutputTokens ?? 2000),
            cancellationToken).ConfigureAwait(false);
        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(result.Text))
        {
            contents.Add(new TextContent(result.Text));
        }
        contents.AddRange(result.ToolCalls.Select(call =>
            (AIContent)new FunctionCallContent(
                call.CallId,
                call.Name,
                new Dictionary<string, object?>(call.Arguments))));
        var response = new ChatResponse(
            new ChatMessage(ChatRole.Assistant, contents))
        {
            ResponseId = result.ResponseId,
            ModelId = result.ModelId,
            FinishReason = result.FinishReason,
            Usage = new UsageDetails
            {
                InputTokenCount = result.InputTokens,
                OutputTokenCount = result.OutputTokens,
                TotalTokenCount = result.InputTokens + result.OutputTokens,
            },
        };
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Contents)
            {
                ResponseId = response.ResponseId,
                ModelId = response.ModelId,
                FinishReason = response.FinishReason,
            };
        }
    }

    public void Dispose()
    {
    }

    internal static string BuildTranscriptJson(IReadOnlyList<ChatMessage> messages)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("messages");
            foreach (var message in messages)
            {
                writer.WriteStartObject();
                writer.WriteString("role", message.Role.Value);
                if (!string.IsNullOrEmpty(message.Text))
                {
                    writer.WriteString("text", message.Text);
                }
                var toolCalls = message.Contents.OfType<FunctionCallContent>().ToArray();
                if (toolCalls.Length > 0)
                {
                    writer.WriteStartArray("toolCalls");
                    foreach (var call in toolCalls)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("callId", call.CallId);
                        writer.WriteString("name", call.Name);
                        writer.WritePropertyName("arguments");
                        JsonSerializer.Serialize(writer, call.Arguments);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }

                var toolResults = message.Contents.OfType<FunctionResultContent>().ToArray();
                if (toolResults.Length > 0)
                {
                    writer.WriteStartArray("toolResults");
                    foreach (var result in toolResults)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("callId", result.CallId);
                        writer.WritePropertyName("result");
                        if (result.Result is null)
                        {
                            writer.WriteNullValue();
                        }
                        else
                        {
                            JsonSerializer.Serialize(
                                writer,
                                result.Result,
                                result.Result.GetType());
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
