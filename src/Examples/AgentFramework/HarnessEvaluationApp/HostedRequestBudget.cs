namespace HarnessEvaluationApp;

internal sealed class HostedRequestBudget(int maximumRequests)
{
    private int _requests;

    internal int Requests => Volatile.Read(ref _requests);

    internal void Consume()
    {
        while (true)
        {
            var current = Volatile.Read(ref _requests);
            if (current >= maximumRequests)
            {
                throw new InvalidOperationException(
                    $"The global provider request cap of {maximumRequests} was exceeded.");
            }

            if (Interlocked.CompareExchange(ref _requests, current + 1, current) == current)
            {
                return;
            }
        }
    }
}
