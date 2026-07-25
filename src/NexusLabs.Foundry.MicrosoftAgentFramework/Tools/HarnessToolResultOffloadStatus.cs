namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// Explicit outcome of one <see cref="HarnessToolResultOffloadTransform.Transform"/> decision,
/// mirroring the "Tool-Result Offload Decision" fields
/// (<c>Decision: inline, offload, existing artifact reference, fail</c>) plus the explicit
/// artifact-written/reference-not-committed recovery state.
/// </summary>
internal enum HarnessToolResultOffloadStatus
{
    /// <summary>
    /// The serialized tool result is at or under the configured
    /// <see cref="HarnessToolResultOffloadPolicy.MaximumInlineToolResultBytes"/> threshold (or the
    /// raw result was a <see cref="Harness.Context.HarnessArtifactRecoverableContextSegment"/>,
    /// which always inlines regardless of size). No workspace write occurred.
    /// </summary>
    Inline,

    /// <summary>
    /// The serialized tool result exceeded the threshold, no artifact previously existed at its
    /// content-addressed path, and a fresh write succeeded. The reference was constructed and any
    /// configured checkpoint ran without throwing.
    /// </summary>
    Offloaded,

    /// <summary>
    /// The serialized tool result exceeded the threshold, but content already existed at its
    /// content-addressed path with a matching digest — no write was required; the existing
    /// artifact's reference was reconstructed instead.
    /// </summary>
    ExistingReference,

    /// <summary>
    /// The serialized tool result exceeded the threshold with no authorized workspace available
    /// (fail-closed, never inlined/truncated), the trusted execution binding was no longer current,
    /// existing content at the content-addressed path did not match its expected digest (refused as
    /// possible corruption), or the workspace write itself failed. No reference was committed.
    /// </summary>
    Failed,

    /// <summary>
    /// The artifact was successfully persisted to its content-addressed path, but either the
    /// request token became canceled during the write or the post-write checkpoint threw before
    /// the reference could be constructed and returned to the caller — the
    /// "artifact-written/reference-not-committed" recovery window. The outcome carries bounded
    /// path/digest retry metadata via
    /// <see cref="HarnessToolResultOffloadOutcome.RecoveryWorkspacePath"/> and
    /// <see cref="HarnessToolResultOffloadOutcome.RecoveryContentDigest"/>; retrying the same
    /// tool result is always safe because the underlying write is idempotent and content-addressed.
    /// </summary>
    RecoveryRequired,
}
