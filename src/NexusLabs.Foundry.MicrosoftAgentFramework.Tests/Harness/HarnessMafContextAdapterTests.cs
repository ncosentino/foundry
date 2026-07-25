using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

public sealed class HarnessMafContextAdapterTests
{
    [Fact]
    public void Adapt_ToolCallMessage_IsAlwaysToolExchangeRegardlessOfClassifierOverride()
    {
        var classifier = new HarnessScriptedMessageClassifier(
            classifyOverride: (_, _, _) => HarnessContextEntryKind.ConversationalMessage);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "tool", new Dictionary<string, object?>())]),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        Assert.Equal(HarnessContextEntryKind.ToolExchange, Assert.Single(entries).Kind);
    }

    [Fact]
    public void Adapt_ToolResultMessage_IsAlwaysToolExchange()
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result")]),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        Assert.Equal(HarnessContextEntryKind.ToolExchange, Assert.Single(entries).Kind);
    }

    [Fact]
    public void Adapt_CanonicalArtifactReferenceText_IsStructurallyRecognizedWithoutClassifierOverride()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("artifact body");
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.Tool, HarnessArtifactIdentity.BuildReferenceId(digest)),
        };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        var entry = Assert.Single(entries);
        Assert.Equal(HarnessContextEntryKind.ArtifactReference, entry.Kind);
        Assert.Equal(digest, entry.ArtifactReferenceDigest);
    }

    [Fact]
    public void Adapt_ClassifierOverride_AssignsRequestedNonStructuralKind()
    {
        var classifier = new HarnessScriptedMessageClassifier(
            classifyOverride: (_, _, _) => HarnessContextEntryKind.AuthoritativeSessionState);
        var messages = new List<ChatMessage> { new(ChatRole.User, "structured state payload") };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        Assert.Equal(HarnessContextEntryKind.AuthoritativeSessionState, Assert.Single(entries).Kind);
    }

    [Theory]
    [InlineData((int)HarnessContextEntryKind.ToolExchange)]
    [InlineData((int)HarnessContextEntryKind.RecoverableContextSegment)]
    [InlineData((int)HarnessContextEntryKind.SystemInstruction)]
    public void Adapt_ClassifierOverride_RejectsStructuralOnlyKinds(int forbiddenKindValue)
    {
        var forbiddenKind = (HarnessContextEntryKind)forbiddenKindValue;
        var classifier = new HarnessScriptedMessageClassifier(classifyOverride: (_, _, _) => forbiddenKind);
        var messages = new List<ChatMessage> { new(ChatRole.User, "plain text") };

        Assert.Throws<InvalidOperationException>(() => HarnessMafMessageContextAdapter.Adapt(messages, classifier));
    }

    [Fact]
    public void Adapt_NoOverrideNoStructuralShape_DefaultsToConversationalMessage()
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage> { new(ChatRole.User, "hello") };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        Assert.Equal(HarnessContextEntryKind.ConversationalMessage, Assert.Single(entries).Kind);
    }

    [Fact]
    public void Adapt_EntryId_IsDeterministicOverIdenticalContentAcrossSeparateAdaptations()
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var first = new List<ChatMessage> { new(ChatRole.User, "repeat me") };
        var second = new List<ChatMessage> { new(ChatRole.User, "repeat me") };

        var firstEntries = HarnessMafMessageContextAdapter.Adapt(first, classifier);
        var secondEntries = HarnessMafMessageContextAdapter.Adapt(second, classifier);

        Assert.Equal(firstEntries[0].EntryId, secondEntries[0].EntryId);
    }

    // --- System-role messages always yield SystemInstruction; classifier is never consulted --------

    [Fact]
    public void Adapt_SystemRoleMessage_IsAlwaysSystemInstruction_WhenClassifierReturnsNull()
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage> { new(ChatRole.System, "system prompt") };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        Assert.Equal(HarnessContextEntryKind.SystemInstruction, Assert.Single(entries).Kind);
    }

    [Theory]
    [InlineData((int)HarnessContextEntryKind.ConversationalMessage)]
    [InlineData((int)HarnessContextEntryKind.Summary)]
    [InlineData((int)HarnessContextEntryKind.OptionalContext)]
    [InlineData((int)HarnessContextEntryKind.AuthoritativeSessionState)]
    public void Adapt_SystemRoleMessage_ClassifierOverrideIsIgnoredInFavorOfSystemInstruction(int ignoredKindValue)
    {
        var ignoredKind = (HarnessContextEntryKind)ignoredKindValue;
        var classifier = new HarnessScriptedMessageClassifier(classifyOverride: (_, _, _) => ignoredKind);
        var messages = new List<ChatMessage> { new(ChatRole.System, "system instructions") };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        // ClassifyOverride is never consulted for system-role messages: the return value is
        // never observed and the result is always SystemInstruction.
        Assert.Equal(HarnessContextEntryKind.SystemInstruction, Assert.Single(entries).Kind);
    }

    [Fact]
    public void Adapt_SystemRoleMessage_SurvivesWithNoOpClassifier_MessageTextPreserved()
    {
        var classifier = new HarnessScriptedMessageClassifier();
        var messages = new List<ChatMessage> { new(ChatRole.System, "pinned system instructions") };

        var entries = HarnessMafMessageContextAdapter.Adapt(messages, classifier);

        var entry = Assert.Single(entries);
        Assert.Equal(HarnessContextEntryKind.SystemInstruction, entry.Kind);
        Assert.Equal("pinned system instructions", entry.Message.Text);
    }
}
