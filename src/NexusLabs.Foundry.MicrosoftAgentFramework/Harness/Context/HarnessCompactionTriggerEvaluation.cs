namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, immutable outcome of evaluating one context size estimate against a
/// <see cref="HarnessHybridContextPolicy"/>'s configured hard limit and trigger margin.
/// </summary>
internal sealed record HarnessCompactionTriggerEvaluation
{
    private HarnessCompactionTriggerEvaluation(
        int estimatedSize,
        int hardLimit,
        int triggerMargin,
        int triggerThreshold,
        bool triggered)
    {
        EstimatedSize = estimatedSize;
        HardLimit = hardLimit;
        TriggerMargin = triggerMargin;
        TriggerThreshold = triggerThreshold;
        Triggered = triggered;
    }

    /// <summary>The total estimated size the evaluated entries measured, in the configured estimator's units.</summary>
    internal int EstimatedSize { get; }

    /// <summary>The configured hard limit this evaluation was measured against.</summary>
    internal int HardLimit { get; }

    /// <summary>The configured compaction execution safety margin.</summary>
    internal int TriggerMargin { get; }

    /// <summary><see cref="HardLimit"/> minus <see cref="TriggerMargin"/>.</summary>
    internal int TriggerThreshold { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="EstimatedSize"/> is at or above <see cref="TriggerThreshold"/>.
    /// Exactly at the threshold requests compaction; strictly below it never does.
    /// </summary>
    internal bool Triggered { get; }

    internal static HarnessCompactionTriggerEvaluation Create(
        int estimatedSize, int hardLimit, int triggerMargin, bool triggered) =>
        new(estimatedSize, hardLimit, triggerMargin, hardLimit - triggerMargin, triggered);
}
