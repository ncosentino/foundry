using System.Diagnostics;

using Microsoft.Extensions.AI;

namespace HarnessEvaluationApp;

internal sealed class HostedTelemetryChatClient : DelegatingChatClient
{
    private readonly HostedRequestBudget _globalBudget;
    private readonly int _maximumRequests;
    private int _requestCount;

    internal HostedTelemetryChatClient(
        IChatClient innerClient,
        HostedRequestBudget globalBudget,
        int maximumRequests)
        : base(innerClient)
    {
        _globalBudget = globalBudget;
        _maximumRequests = maximumRequests;
    }

    internal int RequestCount => _requestCount;

    internal long CumulativeTokens { get; private set; }

    internal long PeakTokens { get; private set; }

    internal TimeSpan ProviderDuration { get; private set; }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestCount = Interlocked.Increment(ref _requestCount);
        if (requestCount > _maximumRequests)
        {
            throw new InvalidOperationException(
                $"The per-attempt provider request cap of {_maximumRequests} was exceeded.");
        }

        _globalBudget.Consume();
        var stopwatch = Stopwatch.StartNew();
        var response = await base
            .GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();
        ProviderDuration += stopwatch.Elapsed;
        var tokens = response.Usage?.TotalTokenCount ?? 0;
        CumulativeTokens += tokens;
        PeakTokens = Math.Max(PeakTokens, tokens);
        return response;
    }
}
