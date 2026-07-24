using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Resolves a <see cref="HarnessArtifactReference"/> against the workspace authorized by a captured
/// <see cref="HarnessExecutionBinding"/>. This is the mechanism behind T053's explicit outcomes and
/// the sole path T052's <see cref="HarnessArtifactRehydration"/> uses to ever obtain artifact
/// content — it never injects non-<see cref="HarnessArtifactResolutionStatus.Resolved"/> content.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Per-execution binding, not ambient.</strong> One instance is bound to exactly one
/// captured <see cref="HarnessExecutionBinding"/> and one trusted session identity, mirroring
/// <c>WorkspaceAgentFileStore</c>'s construction contract. Every <see cref="Resolve"/> call first
/// revalidates that binding against the current ambient execution context
/// (<see cref="HarnessExecutionBinding.EnsureCurrent"/>) before touching the workspace.
/// </para>
/// <para>
/// <strong>Resolution order.</strong> (1) revalidate the trusted execution binding; (2) compare the
/// reference's recorded owner identity to the current binding — a mismatch returns
/// <see cref="HarnessArtifactResolutionStatus.Unauthorized"/> without ever reading the workspace;
/// (3) check existence — a missing path returns <see cref="HarnessArtifactResolutionStatus.Missing"/>;
/// (4) read content and recompute its digest — a mismatch returns
/// <see cref="HarnessArtifactResolutionStatus.Stale"/>; (5) recompute the observed UTF-8 byte size
/// against the caller-required budget — exceeding it returns
/// <see cref="HarnessArtifactResolutionStatus.OverBudget"/>; only when every check passes does this
/// return <see cref="HarnessArtifactResolutionStatus.Resolved"/> with the exact content.
/// </para>
/// <para>
/// <strong>No synthetic expiry status in G4.</strong> <c>Expired</c> is deliberately deferred until a
/// real retention policy exists. Without an authoritative TTL/retention source, resolution can only
/// prove current binding mismatch, absence, digest drift, or budget excess.
/// </para>
/// <para>
/// Uses the bound <see cref="IWorkspace"/> directly (the same primitive the eager-offload
/// write path uses per T041 Decision 4), not the <c>WorkspaceAgentFileStore</c>
/// <c>AgentFileStore</c> bridge — this mechanism is an internal Foundry-to-Foundry concern, not an
/// upstream MAF surface.
/// </para>
/// </remarks>
internal sealed class HarnessArtifactResolver
{
    private readonly HarnessExecutionBinding _executionBinding;
    private readonly IAgentExecutionContextAccessor _executionContextAccessor;
    private readonly string _sessionId;

    /// <exception cref="ArgumentNullException">
    /// <paramref name="executionBinding"/> or <paramref name="executionContextAccessor"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sessionId"/> is null/empty/whitespace, does not match
    /// <paramref name="executionBinding"/>'s captured session identity, or
    /// <paramref name="executionBinding"/> carries no authorized workspace.
    /// </exception>
    internal HarnessArtifactResolver(
        HarnessExecutionBinding executionBinding,
        IAgentExecutionContextAccessor executionContextAccessor,
        string sessionId)
    {
        ArgumentNullException.ThrowIfNull(executionBinding);
        ArgumentNullException.ThrowIfNull(executionContextAccessor);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "A trusted non-empty session identity is required.",
                nameof(sessionId));
        }

        if (!string.Equals(sessionId, executionBinding.SessionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supplied session identity must match the trusted execution binding's captured session identity.",
                nameof(sessionId));
        }

        if (executionBinding.Workspace is null)
        {
            throw new ArgumentException(
                "The execution binding must carry an authorized workspace.",
                nameof(executionBinding));
        }

        _executionBinding = executionBinding;
        _executionContextAccessor = executionContextAccessor;
        _sessionId = sessionId;
    }

    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumResolvedUtf8Bytes"/> is not greater than zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The trusted execution binding no longer matches the current ambient execution context
    /// (missing context, changed identity, changed session, or changed/missing workspace).
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal HarnessArtifactResolution Resolve(
        HarnessArtifactReference reference,
        int maximumResolvedUtf8Bytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (maximumResolvedUtf8Bytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResolvedUtf8Bytes),
                maximumResolvedUtf8Bytes,
                "The maximum resolvable byte budget must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _executionBinding.EnsureCurrent(_executionContextAccessor, _sessionId);

        if (!OwnerMatchesBinding(reference))
        {
            return HarnessArtifactResolution.Unauthorized(reference);
        }

        var workspace = _executionBinding.Workspace!;

        cancellationToken.ThrowIfCancellationRequested();
        if (!workspace.FileExists(reference.WorkspacePath))
        {
            return HarnessArtifactResolution.Missing(reference);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var readResult = workspace.TryReadFile(reference.WorkspacePath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!readResult.Success)
        {
            // Not one of the explicit G4 outcomes — a workspace that reported the file existed but
            // then failed to read it is an unexpected internal error, so it is surfaced unchanged
            // rather than silently reclassified as Missing (fail closed, never a guess).
            throw readResult.Exception!;
        }

        var observedContent = readResult.Value.Content;
        var observedDigest = HarnessArtifactIdentity.ComputeDigest(observedContent);
        var observedByteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(observedContent);

        if (!string.Equals(observedDigest, reference.ContentDigest, StringComparison.Ordinal))
        {
            return HarnessArtifactResolution.Stale(reference, observedDigest, observedByteSize);
        }

        if (observedByteSize > maximumResolvedUtf8Bytes)
        {
            return HarnessArtifactResolution.OverBudget(reference, observedByteSize, maximumResolvedUtf8Bytes);
        }

        // Revalidate once more immediately before handing content back — the binding must still be
        // current at the moment content is about to be returned to the caller, not only when
        // resolution began.
        _executionBinding.EnsureCurrent(_executionContextAccessor, _sessionId);

        return HarnessArtifactResolution.Resolved(reference, observedContent, observedByteSize, observedDigest);
    }

    private bool OwnerMatchesBinding(HarnessArtifactReference reference) =>
        string.Equals(reference.OwnerUserId, _executionBinding.UserId, StringComparison.Ordinal) &&
        string.Equals(reference.OwnerOrchestrationId, _executionBinding.OrchestrationId, StringComparison.Ordinal) &&
        string.Equals(reference.OwnerSessionId, _executionBinding.SessionId, StringComparison.Ordinal);
}
