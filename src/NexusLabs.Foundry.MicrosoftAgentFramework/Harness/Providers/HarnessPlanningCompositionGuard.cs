using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

/// <summary>
/// Focused profile/plugin coherence guard for the Todo and AgentMode selected-provider
/// slice. Todo and AgentMode are independently selectable, so this guard checks each
/// capability against its own provider rather than treating the pair as a single unit:
/// a capability enabled without its provider, a provider supplied for a disabled
/// capability, and a plugin supplied while neither capability is selected are all
/// distinct, precisely reported failures.
/// </summary>
internal static class HarnessPlanningCompositionGuard
{
    internal static HarnessPlanningCompositionGuardResult Validate(
        HarnessCapabilityProfile profile,
        HarnessPlanningProvidersPlugin? planningProviders)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var todoEnabled =
            profile.Capabilities[HarnessCapability.Todo].EffectiveState ==
            HarnessCapabilityState.Enabled;
        var agentModeEnabled =
            profile.Capabilities[HarnessCapability.AgentMode].EffectiveState ==
            HarnessCapabilityState.Enabled;

        if (!todoEnabled && !agentModeEnabled)
        {
            return planningProviders is null
                ? Valid()
                : Failure(
                    HarnessPlanningCompositionGuardStatus.PlanningPluginUnexpected,
                    "A Todo/AgentMode provider plugin was supplied while the capability " +
                    "profile does not select either the Todo or AgentMode capability.");
        }

        if (todoEnabled && planningProviders?.TodoProvider is null)
        {
            return Failure(
                HarnessPlanningCompositionGuardStatus.TodoProviderRequired,
                "The Todo capability is selected but no TodoProvider was supplied.");
        }

        if (!todoEnabled && planningProviders?.TodoProvider is not null)
        {
            return Failure(
                HarnessPlanningCompositionGuardStatus.TodoProviderUnexpected,
                "A TodoProvider was supplied while the Todo capability is not selected.");
        }

        if (agentModeEnabled && planningProviders?.AgentModeProvider is null)
        {
            return Failure(
                HarnessPlanningCompositionGuardStatus.AgentModeProviderRequired,
                "The AgentMode capability is selected but no AgentModeProvider was " +
                "supplied.");
        }

        if (!agentModeEnabled && planningProviders?.AgentModeProvider is not null)
        {
            return Failure(
                HarnessPlanningCompositionGuardStatus.AgentModeProviderUnexpected,
                "An AgentModeProvider was supplied while the AgentMode capability is not " +
                "selected.");
        }

        return Valid();
    }

    private static HarnessPlanningCompositionGuardResult Valid() =>
        new(HarnessPlanningCompositionGuardStatus.Valid, null);

    private static HarnessPlanningCompositionGuardResult Failure(
        HarnessPlanningCompositionGuardStatus status,
        string detail) =>
        new(status, detail);
}
