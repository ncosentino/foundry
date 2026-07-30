using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed partial class HostedScriptedChatClient : IChatClient
{
    public void Dispose()
    {
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToArray();
        var caseId = ResolveCaseId(materialized);
        var hasToolResult = materialized
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Any();
        ChatResponse response = hasToolResult
            ? new ChatResponse(new ChatMessage(ChatRole.Assistant, $"completed:{caseId}"))
            : new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    HostedCaseCatalog.Get(caseId).RequiredTools
                        .Select((tool, index) =>
                            (AIContent)new FunctionCallContent(
                                $"{caseId}-call-{index + 1}",
                                tool,
                                new Dictionary<string, object?>()))
                        .ToArray()));
        response.ModelId = "scripted-hosted-evaluation";
        response.Usage = new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 20,
            TotalTokenCount = 120,
        };
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("The scripted hosted evaluation client does not support streaming.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    private static string ResolveCaseId(IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            var match = CaseIdPattern().Match(message.Text ?? string.Empty);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        throw new InvalidOperationException("The scripted request did not contain a hosted case ID.");
    }

    [GeneratedRegex(@"\[CASE_ID:(h001-\d{2})\]", RegexOptions.CultureInvariant)]
    private static partial Regex CaseIdPattern();
}
