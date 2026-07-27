using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace HarnessEvaluationApp;

internal sealed class HostedHybridSnapshotProvider(
    IReadOnlyList<HarnessContextEntry> entries) : IHarnessContextSnapshotProvider
{
    public HarnessContextSnapshot CaptureSnapshot() =>
        HarnessContextSnapshot.Create(0, entries);
}
