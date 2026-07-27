namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// One versioned comparative task case declared by a <see cref="HarnessCaseSetManifest"/>. A case is
/// either a hosted case (participating in the pre-registered comparison) or a development case
/// (excluded from the hosted run by default). Each case carries the deterministic reference
/// descriptors for every decision dimension it participates in.
/// </summary>
public sealed record HarnessManifestCase
{
    /// <summary>Gets the stable, case-set-unique case identifier (for example <c>h001-01</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Gets the coarse task category label for the case.</summary>
    public required string TaskCategory { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a development case. Development cases are outside the
    /// hosted ID set and are excluded from the hosted run by default.
    /// </summary>
    public required bool Development { get; init; }

    /// <summary>Gets the deterministic reference descriptors for the dimensions this case participates in.</summary>
    public required IReadOnlyList<HarnessDeterministicReference> DeterministicReferences { get; init; }

    /// <summary>Gets optional descriptive tags copied into the materialized experiment case.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
