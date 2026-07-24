using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;

internal static class HarnessHistoryCompositionGuard
{
    internal static HarnessHistoryCompositionGuardResult Validate(
        HarnessCapabilityProfile profile,
        HarnessHistoryProviderPlugin? historyProvider)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var historyEvidence = profile.Capabilities[HarnessCapability.PerServiceHistory];
        if (!historyEvidence.Requested)
        {
            return historyProvider is null
                ? Valid()
                : Failure(
                    HarnessHistoryCompositionGuardStatus.HistoryProviderUnexpected,
                    "A history provider plugin was supplied while the capability profile " +
                    "does not request per-service history.");
        }

        if (profile.HistoryPersistenceMode is HarnessHistoryPersistenceMode.NotApplicable or
            HarnessHistoryPersistenceMode.ServiceManaged)
        {
            return Failure(
                HarnessHistoryCompositionGuardStatus.UnsupportedPersistenceMode,
                $"Persistence mode '{profile.HistoryPersistenceMode}' is not a supported " +
                "selected-provider history mode.");
        }

        if (historyProvider is null)
        {
            return Failure(
                HarnessHistoryCompositionGuardStatus.HistoryProviderRequired,
                "PerServiceHistory requires a history provider plugin.");
        }

        if (profile.HistoryPersistenceMode != historyProvider.PersistenceMode)
        {
            return Failure(
                HarnessHistoryCompositionGuardStatus.PersistenceModeMismatch,
                $"The profile persistence mode '{profile.HistoryPersistenceMode}' does not " +
                $"match the history provider mode '{historyProvider.PersistenceMode}'.");
        }

        return Valid();
    }

    private static HarnessHistoryCompositionGuardResult Valid() =>
        new(HarnessHistoryCompositionGuardStatus.Valid, null);

    private static HarnessHistoryCompositionGuardResult Failure(
        HarnessHistoryCompositionGuardStatus status,
        string detail) =>
        new(status, detail);
}
