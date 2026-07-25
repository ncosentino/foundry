using System.Text;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministic <see cref="IHarnessContextSizeEstimator"/> that reports the UTF-8 byte length of a
/// <see cref="HarnessContextEntry"/>'s message text. Named explicitly for exactly what it measures:
/// this is a byte count, never a provider token estimate. Non-text content (for example a function
/// call or function result with no text representation) contributes zero — a caller that needs those
/// accounted for must supply a different <see cref="IHarnessContextSizeEstimator"/>.
/// </summary>
internal sealed class HarnessUtf8TextSizeEstimator : IHarnessContextSizeEstimator
{
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public int EstimateSize(HarnessContextEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var text = entry.Message.Text;
        return string.IsNullOrEmpty(text) ? 0 : Encoding.UTF8.GetByteCount(text);
    }
}
