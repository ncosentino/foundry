using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only <see cref="IHarnessContextSizeEstimator"/> returning the same fixed size for every entry
/// regardless of its id or content, for seam/composition/cancellation tests whose entry ids are minted
/// dynamically (for example by <see cref="HarnessScriptedMessageClassifier"/>'s content-derived digests)
/// and so cannot be pre-registered in a <see cref="HarnessFixedSizeContextEstimator"/> dictionary.
/// </summary>
internal sealed class HarnessConstantSizeContextEstimator : IHarnessContextSizeEstimator
{
    private readonly int _size;

    internal HarnessConstantSizeContextEstimator(int size)
    {
        _size = size;
    }

    public int EstimateSize(HarnessContextEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _size;
    }
}
