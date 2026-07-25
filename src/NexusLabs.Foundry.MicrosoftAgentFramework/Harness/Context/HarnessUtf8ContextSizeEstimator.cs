using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministic <see cref="IHarnessContextSizeEstimator"/> that reports the total UTF-8 byte length of
/// every model-facing part of a <see cref="HarnessContextEntry"/>'s message: <see cref="TextContent"/>
/// text, and — unlike <see cref="HarnessUtf8TextSizeEstimator"/> — a <see cref="FunctionCallContent"/>'s
/// call id, name, and normalized arguments, and a <see cref="FunctionResultContent"/>'s call id and
/// normalized result payload. Named explicitly for exactly what it measures: this is a byte count,
/// never a provider token estimate, so it never claims token exactness.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists.</strong> A policy that only measures text can never trigger compaction on
/// a context dominated by large tool-call arguments or tool-result payloads — silently letting an
/// oversized tool exchange accumulate unbounded. This estimator is the one production candidates
/// should configure a <see cref="HarnessHybridContextPolicy"/> with.
/// </para>
/// <para>
/// <strong>AOT-safe value sizing only.</strong> A normalized argument or result value is only ever one
/// of the explicit shapes <see cref="HarnessContextEntry.NormalizeValue"/> documents:
/// <see langword="null"/>, <see cref="string"/>, a common primitive value type,
/// <see cref="JsonElement"/>, an <see cref="IDictionary{TKey,TValue}"/> of string to
/// <see langword="object"/>, or an <see cref="IList{T}"/> of <see langword="object"/> (including
/// arrays), recursively. This estimator sizes exactly those shapes and throws
/// <see cref="NotSupportedException"/> for anything else, so a value that could never have legally
/// entered a <see cref="HarnessContextEntry"/> can never be silently mis-measured here either.
/// </para>
/// </remarks>
internal sealed class HarnessUtf8ContextSizeEstimator : IHarnessContextSizeEstimator
{
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// A function-call argument value or a function-result payload value has a type this estimator
    /// (mirroring <see cref="HarnessContextEntry.NormalizeValue"/>) does not support.
    /// </exception>
    public int EstimateSize(HarnessContextEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var total = 0;
        foreach (var content in entry.Message.Contents)
        {
            total += EstimateContentSize(content);
        }

        return total;
    }

    private static int EstimateContentSize(AIContent content) => content switch
    {
        TextContent text => Utf8ByteCount(text.Text),
        FunctionCallContent call => Utf8ByteCount(call.CallId)
            + Utf8ByteCount(call.Name)
            + (call.Arguments is null ? 0 : EstimateValueSize(call.Arguments)),
        FunctionResultContent result => Utf8ByteCount(result.CallId) + EstimateValueSize(result.Result),
        _ => 0,
    };

    /// <summary>
    /// Deterministic byte-size estimate for a normalized argument or result value, mirroring the exact
    /// shapes <see cref="HarnessContextEntry.NormalizeValue"/> supports. Fails closed —
    /// <see cref="NotSupportedException"/> — for any other type rather than guessing at its size.
    /// </summary>
    private static int EstimateValueSize(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case string s:
                return Utf8ByteCount(s);
            case bool b:
                return Utf8ByteCount(b ? "true" : "false");
            case int or long or short or byte or uint or ulong or double or float or decimal:
                return Utf8ByteCount(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            case JsonElement je:
                return Utf8ByteCount(je.GetRawText());
            case IDictionary<string, object?> dict:
                {
                    var size = 0;
                    foreach (var kvp in dict)
                    {
                        size += Utf8ByteCount(kvp.Key) + EstimateValueSize(kvp.Value);
                    }

                    return size;
                }

            case IList<object?> list:
                {
                    var size = 0;
                    foreach (var item in list)
                    {
                        size += EstimateValueSize(item);
                    }

                    return size;
                }

            default:
                throw new NotSupportedException(
                    $"Type '{value.GetType()}' is not a supported normalized value shape for size " +
                    "estimation. This mirrors HarnessContextEntry.NormalizeValue's supported shapes: " +
                    "null, string, common primitive value types, JsonElement, " +
                    "IDictionary<string, object?>, and IList<object?> (including arrays).");
        }
    }

    private static int Utf8ByteCount(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : Encoding.UTF8.GetByteCount(text);
}
