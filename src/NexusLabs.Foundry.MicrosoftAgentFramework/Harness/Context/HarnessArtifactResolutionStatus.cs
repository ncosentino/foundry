namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit outcome of resolving a <see cref="HarnessArtifactReference"/> against the current
/// workspace (T053). Every non-<see cref="Resolved"/> value is a first-class data outcome, never a
/// swallowed null or a success-shaped substitution — matching <c>data-model.md</c>'s "Artifact
/// Reference" and "Rehydration Decision" validation: "Stale, missing, or unauthorized references
/// produce explicit evidence and do not silently inject content."
/// </summary>
/// <remarks>
/// <c>Expired</c> is intentionally not represented here yet. G4 has no retention/TTL policy and
/// therefore no authoritative evidence source for expiry; until such a policy exists, resolution can
/// only prove <see cref="Stale"/>, <see cref="Missing"/>, <see cref="Unauthorized"/>, and
/// <see cref="OverBudget"/> in addition to <see cref="Resolved"/>.
/// </remarks>
internal enum HarnessArtifactResolutionStatus
{
    /// <summary>
    /// The reference's owner matched the current trusted execution binding, its content still
    /// exists at the recorded workspace path, the recomputed digest matches the recorded digest,
    /// and the recomputed size is within the caller's required budget.
    /// </summary>
    Resolved,

    /// <summary>
    /// Content exists at the recorded workspace path, but its recomputed digest no longer matches
    /// the reference's recorded digest — the underlying content was mutated out-of-band after the
    /// reference was created.
    /// </summary>
    Stale,

    /// <summary>No content exists at the reference's recorded workspace path.</summary>
    Missing,

    /// <summary>
    /// The reference's recorded owner identity does not match the current trusted execution
    /// binding. The workspace is never read for an unauthorized reference.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The reference's content exists and matches its recorded digest, but its size exceeds the
    /// caller-supplied maximum resolvable/rehydratable UTF-8 byte budget.
    /// </summary>
    OverBudget,
}
