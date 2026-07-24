using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

using WorkspacePathUtil = NexusLabs.Foundry.MicrosoftAgentFramework.Workspace.WorkspacePath;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Immutable, digest-backed record describing one G4 workspace artifact reference (T050) — a
/// bounded conversational handle to bulk content already persisted to the Foundry workspace,
/// matching <c>data-model.md</c>'s "Artifact Reference" entity and the concrete field set settled
/// by <c>specs/001-maf-harness-first-class/evidence/harness-lifecycle-feasibility.md</c> (T041,
/// "Recorded metadata").
/// </summary>
/// <remarks>
/// <para>
/// <strong>No permissive public constructor.</strong> Every instance is created through
/// <see cref="Create"/> (from freshly-serialized content, guaranteeing digest/path consistency by
/// construction) or <see cref="Reconstruct"/> (from recorded/serialized fields, e.g. a reference an
/// upstream model turn echoed back). Both factories independently validate every required field and
/// reject a reference whose <see cref="WorkspacePath"/> does not match its <see cref="ContentDigest"/>.
/// Serialized reference data is always untrusted input and must be revalidated through
/// <see cref="Reconstruct"/> — never assembled directly.
/// </para>
/// <para>
/// <strong>Fresh references mint ownership only from the trusted execution binding.</strong>
/// <see cref="Create"/> accepts a captured <see cref="HarnessExecutionBinding"/> and extracts
/// <see cref="OwnerUserId"/>, <see cref="OwnerOrchestrationId"/>, and
/// <see cref="OwnerSessionId"/> from it directly — never from model input or restored session
/// state. <see cref="Reconstruct"/> still accepts recorded owner fields because it is the
/// untrusted deserialize/echo path. These fields are audit identity, not an authorization
/// decision by themselves (T020's "UserId is an audit identity" note);
/// <see cref="HarnessArtifactResolver"/> is what actually re-derives and compares ownership
/// against the <em>current</em> trusted binding at resolution time.
/// </para>
/// <para>
/// <strong>Liveness is never cached here.</strong> This record has no mutable/self-reported
/// staleness field. Whether a reference is still <c>Live</c> or has become <c>Stale</c>/<c>Missing</c>
/// is always determined by <see cref="HarnessArtifactResolver"/> re-reading the workspace and
/// recomputing the digest at resolution time — never by trusting a previously recorded status.
/// </para>
/// </remarks>
internal sealed record HarnessArtifactReference
{
    /// <summary>Current schema version for this record shape. Bump when adding/removing fields.</summary>
    internal const int CurrentSchemaVersion = 1;

    /// <summary>Maximum allowed length, in UTF-16 chars, of <see cref="Description"/>.</summary>
    internal const int MaximumDescriptionLength = 500;

    private HarnessArtifactReference(
        int schemaVersion,
        string referenceId,
        string workspacePath,
        string contentDigest,
        int contentByteSize,
        string description,
        string ownerUserId,
        string ownerOrchestrationId,
        string ownerSessionId,
        string creatingToolName,
        string creatingCallId,
        DateTimeOffset createdAtUtc)
    {
        SchemaVersion = schemaVersion;
        ReferenceId = referenceId;
        WorkspacePath = workspacePath;
        ContentDigest = contentDigest;
        ContentByteSize = contentByteSize;
        Description = description;
        OwnerUserId = ownerUserId;
        OwnerOrchestrationId = ownerOrchestrationId;
        OwnerSessionId = ownerSessionId;
        CreatingToolName = creatingToolName;
        CreatingCallId = creatingCallId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Schema version this record was constructed under.</summary>
    internal int SchemaVersion { get; }

    /// <summary>
    /// Stable, bounded, reference-facing identity string (<c>artifact://sha256/{digest}</c>).
    /// Deterministic: identical content always produces the identical identity.
    /// </summary>
    internal string ReferenceId { get; }

    /// <summary>The workspace-relative root segment this artifact is sharded under.</summary>
    internal string ArtifactRoot => HarnessArtifactIdentity.DefaultArtifactRoot;

    /// <summary>The canonical, content-addressed workspace path the artifact was written to.</summary>
    internal string WorkspacePath { get; }

    /// <summary>Lowercase hex SHA-256 digest of the artifact's exact UTF-8 content bytes.</summary>
    internal string ContentDigest { get; }

    /// <summary>UTF-8 byte length of the artifact content at creation time.</summary>
    internal int ContentByteSize { get; }

    /// <summary>Bounded, human-readable summary of the artifact (never the raw content itself).</summary>
    internal string Description { get; }

    /// <summary>Audit-only owning user identity, captured from the trusted execution binding.</summary>
    internal string OwnerUserId { get; }

    /// <summary>Audit-only owning orchestration identity, captured from the trusted execution binding.</summary>
    internal string OwnerOrchestrationId { get; }

    /// <summary>Audit-only owning session identity, captured from the trusted execution binding.</summary>
    internal string OwnerSessionId { get; }

    /// <summary>Name of the tool/function whose result produced this artifact.</summary>
    internal string CreatingToolName { get; }

    /// <summary>Call ID of the tool invocation that produced this artifact.</summary>
    internal string CreatingCallId { get; }

    /// <summary>Creation evidence: the timestamp this reference was minted.</summary>
    internal DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Creates a fresh reference by hashing <paramref name="content"/> directly, guaranteeing the
    /// digest and <see cref="WorkspacePath"/> are consistent by construction. Ownership is sourced
    /// only from the trusted <paramref name="executionBinding"/>, and a bound workspace is required
    /// so this minting path is only available to host-authorized executions.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="executionBinding"/>, <paramref name="content"/>,
    /// <paramref name="description"/>, <paramref name="creatingToolName"/>, or
    /// <paramref name="creatingCallId"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="executionBinding"/> carries no authorized workspace; any tool/call identity
    /// is empty or whitespace-only; or <paramref name="description"/> exceeds
    /// <see cref="MaximumDescriptionLength"/>.
    /// </exception>
    internal static HarnessArtifactReference Create(
        HarnessExecutionBinding executionBinding,
        string content,
        string description,
        string creatingToolName,
        string creatingCallId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(executionBinding);
        ArgumentNullException.ThrowIfNull(content);

        if (executionBinding.Workspace is null)
        {
            throw new ArgumentException(
                "The execution binding must carry an authorized workspace.",
                nameof(executionBinding));
        }

        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var byteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);
        var path = HarnessArtifactIdentity.BuildPath(digest);

        return Reconstruct(
            path,
            digest,
            byteSize,
            description,
            executionBinding.UserId,
            executionBinding.OrchestrationId,
            executionBinding.SessionId,
            creatingToolName,
            creatingCallId,
            createdAtUtc);
    }

