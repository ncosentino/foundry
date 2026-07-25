using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only <see cref="IHarnessContextSizeEstimator"/> returning an exact, per-entry-id configured
/// size, so margin/trigger-threshold tests can construct entry sets whose total estimated size is
/// known precisely rather than depending on incidental text length.
/// </summary>
internal sealed class HarnessFixedSizeContextEstimator : IHarnessContextSizeEstimator
{
    private readonly IReadOnlyDictionary<string, int> _sizesByEntryId;

    internal HarnessFixedSizeContextEstimator(IReadOnlyDictionary<string, int> sizesByEntryId)
    {
        ArgumentNullException.ThrowIfNull(sizesByEntryId);
        _sizesByEntryId = sizesByEntryId;
    }

    /// <summary>
    /// Always <see cref="HarnessContextMeasurementUnit.HostDefinedUnits"/>: this fixture's sizes are
    /// arbitrary configured values with no byte or token meaning.
    /// </summary>
    public HarnessContextMeasurementUnit MeasurementUnit => HarnessContextMeasurementUnit.HostDefinedUnits;

    public int EstimateSize(HarnessContextEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!_sizesByEntryId.TryGetValue(entry.EntryId, out var size))
        {
            throw new InvalidOperationException($"No configured fixture size for entry id '{entry.EntryId}'.");
        }

        return size;
    }
}
