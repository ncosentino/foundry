using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Real-shape coverage for G4 offloaded artifact references surfacing inside tool-call history: a
/// <see cref="FunctionResultContent.Result"/> that is itself the canonical <c>artifact://sha256/{digest}</c>
/// string produced by eager offload. Such a message must remain a <see cref="HarnessContextEntryKind.ToolExchange"/>
/// entry for tool-call/result sequence validation, while also structurally exposing the digest it
/// carries so the preservation policy, snapshot augmentation, and eviction logic can all treat it as
/// durable, reference-bearing context — exactly as if a standalone
/// <see cref="HarnessContextEntryKind.ArtifactReference"/> entry carried the same digest.
/// </summary>
public sealed class HarnessArtifactReferenceInToolResultTests
{
    private static readonly HarnessUtf8ContextSizeEstimator DefaultEstimator = new();

    // --- Adapter: real assistant call + tool result(reference) shape ------------------------

    [Fact]
    public void Adapt_ToolResultWithCanonicalReferenceString_IsToolExchange_WithDigestMetadata()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("g4-offloaded-artifact-body");
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "G4Tool", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", HarnessArtifactIdentity.BuildReferenceId(digest))]),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        Assert.Equal(2, entries.Count);
        var callEntry = entries[0];
        var resultEntry = entries[1];

        // Still ToolExchange for sequence validation — never reclassified as a standalone
        // ArtifactReference entry, since it is structurally a tool result.
        Assert.Equal(HarnessContextEntryKind.ToolExchange, callEntry.Kind);
        Assert.Equal(HarnessContextEntryKind.ToolExchange, resultEntry.Kind);

