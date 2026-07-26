using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministic integer size estimate for one <see cref="HarnessContextEntry"/>, used by
/// <see cref="HarnessHybridContextPolicy"/> to decide whether the configured hard limit and trigger
/// margin are reached. The unit is whatever <see cref="MeasurementUnit"/> declares — this
/// experimental policy never claims provider token exactness, and an implementation that only counts
/// bytes or characters must declare <see cref="HarnessContextMeasurementUnit.Utf8Bytes"/> rather than
/// letting a diagnostics consumer assume it is a token count.
/// </summary>
internal interface IHarnessContextSizeEstimator
{
    /// <summary>
    /// The explicit unit every value <see cref="EstimateSize"/> returns is expressed in. Required so
    /// diagnostics built from this estimator's output never mislabel a generic integer as a
    /// provider token count.
    /// </summary>
    HarnessContextMeasurementUnit MeasurementUnit { get; }

    /// <summary>
    /// Estimates the deterministic, non-negative integer size of <paramref name="entry"/>, in
    /// <see cref="MeasurementUnit"/>. Implementations must never return a negative value;
    /// <see cref="HarnessHybridContextPolicy.Evaluate"/> throws <see cref="InvalidOperationException"/>
    /// if any call returns a negative estimate.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    int EstimateSize(HarnessContextEntry entry);
}
