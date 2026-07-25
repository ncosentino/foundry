using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tools;

/// <summary>
/// The single shared, caller-agnostic eager tool-result offload decision transform, used
/// identically by <c>IterativeAgentLoop</c> and selected-provider
/// <c>HarnessProviderComposition</c>'s FICC <c>FunctionInvoker</c>. Implements a byte-threshold
/// (never token-estimated) inline/offload decision, content-addressed fail-closed writes, and an
/// explicit recovery state for the artifact-written/reference-not-committed window.
/// </summary>
internal static class HarnessToolResultOffloadTransform
{
    /// <summary>
    /// Maximum UTF-8 character length of a tool name or call ID copied into a
    /// <see cref="HarnessToolResultOffloadOutcome.Evidence"/> string. Values longer than this are
    /// truncated so that evidence strings remain bounded regardless of caller-supplied input.
    /// </summary>
    private const int MaximumEvidenceIdentifierLength = 64;

    /// <summary>
    /// Decides whether <paramref name="request"/>'s raw tool result should be inlined or offloaded,
    /// performing at most one content-addressed workspace write.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="HarnessToolResultOffloadRequest.CancellationToken"/> was canceled before any
    /// workspace access was attempted. No side effect (no write) occurs in this case.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="HarnessToolResultOffloadRequest.ExecutionBinding"/> no longer matches the current
    /// ambient execution context at revalidation time (mirrors every other Harness binding
    /// revalidation call site — never silently reclassified as a <c>Failed</c> outcome).
    /// </exception>
    internal static HarnessToolResultOffloadOutcome Transform(HarnessToolResultOffloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.CancellationToken.ThrowIfCancellationRequested();

        // A rehydrated recoverable context segment always bypasses the byte-threshold check and is
        // never re-offloaded within the active request, per HarnessArtifactRecoverableContextSegment's
        // SkipEagerOffload invariant.
        if (request.RawResult is HarnessArtifactRecoverableContextSegment segment)
        {
            // Forward only the recovered body so FICC does not serialize the segment metadata.
            return HarnessToolResultOffloadOutcome.Inline(segment.Body, segment.Body);
        }

        var serializedText = ToolResultSerializer.Serialize(request.RawResult);
        var observedByteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(serializedText);

        // Exactly-at-threshold inlines; only strictly-over-threshold offloads.
        if (observedByteSize <= request.Policy.MaximumInlineToolResultBytes)
        {
            return HarnessToolResultOffloadOutcome.Inline(request.RawResult, serializedText);
        }

        return OffloadOversized(request, serializedText, observedByteSize);
    }

    private static HarnessToolResultOffloadOutcome OffloadOversized(
        HarnessToolResultOffloadRequest request,
        string serializedText,
        int observedByteSize)
    {
        var toolId = BoundedEvidenceId(request.ToolName);
        var callId = BoundedEvidenceId(request.CallId);
        var threshold = request.Policy.MaximumInlineToolResultBytes;

        var binding = request.ExecutionBinding;
        if (binding is null)
        {
            return HarnessToolResultOffloadOutcome.Failed(
                $"NoAuthorizedWorkspace: NoBinding; " +
                $"tool='{toolId}' callId='{callId}' bytes={observedByteSize} threshold={threshold}.");
        }

        if (binding.Workspace is null)
        {
            return HarnessToolResultOffloadOutcome.Failed(
                $"NoAuthorizedWorkspace: NoWorkspace; " +
                $"tool='{toolId}' callId='{callId}' bytes={observedByteSize} threshold={threshold}.");
        }

        var accessor = request.ExecutionContextAccessor;
        if (accessor is null)
        {
            return HarnessToolResultOffloadOutcome.Failed(
                $"NoAuthorizedWorkspace: NoContextAccessor; " +
                $"tool='{toolId}' callId='{callId}' bytes={observedByteSize} threshold={threshold}.");
        }

        binding.EnsureCurrent(accessor, request.Policy.OffloadSessionId);

        var workspace = binding.Workspace;
        var digest = HarnessArtifactIdentity.ComputeDigest(serializedText);
        var path = HarnessArtifactIdentity.BuildPath(digest);

        request.CancellationToken.ThrowIfCancellationRequested();

        if (workspace.FileExists(path))
        {
            return ResolveExistingPath(
                request,
                binding,
                path,
                digest,
                observedByteSize);
        }

        return WriteFresh(
            request,
            binding,
            serializedText,
            path,
            digest);
    }