        // The call-bearing entry carries no digests (arguments are never inspected); the
        // result-bearing entry structurally exposes the exact digest its Result carries.
        Assert.Empty(callEntry.ArtifactReferenceDigests);
        Assert.Equal([digest], resultEntry.ArtifactReferenceDigests);
        Assert.Equal(digest, resultEntry.ArtifactReferenceDigest);
    }

    [Fact]
    public void Adapt_ToolResultWithJsonElementCanonicalReference_ExposesDigestMetadata()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("g4-offloaded-artifact-body-json");
        var referenceId = HarnessArtifactIdentity.BuildReferenceId(digest);
        var element = System.Text.Json.JsonSerializer.SerializeToElement(referenceId);
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Tool, [new FunctionResultContent("call-1", element)]),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        var resultEntry = Assert.Single(entries);
        Assert.Equal(HarnessContextEntryKind.ToolExchange, resultEntry.Kind);
        Assert.Equal([digest], resultEntry.ArtifactReferenceDigests);
    }

    [Theory]
    [InlineData("not-a-reference-at-all")]
    [InlineData("/workspace/artifacts/sha256/deadbeef")]
    [InlineData("artifact://sha256/tooshort")]
    public void Adapt_ToolResultWithNonCanonicalPayload_NeverCountedAsReference(string bareResult)
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Tool, [new FunctionResultContent("call-1", bareResult)]),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        var resultEntry = Assert.Single(entries);
        Assert.Equal(HarnessContextEntryKind.ToolExchange, resultEntry.Kind);
        Assert.Empty(resultEntry.ArtifactReferenceDigests);
        Assert.Null(resultEntry.ArtifactReferenceDigest);
    }

    [Fact]
    public void Adapt_ToolResultWithNonStringPayload_NeverCountedAsReference()
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Tool, [new FunctionResultContent("call-1", 42)]),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        var resultEntry = Assert.Single(entries);
        Assert.Empty(resultEntry.ArtifactReferenceDigests);
    }

    // --- Snapshot augmentation: a ToolExchange result entry alone can back a recoverable body -

    [Fact]
    public void WithRecoverableSegment_MatchingDigestOnlyInToolExchangeResult_Succeeds()
    {
        var contentSeed = "augmentation-tool-result-artifact";
        var digest = HarnessArtifactIdentity.ComputeDigest(contentSeed);
        var reference = HarnessCompactionTestFixture.SampleReference(contentSeed, DateTimeOffset.UnixEpoch);
        var segment = HarnessArtifactRecoverableContextSegment.Create(
            reference, "the recovered body", DateTimeOffset.UnixEpoch);

        var baseline = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultReferenceEntry("tool-result", "call-1", digest),
        };

        var augmented = HarnessContextSnapshotAugmentation.WithRecoverableSegment(baseline, "recovered", segment);

        Assert.Equal(4, augmented.Count);
        var recoveredEntry = augmented[^1];
        Assert.Equal(HarnessContextEntryKind.RecoverableContextSegment, recoveredEntry.Kind);
        Assert.Equal("the recovered body", recoveredEntry.Message.Text);
    }

    [Fact]
    public void WithRecoverableSegment_NoMatchingDigestAnywhere_Throws()
    {
        var contentSeed = "augmentation-no-matching-reference";
        var reference = HarnessCompactionTestFixture.SampleReference(contentSeed, DateTimeOffset.UnixEpoch);
        var segment = HarnessArtifactRecoverableContextSegment.Create(
            reference, "the recovered body", DateTimeOffset.UnixEpoch);

        // The tool result here is an ordinary, non-reference-bearing payload — it must never be
        // (mis)treated as backing the recoverable segment's digest.
        var baseline = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("tool-result", ("call-1", "an ordinary result value")),
        };

        Assert.Throws<ArgumentException>(
            () => HarnessContextSnapshotAugmentation.WithRecoverableSegment(baseline, "recovered", segment));
    }

    // --- Preservation: whole complete tool-exchange group required atomically, outside recency

    [Fact]
    public void SelectRequiredPreservation_OldCompleteToolExchangeWithReferenceResult_WholeGroupRequired()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("preservation-old-tool-result-artifact");
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "system instructions"),
            // Old, reference-bearing tool exchange, well outside the trailing 1-recency-unit window.
            HarnessCompactionTestFixture.ToolCallEntry("old-tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultReferenceEntry("old-tool-result", "call-1", digest),
            HarnessCompactionTestFixture.ConversationalEntry("old-conv", ChatRole.User, "old filler message"),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.Assistant, "recent message"),
        };

        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);

        Assert.Contains("old-tool-call", selection.RequiredEntryIds);
        Assert.Contains("old-tool-result", selection.RequiredEntryIds);
        Assert.Contains("recent-conv", selection.RequiredEntryIds);
        Assert.DoesNotContain("old-conv", selection.RequiredEntryIds);
    }

    [Fact]
    public void SelectRequiredPreservation_OldCompleteToolExchangeWithoutReferenceResult_NotRequiredOutsideRecency()
    {
        // Control: an otherwise-identical old, complete tool exchange whose result carries no
        // reference is an ordinary reducible tool exchange — never required merely for being a
        // complete tool exchange.
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 1, 1, DefaultEstimator);
        var original = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "system instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("old-tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("old-tool-result", ("call-1", "an ordinary result value")),
            HarnessCompactionTestFixture.ConversationalEntry("recent-conv", ChatRole.Assistant, "recent message"),
        };

        var selection = policy.SelectRequiredPreservation(original, CancellationToken.None);

        Assert.DoesNotContain("old-tool-call", selection.RequiredEntryIds);
        Assert.DoesNotContain("old-tool-result", selection.RequiredEntryIds);
    }

    // --- Eviction: recoverable body evicts down to a tool-result reference -------------------

    [Fact]
    public async Task AssembleAsync_RecoverableSegmentBackedByToolResultReference_EvictedBeforeReducer()
    {
        var reference = HarnessCompactionTestFixture.SampleReference("tool-result-artifact-body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 20, ["tool-call"] = 5, ["tool-result"] = 10, ["recoverable"] = 200,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(60, 5, 1, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultReferenceEntry("tool-result", "call-1", reference.ContentDigest),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "recoverable", reference, "the recovered body", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, reducer) = HarnessAssemblerTestFixture.Build(
            policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HarnessContextAssemblyOutcome.Reduced, result.Outcome);
        Assert.Equal(["system", "tool-call", "tool-result"], result.FinalEntries!.Select(e => e.EntryId));
        Assert.Equal(0, reducer.InvocationCount);
        Assert.Contains(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
    }

    [Fact]
    public async Task AssembleAsync_RecoverableSegmentWithNoMatchingReferenceAnywhere_NeverEvicted()
    {
        // The tool result here carries an ordinary, non-reference payload, so it can never back the
        // recoverable segment's digest — the body must be kept rather than silently discarded.
        var reference = HarnessCompactionTestFixture.SampleReference("orphan-artifact-body", DateTimeOffset.UnixEpoch);
        var sizes = new Dictionary<string, int>
        {
            ["system"] = 20, ["tool-call"] = 5, ["tool-result"] = 10, ["recoverable"] = 20,
        };
        var policy = HarnessCompactionTestFixture.CreatePolicy(1_000, 100, 5, 1, new HarnessFixedSizeContextEstimator(sizes));
        var entries = new HarnessContextEntry[]
        {
            HarnessCompactionTestFixture.SystemEntry("system", "instructions"),
            HarnessCompactionTestFixture.ToolCallEntry("tool-call", ("call-1", "lookup")),
            HarnessCompactionTestFixture.ToolResultEntry("tool-result", ("call-1", "an ordinary result value")),
            HarnessCompactionTestFixture.RecoverableSegmentEntry(
                "recoverable", reference, "the recovered body", DateTimeOffset.UnixEpoch),
        };

        var (assembler, _, _) = HarnessAssemblerTestFixture.Build(
            policy, entries, HarnessAssemblerTestFixture.Unchanged);

        var result = await assembler.AssembleAsync(TestContext.Current.CancellationToken);

        Assert.Contains("recoverable", result.FinalEntries!.Select(e => e.EntryId));
        Assert.DoesNotContain(HarnessContextAssemblyStage.RecoverableBodyEviction, result.Stages);
    }
}
