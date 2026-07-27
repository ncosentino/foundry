namespace HarnessEvaluationApp;

internal sealed class HostedRequestBudget(
    int maximumRequests,
    TimeSpan minimumInterval)
{
    private int _requests;
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

    internal int Requests => Volatile.Read(ref _requests);

    internal async ValueTask ConsumeAsync(CancellationToken cancellationToken)
    {
        await _rateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var delay = _nextRequestAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            var current = Volatile.Read(ref _requests);
            if (current >= maximumRequests)
            {
                throw new InvalidOperationException(
                    $"The global provider request cap of {maximumRequests} was exceeded.");
            }

            Interlocked.Increment(ref _requests);
            _nextRequestAt = DateTimeOffset.UtcNow + minimumInterval;
        }
        finally
        {
            _rateGate.Release();
        }
    }
}
