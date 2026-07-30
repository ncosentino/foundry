namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Executes one attempt for one arm of the <c>harness-001</c> paired comparison.
/// </summary>
/// <typeparam name="TOutput">The caller-owned normalized trial output type.</typeparam>
/// <param name="context">The arm, case, trial, attempt, feature, and paired-seed context.</param>
/// <param name="cancellationToken">The caller and optional attempt-timeout token.</param>
/// <returns>The arm-specific trial output.</returns>
public delegate ValueTask<TOutput> HarnessComparisonArmExecutor<TOutput>(
    HarnessComparisonArmExecutionContext context,
    CancellationToken cancellationToken);
