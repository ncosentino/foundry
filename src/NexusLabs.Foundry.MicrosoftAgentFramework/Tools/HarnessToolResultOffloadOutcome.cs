using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// Immutable result union produced by <see cref="HarnessToolResultOffloadTransform.Transform"/>:
/// exactly one of <see cref="HarnessToolResultOffloadStatus.Inline"/>,
/// <see cref="HarnessToolResultOffloadStatus.Offloaded"/>,
/// <see cref="HarnessToolResultOffloadStatus.ExistingReference"/>,
/// <see cref="HarnessToolResultOffloadStatus.Failed"/>, or
/// <see cref="HarnessToolResultOffloadStatus.RecoveryRequired"/>. No permissive public constructor
/// — every instance is created through one of the static factories below, each of which populates
/// only the members meaningful for that outcome and builds the matching
/// <see cref="HarnessArtifactDiagnostics"/> snapshot for that decision.
/// </summary>
internal sealed record HarnessToolResultOffloadOutcome
{
    private HarnessToolResultOffloadOutcome(
        HarnessToolResultOffloadStatus status,
        object? rawResult,
        string? inlineText,
        HarnessArtifactReference? reference,
        string? evidence,
        string? recoveryWorkspacePath,
        string? recoveryContentDigest,
        HarnessArtifactDiagnostics diagnostics)
    {
        Status = status;
        RawResult = rawResult;
        InlineText = inlineText;
        Reference = reference;
        Evidence = evidence;
        RecoveryWorkspacePath = recoveryWorkspacePath;
        RecoveryContentDigest = recoveryContentDigest;
        Diagnostics = diagnostics;
    }

    /// <summary>Which of the five explicit outcomes this instance represents.</summary>
    internal HarnessToolResultOffloadStatus Status { get; }

    /// <summary>
    /// The original, unmodified raw tool result. Populated only for
    /// <see cref="HarnessToolResultOffloadStatus.Inline"/>, so a caller can pass the untouched
    /// value straight through (e.g. selected-provider FICC passthrough).
    /// </summary>
    internal object? RawResult { get; }

    /// <summary>
    /// The already-serialized text for an <see cref="HarnessToolResultOffloadStatus.Inline"/>
    /// outcome (the exact string <see cref="ToolResultSerializer.Serialize"/> produced — never
    /// re-serialized by a caller).
    /// </summary>
    internal string? InlineText { get; }

    /// <summary>
    /// The artifact reference for <see cref="HarnessToolResultOffloadStatus.Offloaded"/> and
    /// <see cref="HarnessToolResultOffloadStatus.ExistingReference"/> outcomes. Always
    /// <see langword="null"/> for <see cref="HarnessToolResultOffloadStatus.Inline"/>,
    /// <see cref="HarnessToolResultOffloadStatus.Failed"/>, and
    /// <see cref="HarnessToolResultOffloadStatus.RecoveryRequired"/> — for recovery outcomes use
    /// <see cref="RecoveryWorkspacePath"/> and <see cref="RecoveryContentDigest"/> instead.
    /// </summary>
    internal HarnessArtifactReference? Reference { get; }

    /// <summary>
    /// The bounded, model/history-facing reference identity for
    /// <see cref="HarnessToolResultOffloadStatus.Offloaded"/> and
    /// <see cref="HarnessToolResultOffloadStatus.ExistingReference"/> outcomes. Never the raw
    /// content — always <see cref="HarnessArtifactReference.ReferenceId"/>.
    /// </summary>
    internal string? ReferenceText => Reference?.ReferenceId;

    /// <summary>
    /// Bounded, explicit evidence describing why an outcome is
    /// <see cref="HarnessToolResultOffloadStatus.Failed"/> or
    /// <see cref="HarnessToolResultOffloadStatus.RecoveryRequired"/>. Always non-null for those two
    /// statuses; always <see langword="null"/> otherwise. Never contains raw/oversized tool result
    /// content — only digests, paths, byte counts, and exception type names.
    /// </summary>
    internal string? Evidence { get; }

    /// <summary>
    /// The content-addressed workspace path of the successfully-written artifact for a
    /// <see cref="HarnessToolResultOffloadStatus.RecoveryRequired"/> outcome. Carries bounded retry
    /// metadata without constructing a reference — a subsequent transform call for the identical
    /// content will observe the already-written artifact and return
    /// <see cref="HarnessToolResultOffloadStatus.ExistingReference"/>. Always
    /// <see langword="null"/> for all other statuses.
    /// </summary>
    internal string? RecoveryWorkspacePath { get; }

    /// <summary>
    /// The SHA-256 hex digest of the successfully-written artifact for a
    /// <see cref="HarnessToolResultOffloadStatus.RecoveryRequired"/> outcome. Carries bounded
    /// retry metadata without constructing a reference. Always <see langword="null"/> for all
    /// other statuses.
    /// </summary>
    internal string? RecoveryContentDigest { get; }

    /// <summary>
    /// The privacy-safe, structured evidence for this decision. The identical instance is also
    /// attached to the <see cref="HarnessArtifactOffloadDecisionEvent"/> emitted for this decision.
    /// </summary>
    internal HarnessArtifactDiagnostics Diagnostics { get; }

