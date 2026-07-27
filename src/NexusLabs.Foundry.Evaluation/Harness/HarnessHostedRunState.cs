namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Identifies the reporter-level terminal state of one hosted Harness comparison run.
/// </summary>
public enum HarnessHostedRunState
{
    /// <summary>All planned paired batches completed.</summary>
    Completed,

    /// <summary>A binding scheduling cap prevented one or more complete paired batches from starting.</summary>
    TruncatedByCap,

    /// <summary>The caller canceled the whole hosted run.</summary>
    CanceledByCaller,

    /// <summary>A hidden case, reference, or manifest defect invalidated comparative conclusions.</summary>
    InvalidInput,
}