    /// <summary>
    /// Reconstructs and fully revalidates a reference from recorded/serialized fields — the only
    /// path allowed for untrusted reference data (e.g. a reference identity a prior model turn or a
    /// restored session echoed back). Independently recomputes the expected content-addressed path
    /// from <paramref name="contentDigest"/> and rejects any mismatch with
    /// <paramref name="workspacePath"/>; a reference whose path does not match its digest can never
    /// be constructed.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="workspacePath"/>, <paramref name="contentDigest"/>,
    /// <paramref name="description"/>, <paramref name="ownerUserId"/>,
    /// <paramref name="ownerOrchestrationId"/>, <paramref name="ownerSessionId"/>,
    /// <paramref name="creatingToolName"/>, or <paramref name="creatingCallId"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="contentDigest"/> is not a well-formed lowercase 64-character hex SHA-256
    /// digest; <paramref name="workspacePath"/> does not match the path derived from the fixed
    /// artifact root and <paramref name="contentDigest"/>; <paramref name="contentByteSize"/> is not
    /// greater than zero; any owner/tool/call identity is empty or whitespace-only; or
    /// <paramref name="description"/> exceeds <see cref="MaximumDescriptionLength"/>.
    /// </exception>
    internal static HarnessArtifactReference Reconstruct(
        string workspacePath,
        string contentDigest,
        int contentByteSize,
        string description,
        string ownerUserId,
        string ownerOrchestrationId,
        string ownerSessionId,
        string creatingToolName,
        string creatingCallId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(workspacePath);
        ArgumentNullException.ThrowIfNull(contentDigest);
        ArgumentNullException.ThrowIfNull(description);
        RequireNonWhiteSpace(ownerUserId, nameof(ownerUserId));
        RequireNonWhiteSpace(ownerOrchestrationId, nameof(ownerOrchestrationId));
        RequireNonWhiteSpace(ownerSessionId, nameof(ownerSessionId));
        RequireNonWhiteSpace(creatingToolName, nameof(creatingToolName));
        RequireNonWhiteSpace(creatingCallId, nameof(creatingCallId));

        if (!HarnessArtifactIdentity.IsWellFormedDigest(contentDigest))
        {
            throw new ArgumentException(
                $"'{contentDigest}' is not a well-formed lowercase {HarnessArtifactIdentity.DigestHexLength}-character hex SHA-256 digest.",
                nameof(contentDigest));
        }

        var expectedPath = HarnessArtifactIdentity.BuildPath(contentDigest);
        var canonicalSuppliedPath = WorkspacePathUtil.Canonicalize(workspacePath);
        if (!WorkspacePathUtil.PathComparer.Equals(expectedPath, canonicalSuppliedPath))
        {
            throw new ArgumentException(
                $"Workspace path '{workspacePath}' does not match the content-addressed path " +
                $"'{expectedPath}' derived from digest '{contentDigest}' under the fixed artifact root " +
                $"'{HarnessArtifactIdentity.DefaultArtifactRoot}'. A reference's path must always be a " +
                "pure function of its digest and that fixed root.",
                nameof(workspacePath));
        }

        if (contentByteSize <= 0)
        {
            throw new ArgumentException(
                "Artifact content byte size must be greater than zero.",
                nameof(contentByteSize));
        }

        if (description.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"Artifact description length ({description.Length}) exceeds the maximum of " +
                $"{MaximumDescriptionLength} characters.",
                nameof(description));
        }

        var referenceId = HarnessArtifactIdentity.BuildReferenceId(contentDigest);

        return new HarnessArtifactReference(
            CurrentSchemaVersion,
            referenceId,
            expectedPath,
            contentDigest,
            contentByteSize,
            description,
            ownerUserId,
            ownerOrchestrationId,
            ownerSessionId,
            creatingToolName,
            creatingCallId,
            createdAtUtc);
    }

    private static void RequireNonWhiteSpace(string? value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A non-empty, non-whitespace value is required.",
                paramName);
        }
    }
}
