using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Focused profile/plugin coherence guard for the three independent tool-approval
/// capabilities: <c>ApprovalResponseBinding</c>, <c>ApprovalNotRequiredBypassing</c>, and
/// <c>ToolAutoApproval</c>. Each capability is checked against its own plugin flag rather
/// than treating the three as a single unit: a capability enabled without the plugin
/// contributing it, a plugin contributing a capability that is not selected, and a plugin
/// supplied while none of the three capabilities are selected are all distinct, precisely
/// reported failures. MAF defaults must not implicitly enable an unselected capability, so
/// this guard never infers a capability from the plugin alone -- the profile is always the
/// source of truth for what was selected.
/// </summary>
internal static class HarnessApprovalCompositionGuard
{
    internal static HarnessApprovalCompositionGuardResult Validate(
        HarnessCapabilityProfile profile,
        HarnessApprovalPlugin? approvalPlugin)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var responseBindingEnabled =
            profile.Capabilities[HarnessCapability.ApprovalResponseBinding].EffectiveState ==
            HarnessCapabilityState.Enabled;
        var notRequiredBypassingEnabled =
            profile.Capabilities[HarnessCapability.ApprovalNotRequiredBypassing].EffectiveState ==
            HarnessCapabilityState.Enabled;
        var toolAutoApprovalEnabled =
            profile.Capabilities[HarnessCapability.ToolAutoApproval].EffectiveState ==
            HarnessCapabilityState.Enabled;

        if (!responseBindingEnabled && !notRequiredBypassingEnabled && !toolAutoApprovalEnabled)
        {
            return approvalPlugin is null
                ? Valid()
                : Failure(
                    HarnessApprovalCompositionGuardStatus.ApprovalPluginUnexpected,
                    "An approval plugin was supplied while the capability profile does not " +
                    "select any of the three approval capabilities.");
        }

        if (responseBindingEnabled && approvalPlugin?.ResponseBindingEnabled != true)
        {
            return Failure(
                HarnessApprovalCompositionGuardStatus.ApprovalResponseBindingRequired,
                "The ApprovalResponseBinding capability is selected but the supplied " +
                "approval plugin does not enable it.");
        }

        if (!responseBindingEnabled && approvalPlugin?.ResponseBindingEnabled == true)
        {
            return Failure(
                HarnessApprovalCompositionGuardStatus.ApprovalResponseBindingUnexpected,
                "The approval plugin enables ApprovalResponseBinding while the capability " +
                "is not selected.");
        }

        if (notRequiredBypassingEnabled && approvalPlugin?.NotRequiredBypassingEnabled != true)
        {
            return Failure(
                HarnessApprovalCompositionGuardStatus.ApprovalNotRequiredBypassingRequired,
                "The ApprovalNotRequiredBypassing capability is selected but the supplied " +
                "approval plugin does not enable it.");
        }

        if (!notRequiredBypassingEnabled && approvalPlugin?.NotRequiredBypassingEnabled == true)
        {
            return Failure(
                HarnessApprovalCompositionGuardStatus.ApprovalNotRequiredBypassingUnexpected,
                "The approval plugin enables ApprovalNotRequiredBypassing while the " +
                "capability is not selected.");
        }

        if (toolAutoApprovalEnabled && approvalPlugin?.ToolApprovalOptions is null)
        {
            return Failure(
                HarnessApprovalCompositionGuardStatus.ToolAutoApprovalRequired,
                "The ToolAutoApproval capability is selected but the supplied approval " +
                "plugin does not provide ToolApprovalAgentOptions.");
        }

        if (!toolAutoApprovalEnabled && approvalPlugin?.ToolApprovalOptions is not null)
        {
            return Failure(
                HarnessApprovalCompositionGuardStatus.ToolAutoApprovalUnexpected,
                "The approval plugin provides ToolApprovalAgentOptions while the " +
                "ToolAutoApproval capability is not selected.");
        }

        return Valid();
    }

    private static HarnessApprovalCompositionGuardResult Valid() =>
        new(HarnessApprovalCompositionGuardStatus.Valid, null);

    private static HarnessApprovalCompositionGuardResult Failure(
        HarnessApprovalCompositionGuardStatus status,
        string detail) =>
        new(status, detail);
}
