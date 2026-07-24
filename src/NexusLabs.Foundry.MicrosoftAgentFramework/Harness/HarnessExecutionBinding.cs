using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessExecutionBinding
{
    private HarnessExecutionBinding(
        string userId,
        string orchestrationId,
        string sessionId,
        IWorkspace? workspace,
        bool requiresWorkspace)
    {
        UserId = userId;
        OrchestrationId = orchestrationId;
        SessionId = sessionId;
        Workspace = workspace;
        RequiresWorkspace = requiresWorkspace;
    }

    internal string UserId { get; }

    internal string OrchestrationId { get; }

    internal string SessionId { get; }

    internal IWorkspace? Workspace { get; }

    internal bool RequiresWorkspace { get; }

    internal static HarnessExecutionBindingCaptureResult Capture(
        IAgentExecutionContextAccessor accessor,
        string sessionId,
        bool requireWorkspace)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new HarnessExecutionBindingCaptureResult(
                HarnessExecutionBindingStatus.InvalidSessionId,
                null,
                "A trusted non-empty session identity is required.");
        }

        var context = accessor.Current;
        if (context is null)
        {
            return new HarnessExecutionBindingCaptureResult(
                HarnessExecutionBindingStatus.MissingContext,
                null,
                "No trusted execution context is active.");
        }

        var workspace = context.GetWorkspace();
        if (requireWorkspace && workspace is null)
        {
            return new HarnessExecutionBindingCaptureResult(
                HarnessExecutionBindingStatus.MissingWorkspace,
                null,
                "The active execution context does not contain an authorized workspace.");
        }

        return new HarnessExecutionBindingCaptureResult(
            HarnessExecutionBindingStatus.Valid,
            new HarnessExecutionBinding(
                context.UserId,
                context.OrchestrationId,
                sessionId,
                workspace,
                requireWorkspace),
            null);
    }

    internal HarnessExecutionBindingValidationResult ValidateCurrent(
        IAgentExecutionContextAccessor accessor,
        string sessionId)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        if (!string.Equals(SessionId, sessionId, StringComparison.Ordinal))
        {
            return new HarnessExecutionBindingValidationResult(
                HarnessExecutionBindingStatus.SessionMismatch,
                "The active session does not match the trusted binding.");
        }

        var context = accessor.Current;
        if (context is null)
        {
            return new HarnessExecutionBindingValidationResult(
                HarnessExecutionBindingStatus.MissingContext,
                "No trusted execution context is active.");
        }

        if (!string.Equals(UserId, context.UserId, StringComparison.Ordinal) ||
            !string.Equals(
                OrchestrationId,
                context.OrchestrationId,
                StringComparison.Ordinal))
        {
            return new HarnessExecutionBindingValidationResult(
                HarnessExecutionBindingStatus.IdentityMismatch,
                "The active execution identity does not match the trusted binding.");
        }

        var workspace = context.GetWorkspace();
        if (RequiresWorkspace && workspace is null)
        {
            return new HarnessExecutionBindingValidationResult(
                HarnessExecutionBindingStatus.MissingWorkspace,
                "The active execution context does not contain an authorized workspace.");
        }

        if (!ReferenceEquals(Workspace, workspace))
        {
            return new HarnessExecutionBindingValidationResult(
                HarnessExecutionBindingStatus.WorkspaceMismatch,
                "The active workspace does not match the trusted binding.");
        }

        return new HarnessExecutionBindingValidationResult(
            HarnessExecutionBindingStatus.Valid,
            null);
    }

    internal void EnsureCurrent(
        IAgentExecutionContextAccessor accessor,
        string sessionId)
    {
        var validation = ValidateCurrent(accessor, sessionId);
        if (validation.Status != HarnessExecutionBindingStatus.Valid)
        {
            throw new InvalidOperationException(
                $"Harness execution binding rejected the operation with status '{validation.Status}'.");
        }
    }
}
