namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// The explicit, two-variant trust policy for MAF 1.15's <c>load_skill</c>,
/// <c>read_skill_resource</c>, and <c>run_skill_script</c> approval gates on this G3
/// host-authored inline-skills slice. Selecting the Skills capability never implicitly
/// changes these gates on its own: constructing <see cref="HarnessSkillsPlugin"/> always
/// requires the caller to choose one of the two values below. No other combination of the
/// three underlying MAF <c>AgentSkillsProviderOptions.Disable*Approval</c> flags is
/// reachable through <see cref="HarnessSkillsPlugin"/>.
/// </summary>
internal enum HarnessSkillsTrustPolicy
{
    /// <summary>
    /// MAF's default: all three skill-tool approval gates
    /// (<c>DisableLoadSkillApproval</c>, <c>DisableReadSkillResourceApproval</c>,
    /// <c>DisableRunSkillScriptApproval</c>) stay enabled, so <c>load_skill</c>,
    /// <c>read_skill_resource</c>, and <c>run_skill_script</c> all surface a
    /// <c>ToolApprovalRequestContent</c> before running. Requires the
    /// <c>ApprovalResponseBinding</c> capability and a coherent
    /// <see cref="HarnessApprovalPlugin"/> to resolve those requests; see
    /// <see cref="HarnessSkillsCompositionGuard"/>.
    /// </summary>
    ApprovalRequired,

    /// <summary>
    /// The caller explicitly attests that the supplied inline skills are host-authored and
    /// trusted, disabling all three skill-tool approval gates
    /// (<c>DisableLoadSkillApproval</c>, <c>DisableReadSkillResourceApproval</c>,
    /// <c>DisableRunSkillScriptApproval</c>). This is never inferred from selecting the
    /// Skills capability or from MAF's own defaults -- only from explicitly constructing a
    /// <see cref="HarnessSkillsPlugin"/> with this value.
    /// </summary>
    HostTrusted,
}
