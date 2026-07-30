namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// The versioned, immutable case-set manifest for a hosted Harness comparison. It pins the case-set
/// identity/version, the frozen hosted trial count, and the ordered case declarations. Once frozen,
/// any change to the arm-neutral case declarations, hosted IDs, or trial count requires a new
/// case-set version.
/// </summary>
public sealed record HarnessCaseSetManifest
{
    /// <summary>Gets the manifest schema version this document conforms to.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Gets the stable case-set identifier (for example <c>harness-001</c>).</summary>
    public required string CaseSetId { get; init; }

    /// <summary>Gets the case-set version (for example <c>v1.0</c>).</summary>
    public required string Version { get; init; }

    /// <summary>Gets the frozen number of independent trials each hosted case runs per arm.</summary>
    public required int HostedTrialCount { get; init; }

    /// <summary>Gets the ordered case declarations, both hosted and development.</summary>
    public required IReadOnlyList<HarnessManifestCase> Cases { get; init; }
}
