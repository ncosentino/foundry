namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>Explicit, categorical outcome of one <see cref="HarnessContextAssembler.AssembleAsync"/> call.</summary>
internal enum HarnessContextAssemblyOutcome
{
    /// <summary>
    /// The verified entries fit the hard limit without accepting a size-reducing proposal or using
    /// deterministic fallback. A reducer attempt may still have been made after the trigger margin
    /// was reached and found unnecessary or non-progressing.
    /// </summary>
    WithinLimit,

    /// <summary>
    /// Evicting recoverable rehydrated bodies and/or a verified, strictly-size-reducing reducer proposal
    /// brought the entries within the hard limit.
    /// </summary>
    Reduced,

    /// <summary>
    /// The deterministic preservation-only fallback — required entries, plus any retained optional
    /// context that still fit — reached the hard limit after the reducer failed to produce a fitting,
    /// strictly-reducing, verified proposal within the configured attempt bound.
    /// </summary>
    PreservationFallback,

    /// <summary>
    /// Required (and, where retained, optional) content alone still exceeds the hard limit even after
    /// the deterministic fallback. A distinct termination — never a silently forwarded unchanged
    /// over-budget history. This is the outcome whenever required content cannot fit or verify against a
    /// stable, successfully-observed snapshot version — including after one or more prior restarts, as
    /// long as a later version was established and evaluated on its own terms.
    /// </summary>
    Irreducible,

    /// <summary>
    /// Injected entries kept invalidating in-flight proposals until the attempt budget was exhausted
    /// before a detected version change could be consumed as a restart — the direct churn path. Reserved
    /// exclusively for that case: a successful restart onto a newer, stable snapshot that subsequently
    /// still cannot fit or verify is <see cref="Irreducible"/>, not this outcome. Never returned as
    /// success.
    /// </summary>
    ConcurrentMutationLimit,
}
