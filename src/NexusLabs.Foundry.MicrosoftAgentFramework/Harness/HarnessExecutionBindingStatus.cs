namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal enum HarnessExecutionBindingStatus
{
    Valid,
    MissingContext,
    MissingWorkspace,
    InvalidSessionId,
    IdentityMismatch,
    WorkspaceMismatch,
    SessionMismatch,
}
