using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Focused profile/plugin coherence guard for the single-capability Skills selected-provider
/// slice. Beyond the capability-enabled/plugin-supplied symmetry shared with the other
/// single-plugin guards, this guard also enforces the explicit skills trust-policy coherence
/// rule: a plugin using <see cref="HarnessSkillsTrustPolicy.ApprovalRequired"/> leaves MAF's
/// three skill-tool approval gates enabled, so it requires the <c>ApprovalResponseBinding</c>
/// capability and a coherent <see cref="HarnessApprovalPlugin"/> to be selected alongside it;
/// a plugin using <see cref="HarnessSkillsTrustPolicy.HostTrusted"/> carries no such
/// requirement, since it disables those gates itself.
/// </summary>
internal static class HarnessSkillsCompositionGuard
{
    internal static HarnessSkillsCompositionGuardResult Validate(
        HarnessCapabilityProfile profile,
        HarnessSkillsPlugin? skillsPlugin,
        HarnessApprovalPlugin? approvalPlugin)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var skillsEnabled =
            profile.Capabilities[HarnessCapability.Skills].EffectiveState ==
            HarnessCapabilityState.Enabled;

        if (!skillsEnabled)
        {
            return skillsPlugin is null
                ? Valid()
                : Failure(
                    HarnessSkillsCompositionGuardStatus.SkillsPluginUnexpected,
                    "A skills plugin was supplied while the capability profile does not " +
                    "select the Skills capability.");
        }

        if (skillsPlugin is null)
        {
            return Failure(
                HarnessSkillsCompositionGuardStatus.SkillsPluginRequired,
                "The Skills capability is selected but no skills plugin was supplied.");
        }

        if (skillsPlugin.TrustPolicy == HarnessSkillsTrustPolicy.ApprovalRequired)
        {
            var responseBindingEnabled =
                profile.Capabilities[HarnessCapability.ApprovalResponseBinding].EffectiveState ==
                HarnessCapabilityState.Enabled;
            if (!responseBindingEnabled || approvalPlugin?.ResponseBindingEnabled != true)
            {
                return Failure(
                    HarnessSkillsCompositionGuardStatus.SkillsApprovalCoherenceRequired,
                    "The skills plugin uses the ApprovalRequired trust policy, which leaves " +
                    "MAF's load_skill/read_skill_resource/run_skill_script approval gates " +
                    "enabled, but the ApprovalResponseBinding capability and a coherent " +
                    "approval plugin enabling it were not both selected to resolve the " +
                    "resulting approval requests.");
            }
        }

        return Valid();
    }

    private static HarnessSkillsCompositionGuardResult Valid() =>
        new(HarnessSkillsCompositionGuardStatus.Valid, null);

    private static HarnessSkillsCompositionGuardResult Failure(
        HarnessSkillsCompositionGuardStatus status,
        string detail) =>
        new(status, detail);
}
