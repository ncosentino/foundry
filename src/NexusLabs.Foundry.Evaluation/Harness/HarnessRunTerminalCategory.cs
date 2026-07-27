namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// The terminal disposition category observed for a single agent run/trial. Deterministic evaluators
/// use this to score cancellation, timeout, and termination-appropriateness dimensions without
/// re-deriving them from free-form text.
/// </summary>
public enum HarnessRunTerminalCategory
{
    /// <summary>The run completed and produced a terminal output.</summary>
    Completed,

    /// <summary>The attempt exceeded its per-attempt timeout.</summary>
    PerAttemptTimeout,

    /// <summary>The run was canceled at the task level (for example a retryable infrastructure condition).</summary>
    TaskCanceled,

    /// <summary>The whole hosted run was canceled by the caller.</summary>
    CallerCanceled,

    /// <summary>The run failed with an execution error before producing a terminal output.</summary>
    ExecutionFailure,

    /// <summary>The run completed but failed the deterministic task-completion predicate.</summary>
    DeterministicTaskFailure,
}