    private static HarnessToolResultOffloadOutcome ResolveExistingPath(
        HarnessToolResultOffloadRequest request,
        HarnessExecutionBinding binding,
        string path,
        string expectedDigest,
        int observedByteSize)
    {
        var readResult = binding.Workspace!.TryReadFile(path);
        request.CancellationToken.ThrowIfCancellationRequested();

        if (!readResult.Success)
        {
            return HarnessToolResultOffloadOutcome.Failed(
                $"WorkspaceReadFailed: reading existing artifact path '{path}' failed with " +
                $"'{readResult.Exception?.GetType().Name}'; no reference committed, existing content left untouched.");
        }

        var existingDigest = HarnessArtifactIdentity.ComputeDigest(readResult.Value.Content);
        if (!string.Equals(existingDigest, expectedDigest, StringComparison.Ordinal))
        {
            return HarnessToolResultOffloadOutcome.Failed(
                $"ContentAddressMismatch: existing content at path '{path}' has digest " +
                $"'{existingDigest}' but expected '{expectedDigest}'; possible corruption — the existing " +
                "content is never overwritten.");
        }

        var description = BoundedDescription(request);
        var reference = HarnessArtifactReference.Reconstruct(
            path,
            expectedDigest,
            observedByteSize,
            description,
            binding.UserId,
            binding.OrchestrationId,
            binding.SessionId,
            request.ToolName,
            request.CallId,
            request.CreatedAtUtc);

        return HarnessToolResultOffloadOutcome.ExistingReference(reference);
    }

    private static HarnessToolResultOffloadOutcome WriteFresh(
        HarnessToolResultOffloadRequest request,
        HarnessExecutionBinding binding,
        string serializedText,
        string path,
        string digest)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        var writeResult = binding.Workspace!.TryWriteFile(path, serializedText);
        if (!writeResult.Success)
        {
            return HarnessToolResultOffloadOutcome.Failed(
                $"WorkspaceWriteFailed: writing artifact path '{path}' (digest '{digest}') failed with " +
                $"'{writeResult.Exception?.GetType().Name}'; no reference committed.");
        }

        // The write succeeded, so its side effect has already occurred. If the token became
        // canceled during the write, do not throw — throw would hide the write. Return
        // RecoveryRequired with bounded path/digest so a retry with a non-canceled token can
        // resolve the existing artifact via ExistingReference without re-writing.
        if (request.CancellationToken.IsCancellationRequested)
        {
            return HarnessToolResultOffloadOutcome.RecoveryRequired(
                $"CanceledAfterWrite: the artifact at path '{path}' (digest '{digest}') was written " +
                "successfully but the request token became canceled before the reference could be " +
                "committed; retrying with a non-canceled token will resolve the existing artifact.",
                path,
                digest);
        }

        // Run the deterministic checkpoint seam in the pre-reference window: after write
        // success, before the reference is constructed. Any exception means the reference
        // was never committed; bounded path/digest retry metadata is returned instead.
        var checkpoint = request.Policy.Checkpoint;
        if (checkpoint is not null)
        {
            try
            {
                checkpoint(path, digest, request.CancellationToken);
            }
            catch (Exception ex)
            {
                return HarnessToolResultOffloadOutcome.RecoveryRequired(
                    $"CheckpointFailed: the artifact at path '{path}' " +
                    $"(digest '{digest}') was written successfully, but the post-write " +
                    $"checkpoint failed with '{ex.GetType().Name}' before its reference was committed; " +
                    "retrying will resolve the same artifact without re-writing.",
                    path,
                    digest);
            }
        }

        // Both post-write checks passed — now construct the reference and return Offloaded.
        var description = BoundedDescription(request);
        var reference = HarnessArtifactReference.Create(
            binding,
            serializedText,
            description,
            request.ToolName,
            request.CallId,
            request.CreatedAtUtc);

        return HarnessToolResultOffloadOutcome.Offloaded(reference);
    }

    private static string BoundedDescription(HarnessToolResultOffloadRequest request)
    {
        var description = request.Policy.DescriptionStrategy(request.ToolName, request.CallId);
        return description.Length > HarnessArtifactReference.MaximumDescriptionLength
            ? description[..HarnessArtifactReference.MaximumDescriptionLength]
            : description;
    }

    private static string BoundedEvidenceId(string id) =>
        id.Length > MaximumEvidenceIdentifierLength
            ? id[..MaximumEvidenceIdentifierLength]
            : id;
}
