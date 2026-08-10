namespace AutonomousPhasePipelineApp.Core;

internal sealed class ConcurrentInvocationGate(
    int expectedParticipants,
    bool holdUntilCancellation,
    bool holdUntilRelease)
{
    private readonly TaskCompletionSource _allEntered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _activeCount;
    private int _enteredCount;
    private int _maximumConcurrency;

    internal Task AllEntered => _allEntered.Task;

    internal int EnteredCount => Volatile.Read(ref _enteredCount);

    internal int MaximumConcurrency => Volatile.Read(ref _maximumConcurrency);

    internal void Release() => _release.TrySetResult();

    internal async Task EnterAsync(CancellationToken cancellationToken)
    {
        int active = Interlocked.Increment(ref _activeCount);
        UpdateMaximum(active);
        int entered = Interlocked.Increment(ref _enteredCount);
        if (entered == expectedParticipants)
        {
            _allEntered.TrySetResult();
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            await _allEntered.Task.WaitAsync(linked.Token);
            if (holdUntilRelease)
            {
                await _release.Task.WaitAsync(linked.Token);
            }

            if (holdUntilCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeCount);
        }
    }

    private void UpdateMaximum(int candidate)
    {
        int observed = Volatile.Read(ref _maximumConcurrency);
        while (candidate > observed)
        {
            int prior = Interlocked.CompareExchange(
                ref _maximumConcurrency,
                candidate,
                observed);
            if (prior == observed)
            {
                return;
            }

            observed = prior;
        }
    }
}
