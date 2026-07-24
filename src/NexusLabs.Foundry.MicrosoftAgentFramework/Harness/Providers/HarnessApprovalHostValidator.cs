namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Required host callback that authorizes reliance on a standing ("always approve") tool
/// approval. MAF 1.15's <c>ToolApprovalAgent</c> transparently unwraps
/// <c>AlwaysApproveToolApprovalResponseContent</c> into a persisted rule and later matches
/// future tool calls against it with no public extensibility point for host review, so
/// <see cref="HarnessGuardedAgent"/> enforces this check itself, under
/// <see cref="HarnessExecutionBinding.EnsureCurrent"/>, before any run that could create or
/// rely on such a rule is allowed to proceed.
/// </summary>
/// <param name="context">Stable, non-MAF identifiers describing the run being authorized.</param>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
/// <returns>
/// <see langword="true"/> to authorize the run; <see langword="false"/> to fail it closed.
/// A thrown exception also fails the run closed.
/// </returns>
internal delegate ValueTask<bool> HarnessApprovalHostValidator(
    HarnessApprovalHostValidationContext context,
    CancellationToken cancellationToken);
