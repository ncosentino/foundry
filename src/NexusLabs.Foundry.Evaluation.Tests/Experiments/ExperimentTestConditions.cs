using Microsoft.Extensions.Time.Testing;

namespace NexusLabs.Foundry.Evaluation.Tests.Experiments;

/// <summary>
/// Shared waiting primitives for experiment runner tests, which observe runner progress through
/// counters mutated on worker and scheduler tasks the test cannot otherwise synchronize with.
/// </summary>
internal static class ExperimentTestConditions
{
    private static readonly TimeSpan ConditionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Spins until <paramref name="predicate"/> holds, failing with a diagnostic rather than
    /// spinning indefinitely when the runner never reaches the expected state.
    /// </summary>
    internal static async Task WaitUntilAsync(
        Func<bool> predicate,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ConditionTimeout;
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(
                    $"The awaited runner condition was not reached within {ConditionTimeout}.");
            }

            await Task.Yield();
        }
    }

    /// <summary>
    /// Advances <paramref name="timeProvider"/> repeatedly until <paramref name="predicate"/> holds.
    /// </summary>
    /// <remarks>
    /// The runner registers delay timers on tasks the caller cannot observe, so a single advance can
    /// land between a delay being computed and its timer being registered, leaving that timer armed
    /// relative to the already-advanced clock and due at a time the test never reaches. Advancing
    /// repeatedly re-arms whatever registered late.
    /// </remarks>
    internal static async Task AdvanceUntilAsync(
        FakeTimeProvider timeProvider,
        Func<bool> predicate,
        TimeSpan increment,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ConditionTimeout;
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(
                    $"The awaited runner condition was not reached within {ConditionTimeout} " +
                    $"while advancing the test clock in {increment} steps.");
            }

            timeProvider.Advance(increment);
            await Task.Yield();
        }
    }
}
