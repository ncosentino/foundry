namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// A single deterministic reference descriptor: it names the decision dimension it anchors, a stable
/// reference identity, the case-set-relative path to the reference evidence file, and, when known, the
/// lowercase SHA-256 digest of that file. It is a descriptor only — the loader validates its shape,
/// never that the referenced file exists on disk (the reference evidence files are authored and hashed
/// separately when the case set is frozen).
/// </summary>
public sealed record HarnessDeterministicReference
{
    /// <summary>Gets the decision dimension this reference anchors.</summary>
    public required HarnessEvaluationDimension Dimension { get; init; }

    /// <summary>Gets the stable, case-set-unique reference identity.</summary>
    public required string ReferenceId { get; init; }

    /// <summary>Gets the case-set-relative path to the reference evidence file.</summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Gets the lowercase 64-character hex SHA-256 digest of the reference evidence file, or
    /// <see langword="null"/> when the digest is recorded at run time rather than pinned in the manifest.
    /// </summary>
    public string? Sha256 { get; init; }
}
