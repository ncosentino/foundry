using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Narrow, immutable selected-provider slice composing the upstream MAF 1.15
/// <see cref="AgentSkillsProvider"/> from host-authored, in-memory
/// <see cref="AgentInlineSkill"/> instances only.
/// </summary>
/// <remarks>
/// <para>
/// G3 supports only host-authored in-memory/inline skills. This plugin's public
/// construction surface accepts <see cref="AgentInlineSkill"/> instances exclusively --
/// never a raw file system path, an <c>AgentFileSkill</c>, an <c>AgentFileSkillsSource</c>,
/// an <c>AgentFileSkillScriptRunner</c>, or any other <c>AgentSkillsSource</c> -- so
/// file-backed skill discovery and any ambient filesystem source are rejected by the type
/// system before composition is ever attempted. File-backed skills, and their overlap with
/// the deferred G4 <c>FileAccess</c>/<c>FileMemory</c> capabilities, remain out of scope for
/// this slice.
/// </para>
/// <para>
/// MAF 1.15's <c>load_skill</c>, <c>read_skill_resource</c>, and <c>run_skill_script</c>
/// tools require per-call approval by default. Selecting the Skills capability alone never
/// implicitly disables that approval: doing so requires the caller to explicitly construct
/// this plugin with <see cref="HarnessSkillsTrustPolicy.HostTrusted"/>. See
/// <see cref="HarnessSkillsTrustPolicy"/> and <see cref="HarnessSkillsCompositionGuard"/> for
/// the coherence rules attached to each of the two supported trust policies.
/// </para>
/// </remarks>
internal sealed class HarnessSkillsPlugin
{
    private HarnessSkillsPlugin(
        AgentSkillsProvider skillsProvider,
        HarnessSkillsTrustPolicy trustPolicy,
        IReadOnlyList<string> providerStateKeys)
    {
        SkillsProvider = skillsProvider;
        TrustPolicy = trustPolicy;
        ProviderStateKeys = providerStateKeys;
    }

    /// <summary>
    /// The upstream in-memory skills provider, constructed exclusively from host-authored
    /// inline skills. Composed into <c>ChatClientAgentOptions.AIContextProviders</c> by
    /// <see cref="HarnessProviderComposition"/> and never exposed through
    /// <see cref="HarnessGuardedAgent.GetService"/>.
    /// </summary>
    internal AgentSkillsProvider SkillsProvider { get; }

    /// <summary>
    /// The explicit trust policy the caller selected for this slice's three MAF skill-tool
    /// approval gates.
    /// </summary>
    internal HarnessSkillsTrustPolicy TrustPolicy { get; }

    /// <summary>
    /// The canonical (ordinal, sorted, de-duplicated) set of session state keys
    /// <see cref="SkillsProvider"/> contributes to <c>AgentSession.StateBag</c>, for union
    /// into the trusted schema-v2 session envelope alongside history/planning state keys.
    /// </summary>
    internal IReadOnlyList<string> ProviderStateKeys { get; }

    /// <summary>
    /// Creates a skills plugin from one or more host-authored inline skills and an explicit
    /// trust policy. At least one inline skill must be supplied; a plugin contributing no
    /// skills would add nothing and must not be constructed. <paramref name="trustPolicy"/>
    /// has no default: callers must decide explicitly whether MAF's three skill-tool
    /// approval gates stay enabled (<see cref="HarnessSkillsTrustPolicy.ApprovalRequired"/>)
    /// or are disabled because the supplied inline skills are host-trusted
    /// (<see cref="HarnessSkillsTrustPolicy.HostTrusted"/>).
    /// </summary>
    internal static HarnessSkillsPlugin Create(
        IReadOnlyList<AgentInlineSkill> skills,
        HarnessSkillsTrustPolicy trustPolicy) =>
        Create(skills, trustPolicy, loggerFactory: null);

    /// <summary>
    /// Creates a skills plugin from one or more host-authored inline skills, an explicit
    /// trust policy, and the logger factory used by the upstream MAF provider.
    /// </summary>
    internal static HarnessSkillsPlugin Create(
        IReadOnlyList<AgentInlineSkill> skills,
        HarnessSkillsTrustPolicy trustPolicy,
        ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(skills);
        if (skills.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one host-authored inline skill must be supplied to construct a " +
                "skills plugin.");
        }

        if (skills.Any(skill => skill is null))
        {
            throw new InvalidOperationException(
                "The supplied inline skills must not contain a null entry.");
        }

        if (!Enum.IsDefined(trustPolicy))
        {
            throw new InvalidOperationException(
                $"Skills trust policy '{trustPolicy}' is not supported.");
        }

        // The trust policy is the sole, explicit source of these three flags: no partial
        // combination is reachable, and selecting Skills alone never reaches this code path
        // (HarnessSkillsCompositionGuard requires a plugin before any of this runs).
        var hostTrusted = trustPolicy == HarnessSkillsTrustPolicy.HostTrusted;
        var options = new AgentSkillsProviderOptions
        {
            DisableLoadSkillApproval = hostTrusted,
            DisableReadSkillResourceApproval = hostTrusted,
            DisableRunSkillScriptApproval = hostTrusted,
        };

        var provider = new AgentSkillsProvider(
            skills.ToArray(),
            options,
            loggerFactory);
        var stateKeys = provider.StateKeys;
        if (stateKeys.Count == 0 ||
            stateKeys.Any(string.IsNullOrWhiteSpace) ||
            stateKeys.Distinct(StringComparer.Ordinal).Count() != stateKeys.Count)
        {
            throw new InvalidOperationException(
                "The upstream skills provider must expose unique, non-empty state keys.");
        }

        return new HarnessSkillsPlugin(
            provider,
            trustPolicy,
            [.. stateKeys.OrderBy(key => key, StringComparer.Ordinal)]);
    }
}
