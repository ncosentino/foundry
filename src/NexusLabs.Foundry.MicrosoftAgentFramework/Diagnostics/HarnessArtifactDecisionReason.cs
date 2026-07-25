namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// The stable, explicit reason category behind one <see cref="HarnessArtifactOutcomeCategory"/>.
/// Every value is assigned by a deterministic mapping at the point a decision is made — never
/// derived by parsing a human-readable evidence string. The first ten members are only ever paired
/// with an offload outcome; the last five are only ever paired with a rehydration outcome.
/// </summary>
public enum HarnessArtifactDecisionReason
{
    /// <summary>The serialized content was at or under the configured inline byte threshold.</summary>
    BelowThreshold,

    /// <summary>
    /// The content was a recoverable context segment, which always inlines unconditionally,
    /// bypassing the byte threshold entirely.
    /// </summary>
    RecoverableSegmentBypass,

    /// <summary>The serialized content exceeded the configured inline byte threshold.</summary>
    ThresholdExceeded,

    /// <summary>
    /// Content already existed at the content-addressed path with a matching digest, so no write
    /// was required.
    /// </summary>
    ExistingContentMatch,

    /// <summary>
    /// No authorized workspace was available to offload to (no execution binding, no bound
    /// workspace, or no execution context accessor).
    /// </summary>
    NoAuthorizedWorkspace,

    /// <summary>Reading existing content at the content-addressed path failed.</summary>
    WorkspaceReadFailed,

    /// <summary>
    /// Existing content at the content-addressed path did not match its expected digest.
    /// </summary>
    ContentAddressMismatch,

    /// <summary>Writing fresh content to the workspace failed.</summary>
    WorkspaceWriteFailed,

    /// <summary>
    /// The request token became canceled after a successful write but before the reference could
    /// be committed.
    /// </summary>
    CanceledAfterWrite,

    /// <summary>The configured post-write checkpoint threw after a successful write.</summary>
    CheckpointFailed,

    /// <summary>The recomputed digest matched the reference's recorded digest.</summary>
    DigestVerified,

    /// <summary>The recomputed digest no longer matched the reference's recorded digest.</summary>
    DigestMismatch,

    /// <summary>No content exists at the reference's recorded workspace path.</summary>
    Missing,

    /// <summary>The reference's recorded owner did not match the current trusted execution binding.</summary>
    OwnerMismatch,

    /// <summary>The observed content size exceeded the caller-supplied maximum byte budget.</summary>
    BudgetExceeded,
}
