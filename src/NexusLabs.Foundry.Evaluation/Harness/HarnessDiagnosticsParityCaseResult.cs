namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Describes diagnostics comparability for one hosted case in one ordered arm contrast.
/// </summary>
public sealed record HarnessDiagnosticsParityCaseResult
{
    internal HarnessDiagnosticsParityCaseResult(
        string caseId,
        bool fullyScheduled,
        bool isComparable)
    {
        CaseId = caseId;
        FullyScheduled = fullyScheduled;
        IsComparable = isComparable;
    }

    /// <summary>Gets the hosted case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets whether all three trial indices were scheduled for both contrast arms.</summary>
    public bool FullyScheduled { get; }

    /// <summary>Gets whether every normalized dimension schema was marked comparable across the arms.</summary>
    public bool IsComparable { get; }
}
