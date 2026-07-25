namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Public, privacy-safe mirror of <c>Harness.Context.HarnessContextAssemblyOutcome</c>: the explicit
/// categorical outcome of one hybrid context compaction/assembly attempt. The two termination
/// members double as the explicit termination category carried by
/// <see cref="HarnessContextDiagnostics"/> — there is no separate, redundant termination-category
/// field.
/// </summary>
public enum HarnessContextCompactionOutcome
{
    /// <summary>
    /// The verified entries fit the hard limit without accepting a size-reducing proposal or using
    /// deterministic fallback.
    /// </summary>
    WithinLimit,

    /// <summary>
    /// Evicting recoverable rehydrated bodies and/or a verified, strictly-size-reducing reducer
    /// proposal brought the entries within the hard limit.
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
    /// over-budget history.
    /// </summary>
    Irreducible,

    /// <summary>
    /// Injected entries kept invalidating in-flight proposals until the attempt budget was exhausted
    /// before a detected version change could be consumed as a restart. Never a success.
    /// </summary>
    ConcurrentMutationLimit,
}
