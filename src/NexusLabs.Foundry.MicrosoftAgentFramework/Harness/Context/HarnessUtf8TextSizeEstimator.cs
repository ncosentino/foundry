using System.Text;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministic <see cref="IHarnessContextSizeEstimator"/> that reports the UTF-8 byte length of a
/// <see cref="HarnessContextEntry"/>'s message text only. Named explicitly for exactly what it
/// measures: this is a text byte count, never a provider token estimate, and never a full accounting
/// of an entry's size.
/// </summary>
/// <remarks>
/// <strong>Text-only — never use this for a production trigger decision.</strong> A
/// <see cref="HarnessContextEntryKind.ToolExchange"/> entry's <see cref="FunctionCallContent"/> or
/// <see cref="FunctionResultContent"/> payload has no <see cref="Microsoft.Extensions.AI.ChatMessage.Text"/>
/// representation, so this estimator reports <c>0</c> for it and silently ignores potentially large
/// call arguments or result payloads. Use <see cref="HarnessUtf8ContextSizeEstimator"/> — which counts
/// call ids/names/arguments and result ids/payloads in addition to text — for any real
/// <see cref="HarnessHybridContextPolicy"/> trigger decision. This type exists only for tests that
/// intentionally isolate text-length arithmetic from tool-payload accounting.
/// </remarks>
internal sealed class HarnessUtf8TextSizeEstimator : IHarnessContextSizeEstimator
{
    /// <summary>Always <see cref="HarnessContextMeasurementUnit.Utf8Bytes"/>: this estimator counts UTF-8 text bytes only.</summary>
    public HarnessContextMeasurementUnit MeasurementUnit => HarnessContextMeasurementUnit.Utf8Bytes;

    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public int EstimateSize(HarnessContextEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var text = entry.Message.Text;
        return string.IsNullOrEmpty(text) ? 0 : Encoding.UTF8.GetByteCount(text);
    }
}
