namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal enum HarnessApprovalCompositionGuardStatus
{
    Valid,
    ApprovalPluginUnexpected,
    ApprovalResponseBindingRequired,
    ApprovalResponseBindingUnexpected,
    ApprovalNotRequiredBypassingRequired,
    ApprovalNotRequiredBypassingUnexpected,
    ToolAutoApprovalRequired,
    ToolAutoApprovalUnexpected,
}
