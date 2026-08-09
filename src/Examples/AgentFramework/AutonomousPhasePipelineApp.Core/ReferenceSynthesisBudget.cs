namespace AutonomousPhasePipelineApp.Core;

internal sealed class ReferenceSynthesisBudget
{
    private int _usedProviderCalls;

    internal ReferenceSynthesisBudget(int maxProviderCalls)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maxProviderCalls);
        MaxProviderCalls = maxProviderCalls;
    }

    internal int MaxProviderCalls { get; }

    internal int UsedProviderCalls =>
        Volatile.Read(ref _usedProviderCalls);

    internal void BeginExecution() =>
        Interlocked.Exchange(ref _usedProviderCalls, 0);

    internal void ConsumeProviderCall(
        string participant)
    {
        while (true)
        {
            int used = Volatile.Read(ref _usedProviderCalls);
            if (used >= MaxProviderCalls)
            {
                throw new InvalidOperationException(
                    $"Synthesis provider-call budget of {MaxProviderCalls} was exhausted before '{participant}' could run.");
            }

            if (Interlocked.CompareExchange(
                ref _usedProviderCalls,
                used + 1,
                used) == used)
            {
                return;
            }
        }
    }
}
