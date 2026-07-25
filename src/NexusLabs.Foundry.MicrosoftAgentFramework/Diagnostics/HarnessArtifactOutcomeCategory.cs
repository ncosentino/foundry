namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// The explicit outcome of one artifact offload or rehydration decision. The five members through
/// <see cref="RecoveryRequired"/> are only ever paired with
/// <see cref="HarnessArtifactOperationCategory.Offload"/>; the five members from
/// <see cref="Resolved"/> onward are only ever paired with
/// <see cref="HarnessArtifactOperationCategory.Rehydration"/>. A single unified enum keeps every
/// outcome inspectable through one categorical dimension without merging the two decisions'
/// otherwise-disjoint state machines.
/// </summary>
public enum HarnessArtifactOutcomeCategory
{
    /// <summary>The content was kept inline; no workspace write occurred.</summary>
    Inline,

    /// <summary>The content was freshly persisted to the workspace and a reference was minted.</summary>
    Offloaded,

    /// <summary>
    /// Matching content already existed at the content-addressed path; no write was required.
    /// </summary>
    ExistingReference,

    /// <summary>The decision failed closed; no reference was committed.</summary>
    Failed,

    /// <summary>
    /// The content was persisted, but the reference could not be committed before the call
    /// returned; a retry against the identical content resolves it without re-writing.
    /// </summary>
    RecoveryRequired,

    /// <summary>The referenced content resolved successfully and is available for rehydration.</summary>
    Resolved,

    /// <summary>The referenced content exists but no longer matches its recorded digest.</summary>
    Stale,

    /// <summary>No content exists at the reference's recorded workspace path.</summary>
    Missing,

    /// <summary>The reference's recorded owner does not match the current trusted execution binding.</summary>
    Unauthorized,

    /// <summary>The referenced content exists and matches its digest, but exceeds the caller's budget.</summary>
    OverBudget,
}
