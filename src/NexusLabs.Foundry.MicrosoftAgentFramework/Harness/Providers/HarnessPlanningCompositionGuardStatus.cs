namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal enum HarnessPlanningCompositionGuardStatus
{
    Valid,
    PlanningPluginUnexpected,
    TodoProviderRequired,
    TodoProviderUnexpected,
    AgentModeProviderRequired,
    AgentModeProviderUnexpected,
}
