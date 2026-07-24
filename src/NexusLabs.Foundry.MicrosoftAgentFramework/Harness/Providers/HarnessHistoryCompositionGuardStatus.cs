namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal enum HarnessHistoryCompositionGuardStatus
{
    Valid,
    HistoryProviderRequired,
    HistoryProviderUnexpected,
    PersistenceModeMismatch,
    UnsupportedPersistenceMode,
}
