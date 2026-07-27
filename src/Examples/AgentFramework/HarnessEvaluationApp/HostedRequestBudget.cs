namespace HarnessEvaluationApp;

internal sealed class HostedRequestBudget(int maximumRequests)
{
    private int _requests;

    internal int Requests => Volatile.Read(ref _requests);

    internal void Consume()
    {
        var value = Interlocked.Increment(ref _requests);
        if (value > maximumRequests)
        {
            throw new InvalidOperationException(
                $"The global provider request cap of {maximumRequests} was exceeded.");
        }
    }
}