    /// <summary>Small (at-or-under-threshold) result: the original value is used unchanged.</summary>
    internal static HarnessToolResultOffloadOutcome Inline(
        object? rawResult,
        string serializedText,
        HarnessArtifactContentCategory content,
        HarnessArtifactDecisionReason reason,
        int observedUtf8ByteSize,
        int configuredThresholdBytes)
    {
        ArgumentNullException.ThrowIfNull(serializedText);
        return new HarnessToolResultOffloadOutcome(
            HarnessToolResultOffloadStatus.Inline,
            rawResult,
            serializedText,
            reference: null,
            evidence: null,
            recoveryWorkspacePath: null,
            recoveryContentDigest: null,
            HarnessArtifactDiagnostics.ForOffload(
                HarnessArtifactOutcomeCategory.Inline,
                content,
                reason,
                observedUtf8ByteSize,
                configuredThresholdBytes,
                referenceId: null));
    }

    /// <summary>Oversized result freshly persisted to the workspace this call.</summary>
    internal static HarnessToolResultOffloadOutcome Offloaded(
        HarnessArtifactReference reference,
        int observedUtf8ByteSize,
        int configuredThresholdBytes)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new HarnessToolResultOffloadOutcome(
            HarnessToolResultOffloadStatus.Offloaded,
            rawResult: null,
            inlineText: null,
            reference,
            evidence: null,
            recoveryWorkspacePath: null,
            recoveryContentDigest: null,
            HarnessArtifactDiagnostics.ForOffload(
                HarnessArtifactOutcomeCategory.Offloaded,
                HarnessArtifactContentCategory.ToolResult,
                HarnessArtifactDecisionReason.ThresholdExceeded,
                observedUtf8ByteSize,
                configuredThresholdBytes,
                reference.ReferenceId));
    }

    /// <summary>
    /// Oversized result whose content-addressed path already held matching content — no write
    /// performed this call.
    /// </summary>
    internal static HarnessToolResultOffloadOutcome ExistingReference(
        HarnessArtifactReference reference,
        int observedUtf8ByteSize,
        int configuredThresholdBytes)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new HarnessToolResultOffloadOutcome(
            HarnessToolResultOffloadStatus.ExistingReference,
            rawResult: null,
            inlineText: null,
            reference,
            evidence: null,
            recoveryWorkspacePath: null,
            recoveryContentDigest: null,
            HarnessArtifactDiagnostics.ForOffload(
                HarnessArtifactOutcomeCategory.ExistingReference,
                HarnessArtifactContentCategory.ToolResult,
                HarnessArtifactDecisionReason.ExistingContentMatch,
                observedUtf8ByteSize,
                configuredThresholdBytes,
                reference.ReferenceId));
    }

    /// <summary>
    /// Fail-closed outcome: no reference was committed and nothing was inlined/truncated/discarded.
    /// </summary>
    internal static HarnessToolResultOffloadOutcome Failed(
        string evidence,
        HarnessArtifactDecisionReason reason,
        int observedUtf8ByteSize,
        int configuredThresholdBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        return new HarnessToolResultOffloadOutcome(
            HarnessToolResultOffloadStatus.Failed,
            rawResult: null,
            inlineText: null,
            reference: null,
            evidence,
            recoveryWorkspacePath: null,
            recoveryContentDigest: null,
            HarnessArtifactDiagnostics.ForOffload(
                HarnessArtifactOutcomeCategory.Failed,
                HarnessArtifactContentCategory.ToolResult,
                reason,
                observedUtf8ByteSize,
                configuredThresholdBytes,
                referenceId: null));
    }

    /// <summary>
    /// The artifact was successfully written, but either the request token became canceled during
    /// the write or the post-write checkpoint seam threw before the reference could be constructed
    /// and returned to the caller — the "artifact-written/reference-not-committed" window.
    /// <paramref name="workspacePath"/> and <paramref name="contentDigest"/> are retained as
    /// bounded, structured retry metadata so callers do not need to parse prose evidence. A
    /// subsequent transform call for the identical content will observe the already-written
    /// artifact and return <see cref="HarnessToolResultOffloadStatus.ExistingReference"/> without
    /// re-writing or re-invoking the checkpoint.
    /// </summary>
    internal static HarnessToolResultOffloadOutcome RecoveryRequired(
        string evidence,
        string workspacePath,
        string contentDigest,
        HarnessArtifactDecisionReason reason,
        int observedUtf8ByteSize,
        int configuredThresholdBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDigest);
        return new HarnessToolResultOffloadOutcome(
            HarnessToolResultOffloadStatus.RecoveryRequired,
            rawResult: null,
            inlineText: null,
            reference: null,
            evidence,
            recoveryWorkspacePath: workspacePath,
            recoveryContentDigest: contentDigest,
            HarnessArtifactDiagnostics.ForOffload(
                HarnessArtifactOutcomeCategory.RecoveryRequired,
                HarnessArtifactContentCategory.ToolResult,
                reason,
                observedUtf8ByteSize,
                configuredThresholdBytes,
                referenceId: null));
    }
}
