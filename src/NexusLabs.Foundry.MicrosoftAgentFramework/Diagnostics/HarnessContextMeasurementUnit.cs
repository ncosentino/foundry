namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// The explicit unit a <c>Harness.Context.IHarnessContextSizeEstimator</c> reports its integer size
/// estimates in. Every size, threshold, and limit carried by <see cref="HarnessContextDiagnostics"/>
/// is expressed in whatever unit the estimator that produced it declares — this enum exists so a
/// generic integer is never silently mislabeled as a provider token count.
/// </summary>
public enum HarnessContextMeasurementUnit
{
    /// <summary>
    /// The estimator counts UTF-8 bytes of the model-facing content it measures. Never a provider
    /// token estimate, even though byte counts and token counts are sometimes loosely correlated.
    /// </summary>
    Utf8Bytes,

    /// <summary>
    /// The estimator reports a provider- or tokenizer-derived token estimate. Reserved for a future
    /// estimator that is actually backed by a tokenizer; no estimator in this codebase reports this
    /// unit today.
    /// </summary>
    EstimatedTokens,

    /// <summary>
    /// The estimator reports host- or test-defined arbitrary units with no byte or token meaning at
    /// all — for example a fixed or constant fixture size used to make trigger-threshold arithmetic
    /// deterministic in tests.
    /// </summary>
    HostDefinedUnits,
}
