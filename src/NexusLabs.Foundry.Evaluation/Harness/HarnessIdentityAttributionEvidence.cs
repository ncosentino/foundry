namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item evidence for identity attribution: which workflow the run belonged to, which agent
/// identity was expected to own the run's records, which agent identities were actually observed, and
/// how many records that should have carried an agent identity did not.
/// </summary>
public sealed record HarnessIdentityAttributionEvidence
{
    /// <summary>Gets the workflow correlation identity for the run.</summary>
    public required string WorkflowId { get; init; }

    /// <summary>Gets the agent identity expected to own the run's attributed records.</summary>
    public required string ExpectedAgentId { get; init; }

    /// <summary>Gets the distinct agent identities observed across the run's attributed records.</summary>
    public required IReadOnlyList<string> ObservedAgentIds { get; init; }

    /// <summary>
    /// Gets the number of records that were expected to carry an agent identity but did not.
    /// </summary>
    public required int UnattributedRecordCount { get; init; }
}
