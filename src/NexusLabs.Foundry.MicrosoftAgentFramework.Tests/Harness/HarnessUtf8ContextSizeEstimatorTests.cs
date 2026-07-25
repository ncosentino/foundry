using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

// Tests intentionally exercise explicit CancellationToken parameters (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

/// <summary>
/// Tests for <see cref="HarnessUtf8ContextSizeEstimator"/>: unlike <see cref="HarnessUtf8TextSizeEstimator"/>,
/// it accounts for a <see cref="FunctionCallContent"/>'s call id, name, and normalized arguments, and a
/// <see cref="FunctionResultContent"/>'s call id and normalized result payload — not just
/// <see cref="TextContent"/> text — so a tool-heavy entry can never be silently ignored by a
/// <see cref="HarnessHybridContextPolicy"/> trigger decision.
/// </summary>
public sealed class HarnessUtf8ContextSizeEstimatorTests
{
    private static readonly HarnessUtf8ContextSizeEstimator Estimator = new();

    [Fact]
    public void EstimateSize_NullEntry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Estimator.EstimateSize(null!));
    }

    // --- Text content: identical to the text-only estimator for a plain text message ---------

    [Fact]
    public void EstimateSize_AsciiTextContent_ReturnsUtf8ByteCount()
    {
        var entry = HarnessCompactionTestFixture.SystemEntry("system", "hello");

        Assert.Equal(5, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_MultiByteUtf8TextContent_CountsBytesNotCharacters()
    {
        // "café": c, a, f are 1 byte each; é is 2 bytes in UTF-8 -> 5 bytes total for 4 characters.
        var entry = HarnessCompactionTestFixture.SystemEntry("system", "café");

        Assert.Equal(5, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_EmptyMessageText_ReturnsZero()
    {
        var entry = HarnessCompactionTestFixture.SystemEntry("system", string.Empty);

        Assert.Equal(0, Estimator.EstimateSize(entry));
    }

    // --- Tool-heavy content: call id/name/arguments and result id/payload are counted ---------

    [Fact]
    public void EstimateSize_FunctionCallWithEmptyArguments_CountsOnlyCallIdAndName()
    {
        var entry = HarnessCompactionTestFixture.ToolCallEntry("call", ("id-1", "lookup"));

        Assert.Equal("id-1".Length + "lookup".Length, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_FunctionCallWithStringArgument_IncludesArgumentKeyAndValueBytes()
    {
        var arguments = new Dictionary<string, object?> { ["query"] = "hello world" };
        var call = new FunctionCallContent("call-id", "search", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        var expected = "call-id".Length + "search".Length + "query".Length + "hello world".Length;
        Assert.Equal(expected, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_FunctionResultWithStringPayload_IncludesCallIdAndPayloadBytes()
    {
        var entry = HarnessCompactionTestFixture.ToolResultEntry(
            "result", ("call-id", "a fairly long tool result payload"));

        var expected = "call-id".Length + "a fairly long tool result payload".Length;
        Assert.Equal(expected, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_FunctionResultWithNullPayload_CountsOnlyCallId()
    {
        var entry = HarnessCompactionTestFixture.ToolResultEntry("result", ("call-id", null));

        Assert.Equal("call-id".Length, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_NestedDictionaryAndListArguments_SumsRecursively()
    {
        var arguments = new Dictionary<string, object?>
        {
            ["items"] = new List<object?> { "alpha", "beta" },
            ["nested"] = new Dictionary<string, object?> { ["inner"] = "gamma" },
        };
        var call = new FunctionCallContent("call-id", "batch", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        var expected = "call-id".Length + "batch".Length
            + "items".Length + "alpha".Length + "beta".Length
            + "nested".Length + "inner".Length + "gamma".Length;
        Assert.Equal(expected, Estimator.EstimateSize(entry));
    }

    [Fact]
    public void EstimateSize_JsonElementArgument_CountsRawTextBytes()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        var arguments = new Dictionary<string, object?> { ["payload"] = doc.RootElement };
        var call = new FunctionCallContent("call-id", "op", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        var rawTextByteCount = Encoding.UTF8.GetByteCount(doc.RootElement.GetRawText());
        var expected = "call-id".Length + "op".Length + "payload".Length + rawTextByteCount;
        Assert.Equal(expected, Estimator.EstimateSize(entry));
    }

    // --- Hardening rationale: a text-only estimator would miss a tool-payload-dominated entry --

    [Fact]
    public void EstimateSize_ToolExchangeDominatedByLargeArguments_TriggersCompactionUnlikeTextOnlyEstimator()
    {
        var largeArgument = new string('x', 200);
        var arguments = new Dictionary<string, object?> { ["payload"] = largeArgument };
        var call = new FunctionCallContent("call-id", "op", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        // The text-only estimator has no TextContent to measure here, so it reports zero: a policy
        // configured with it could never trigger on this oversized tool exchange.
        var textOnlyEstimator = new HarnessUtf8TextSizeEstimator();
        Assert.Equal(0, textOnlyEstimator.EstimateSize(entry));

        var policy = HarnessCompactionTestFixture.CreatePolicy(100, 10, 1, 1, Estimator);
        var evaluation = policy.Evaluate([entry], CancellationToken.None);

        Assert.True(evaluation.EstimatedSize >= 200);
        Assert.True(evaluation.Triggered);
    }

    // --- Unsupported types fail closed, mirroring HarnessContextEntry.NormalizeValue ----------

    [Fact]
    public void Create_ThenEstimateSize_NeverSeesAnUnsupportedArgumentType()
    {
        // HarnessContextEntry.Create itself already throws NotSupportedException for an unsupported
        // argument/result type (see HarnessContextEntryValidationTests), so by the time an entry
        // exists at all, every argument/result value is guaranteed to be one of the explicit shapes
        // this estimator supports.
        var arguments = new Dictionary<string, object?> { ["value"] = 42 };
        var call = new FunctionCallContent("call-id", "op", arguments);
        var entry = HarnessContextEntry.Create(
            "call", HarnessContextEntryKind.ToolExchange,
            new ChatMessage(ChatRole.Assistant, new List<AIContent> { call }));

        var expected = "call-id".Length + "op".Length + "value".Length + "42".Length;
        Assert.Equal(expected, Estimator.EstimateSize(entry));
    }
}
