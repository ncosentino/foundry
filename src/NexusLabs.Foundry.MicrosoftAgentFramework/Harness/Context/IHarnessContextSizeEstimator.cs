namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministic integer size estimate for one <see cref="HarnessContextEntry"/>, used by
/// <see cref="HarnessHybridContextPolicy"/> to decide whether the configured hard limit and trigger
/// margin are reached. The unit is whatever this estimator's implementation declares — this
/// experimental policy never claims provider token exactness, and an implementation that only counts
/// bytes or characters must name itself accordingly rather than calling its result a token count.
/// </summary>
internal interface IHarnessContextSizeEstimator
{
    /// <summary>
    /// Estimates the deterministic, non-negative integer size of <paramref name="entry"/>.
    /// Implementations must never return a negative value; <see cref="HarnessHybridContextPolicy.Evaluate"/>
    /// throws <see cref="InvalidOperationException"/> if any call returns a negative estimate.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    int EstimateSize(HarnessContextEntry entry);
}
