namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// Optional internal deterministic checkpoint seam invoked by
/// <see cref="HarnessToolResultOffloadTransform"/> after an artifact write succeeds but
/// <em>before</em> its <see cref="Harness.Context.HarnessArtifactReference"/> is constructed —
/// exactly the "artifact-written/reference-not-committed" recovery window (Decision 4,
/// Cancellation point 3).
/// </summary>
/// <param name="workspacePath">
/// The content-addressed workspace path at which the artifact was just written.
/// </param>
/// <param name="contentDigest">
/// The SHA-256 hex digest identifying the artifact that was written.
/// </param>
/// <param name="cancellationToken">
/// The active cancellation token for the enclosing transform call. Implementations may call
/// <see cref="CancellationToken.ThrowIfCancellationRequested"/> to simulate/observe cancellation
/// in this exact window.
/// </param>
/// <remarks>
/// A production caller ordinarily supplies <see langword="null"/> for this seam — it exists so
/// tests can deterministically force the reference-not-committed recovery state without any real
/// timing race. Any exception (including <see cref="OperationCanceledException"/>) thrown by this
/// delegate causes the transform to return a
/// <see cref="HarnessToolResultOffloadStatus.RecoveryRequired"/> outcome instead of
/// <see cref="HarnessToolResultOffloadStatus.Offloaded"/>. No reference is constructed when
/// recovery is required — bounded path/digest retry metadata is carried in the outcome instead.
/// </remarks>
internal delegate void HarnessToolResultOffloadCheckpoint(
    string workspacePath,
    string contentDigest,
    CancellationToken cancellationToken);
