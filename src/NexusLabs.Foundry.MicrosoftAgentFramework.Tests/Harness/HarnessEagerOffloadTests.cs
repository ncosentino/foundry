// Tests intentionally exercise HarnessToolResultOffloadTransform's explicit CancellationToken
// parameter (CancellationToken.None) directly. This is the behavior under test, not an oversight
// of TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using Moq;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Capabilities;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Providers;
using NexusLabs.Foundry.MicrosoftAgentFramework.Iterative;
using NexusLabs.Foundry.MicrosoftAgentFramework.Tools;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for the shared, caller-agnostic eager tool-result offload transform:
/// <see cref="HarnessToolResultOffloadTransform"/> directly, and both of its production seams
/// (<see cref="IterativeAgentLoop"/> and selected-provider <see cref="HarnessProviderComposition"/>'s
/// FICC <c>FunctionInvoker</c>).
/// </summary>
public sealed class HarnessEagerOffloadTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // --- Transform: exact threshold boundary ------------------------------------------------

    [Fact]
    public void Transform_ExactlyAtThreshold_Inlines()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('a', 100);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);
        var request = CreateRequest(fixture, content, policy);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Inline, outcome.Status);
        Assert.Equal(content, outcome.InlineText);
        Assert.Same(content, outcome.RawResult);
        Assert.Equal(0, fixture.Workspace.WriteFileCallCount);
    }

    [Fact]
    public void Transform_ThresholdPlusOne_Offloads()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('a', 101);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 100);
        var request = CreateRequest(fixture, content, policy);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Offloaded, outcome.Status);
        Assert.NotNull(outcome.Reference);
        var expectedDigest = HarnessArtifactIdentity.ComputeDigest(content);
        Assert.Equal(expectedDigest, outcome.Reference!.ContentDigest);
        Assert.Equal($"artifact://sha256/{expectedDigest}", outcome.ReferenceText);
        Assert.Equal(1, fixture.Workspace.WriteFileCallCount);
        Assert.Equal(content, fixture.Workspace.TryReadFile(outcome.Reference.WorkspacePath).Value.Content);
        Assert.DoesNotContain(content, outcome.ReferenceText);
    }

    [Fact]
    public void Transform_SmallNonStringResult_ReturnsOriginalRawResultUnchanged()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var raw = new { Value = "small" };
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 10_000);
        var request = CreateRequest(fixture, raw, policy);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Inline, outcome.Status);
        Assert.Same(raw, outcome.RawResult);
    }

    // --- Transform: recoverable context segment bypass ---------------------------------------

    [Fact]
    public void Transform_RecoverableContextSegment_AlwaysInlinesBodyBypassingThreshold_NeverWritesAgain()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var largeBody = new string('z', 100_000);
        var reference = fixture.CreateReference(largeBody, CreatedAtUtc);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, largeBody, CreatedAtUtc);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 1);
        var request = CreateRequest(fixture, segment, policy);
        var writesBefore = fixture.Workspace.WriteFileCallCount;

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Inline, outcome.Status);
        Assert.Equal(largeBody, outcome.InlineText);
        Assert.Equal(largeBody, outcome.RawResult);
        Assert.Equal(writesBefore, fixture.Workspace.WriteFileCallCount);
    }

    // --- Transform: no authorized workspace fails closed --------------------------------------

    [Fact]
    public void Transform_OversizedWithNoExecutionBinding_FailsClosed_NeverInlinesOrTruncates()
    {
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            "no-binding-session",
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var oversized = new string('a', 50);
        var request = new HarnessToolResultOffloadRequest(
            oversized,
            "tool",
            "call-1",
            ExecutionBinding: null,
            ExecutionContextAccessor: null,
            policy,
            CreatedAtUtc,
            CancellationToken.None,
            ProgressAccessor: null);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        Assert.NotNull(outcome.Evidence);
        Assert.Contains("NoAuthorizedWorkspace", outcome.Evidence);
        Assert.Contains("NoBinding", outcome.Evidence);
        Assert.Contains("50", outcome.Evidence);  // observed byte count
        Assert.Contains("10", outcome.Evidence);  // configured threshold
        Assert.Null(outcome.Reference);
        Assert.DoesNotContain(oversized, outcome.Evidence);
    }

    [Fact]
    public void Transform_OversizedWithNoBinding_Evidence_IsBoundedAndContainsCategoricalCounts_NotOversizedIdentifiers()
    {
        // Deliberately long tool name and call ID — well over the 64-char evidence bound.
        var longToolName = new string('T', 200);
        var longCallId = new string('C', 200);
        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            "bounded-evidence-session",
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var oversized = new string('a', 50);
        var request = new HarnessToolResultOffloadRequest(
            oversized,
            longToolName,
            longCallId,
            ExecutionBinding: null,
            ExecutionContextAccessor: null,
            policy,
            CreatedAtUtc,
            CancellationToken.None,
            ProgressAccessor: null);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        Assert.NotNull(outcome.Evidence);
        // Categorical reason and counts must be present.
        Assert.Contains("NoAuthorizedWorkspace", outcome.Evidence);
        Assert.Contains("50", outcome.Evidence);   // observed UTF-8 byte count
        Assert.Contains("10", outcome.Evidence);   // configured threshold
        // Full 200-char identifiers must not appear — evidence is bounded to 64 chars each.
        Assert.DoesNotContain(longToolName, outcome.Evidence);
        Assert.DoesNotContain(longCallId, outcome.Evidence);
        // Raw content must not appear.
        Assert.DoesNotContain(oversized, outcome.Evidence);
    }

    [Fact]
    public void Transform_OversizedWithBindingButNoWorkspace_FailsClosed()
    {
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(new AgentExecutionContext("user-1", "orch-1"));
        var capture = HarnessExecutionBinding.Capture(accessor, "no-workspace-session", requireWorkspace: false);
        Assert.Equal(HarnessExecutionBindingStatus.Valid, capture.Status);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);
        Assert.Null(binding.Workspace);

        var policy = HarnessToolResultOffloadPolicy.Create(
            10,
            "no-workspace-session",
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var oversized = new string('b', 50);
        var request = new HarnessToolResultOffloadRequest(
            oversized,
            "tool",
            "call-1",
            binding,
            accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None,
            ProgressAccessor: null);

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        Assert.Contains("NoAuthorizedWorkspace", outcome.Evidence);
        Assert.Null(outcome.Reference);
    }

    // --- Transform: content-addressed existing path ------------------------------------------

    [Fact]
    public void Transform_ExistingMatchingContent_ReturnsExistingReferenceWithoutWriting()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('c', 200);
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var path = HarnessArtifactIdentity.BuildPath(digest);
        fixture.Workspace.TryWriteFile(path, content);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 10);
        var request = CreateRequest(fixture, content, policy);
        var writesBefore = fixture.Workspace.WriteFileCallCount;

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.ExistingReference, outcome.Status);
        Assert.Equal(digest, outcome.Reference!.ContentDigest);
        Assert.Equal(writesBefore, fixture.Workspace.WriteFileCallCount);
    }

    [Fact]
    public void Transform_ExistingMismatchedContent_FailsClosed_NeverOverwritesCorruption()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var content = new string('d', 200);
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var path = HarnessArtifactIdentity.BuildPath(digest);
        const string corrupted = "corrupted content that does not match the expected digest";
        fixture.Workspace.TryWriteFile(path, corrupted);
        var policy = CreatePolicy(fixture, maximumInlineToolResultBytes: 10);
        var request = CreateRequest(fixture, content, policy);
        var writesBefore = fixture.Workspace.WriteFileCallCount;

        var outcome = HarnessToolResultOffloadTransform.Transform(request);

        Assert.Equal(HarnessToolResultOffloadStatus.Failed, outcome.Status);
        Assert.Contains("ContentAddressMismatch", outcome.Evidence);
        Assert.Equal(corrupted, fixture.Workspace.TryReadFile(path).Value.Content);
        Assert.Equal(writesBefore, fixture.Workspace.WriteFileCallCount);
    }

    // --- Iterative seam ------------------------------------------------------------------------

    [Fact]
    public async Task IterativeLoop_OversizedToolResult_MapsToBoundedReference_NoRawMarkerInMessages()
    {
        // AIFunctionFactory.Create always JSON round-trips return values (including plain strings)
        // into a JsonElement before FICC ever sees them, so ToolResultSerializer.Serialize renders
        // this as its raw JSON text (quoted). The marker must be long enough that even the quoted
        // form exceeds the byte threshold below.
        const string oversizedMarker = "OVERSIZED-ITERATIVE-MARKER-" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";
        var quotedMarker = $"\"{oversizedMarker}\"";
        var invocationCount = 0;
        var tool = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return oversizedMarker;
            },
            new AIFunctionFactoryOptions { Name = "big_tool" });

        var capturedMessages = new List<IEnumerable<ChatMessage>>();
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                capturedMessages.Add(messages.ToList());
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new ChatResponse(
                        new ChatMessage(ChatRole.Assistant,
                            [new FunctionCallContent("call-1", "big_tool", new Dictionary<string, object?>())])));
                }

                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
            });

        var accessorMock = new Mock<IChatClientAccessor>();
        accessorMock.Setup(a => a.ChatClient).Returns(mockChat.Object);
        var contextAccessor = new AgentExecutionContextAccessor();
        var loop = new IterativeAgentLoop(accessorMock.Object, executionContextAccessor: contextAccessor);

        var policy = HarnessToolResultOffloadPolicy.Create(
            100,
            "iterative-offload-session",
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            PromptFactory = _ => "go",
            Tools = [tool],
            OffloadPolicy = policy,
        };
        var workspace = new InMemoryWorkspace();
        var context = new IterativeContext { Workspace = workspace };

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, invocationCount);
        Assert.Equal(2, capturedMessages.Count);

        var toolResultContent = capturedMessages[1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single(c => c.CallId == "call-1");
        var resultText = Assert.IsType<string>(toolResultContent.Result);

        Assert.DoesNotContain(oversizedMarker, resultText);
        var expectedDigest = HarnessArtifactIdentity.ComputeDigest(quotedMarker);
        Assert.Equal($"artifact://sha256/{expectedDigest}", resultText);
        Assert.True(workspace.FileExists(HarnessArtifactIdentity.BuildPath(expectedDigest)));
        Assert.Equal(
            quotedMarker,
            workspace.TryReadFile(HarnessArtifactIdentity.BuildPath(expectedDigest)).Value.Content);
    }

    [Fact]
    public async Task IterativeLoop_SmallToolResult_BehaviorUnchangedWhenOffloadPolicyConfigured()
    {
        var tool = AIFunctionFactory.Create(() => "ok", new AIFunctionFactoryOptions { Name = "small_tool" });

        var capturedMessages = new List<IEnumerable<ChatMessage>>();
        var callCount = 0;
        var mockChat = new Mock<IChatClient>();
        mockChat
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? _, CancellationToken _) =>
            {
                capturedMessages.Add(messages.ToList());
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new ChatResponse(
                        new ChatMessage(ChatRole.Assistant,
                            [new FunctionCallContent("call-1", "small_tool", new Dictionary<string, object?>())])));
                }

                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
            });

        var accessorMock = new Mock<IChatClientAccessor>();
        accessorMock.Setup(a => a.ChatClient).Returns(mockChat.Object);
        var contextAccessor = new AgentExecutionContextAccessor();
        var loop = new IterativeAgentLoop(accessorMock.Object, executionContextAccessor: contextAccessor);

        var policy = HarnessToolResultOffloadPolicy.Create(
            100_000,
            "iterative-offload-session-2",
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);
        var options = new IterativeLoopOptions
        {
            Instructions = "test",
            PromptFactory = _ => "go",
            Tools = [tool],
            OffloadPolicy = policy,
        };
        var context = new IterativeContext { Workspace = new InMemoryWorkspace() };

        var result = await loop.RunAsync(options, context, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var toolResultContent = capturedMessages[1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single(c => c.CallId == "call-1");
        // AIFunctionFactory.Create round-trips "ok" through JSON before FICC sees it, so the raw
        // result is a JsonElement whose ToolResultSerializer.Serialize rendering is its raw JSON
        // text ("\"ok\""), identical to the pre-existing (non-offload) serialization behavior —
        // this is what "unchanged" means here, not a literal unquoted "ok".
        Assert.Equal("\"ok\"", toolResultContent.Result);
    }

    // --- Selected-provider seam ------------------------------------------------------------------

    [Fact]
    public async Task SelectedProvider_OversizedToolResult_MapsToBoundedReference_SameDigestAsIterativeSeam()
    {
        // AIFunctionFactory.Create always JSON round-trips return values into a JsonElement before
        // FICC ever sees them, so the marker must be long enough that even the quoted form exceeds
        // the byte threshold below (mirrors the iterative-seam test above).
        const string oversizedMarker = "OVERSIZED-SELECTED-PROVIDER-MARKER-" +
            "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789";
        var quotedMarker = $"\"{oversizedMarker}\"";
        var invocationCount = 0;
        var function = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return oversizedMarker;
            },
            "big_tool");

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var offloadPlugin = HarnessToolResultOffloadPlugin.Create(100);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: null,
                offloadPlugin: offloadPlugin);
            var composition = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);

            var response = await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken);

            var expectedDigest = HarnessArtifactIdentity.ComputeDigest(quotedMarker);
            var expectedReferenceText = $"artifact://sha256/{expectedDigest}";

            Assert.Equal(1, invocationCount);
            Assert.Equal(expectedReferenceText, response.GetText());
            Assert.DoesNotContain(oversizedMarker, response.GetText());
            Assert.True(binding.Workspace!.FileExists(HarnessArtifactIdentity.BuildPath(expectedDigest)));
        }
    }

    [Fact]
    public async Task SelectedProvider_RecoverableSegmentAsRawResult_HonorsSkipEagerOffload_InlinesWithoutWriting()
    {
        using var artifactFixture = HarnessArtifactTestFixture.Create();
        var body = new string('r', 5_000);
        var reference = artifactFixture.CreateReference(body, CreatedAtUtc);
        var segment = HarnessArtifactRecoverableContextSegment.Create(reference, body, CreatedAtUtc);

        // Uses HarnessRawResultFunction (an AIFunction subclass), not AIFunctionFactory.Create,
        // because AIFunctionFactory always JSON round-trips return values into a JsonElement before
        // FICC ever sees them — which would strip the segment's CLR type before the transform's
        // recoverable-segment check could ever observe it. A hand-authored rehydration tool in
        // production must preserve the raw segment the same way to reach this bypass at all.
        var invocationCount = 0;
        var function = new HarnessRawResultFunction(
            "rehydrate_tool",
            segment,
            onInvoked: () => invocationCount++);

        using var services = HarnessCompositionTestFixture.CreateServices();
        var accessor = new AgentExecutionContextAccessor();
        var binding = HarnessCompositionTestFixture.CaptureBinding(accessor, out var scope);
        using (scope)
        {
            var chatClient = new HarnessScriptedChatClient(function.Name);
            var offloadPlugin = HarnessToolResultOffloadPlugin.Create(1);
            var request = HarnessCompositionTestFixture.CreateRequest(
                chatClient,
                services,
                HarnessCompositionTestFixture.CreateProfile(
                    HarnessToolLoopOwner.Harness,
                    HarnessTelemetryOwner.Harness),
                HarnessCompositionTestFixture.CreateToolResolution(function),
                binding,
                accessor,
                metrics: null,
                historyProvider: null,
                planningProviders: null,
                approvalPlugin: null,
                skillsPlugin: null,
                progressAccessor: null,
                webSearchPlugin: null,
                offloadPlugin: offloadPlugin);
            var composition = new HarnessProviderComposition().Compose(request);
            var agent = Assert.IsAssignableFrom<AIAgent>(composition.Agent);
            var writesBefore = artifactFixture.Workspace.WriteFileCallCount;

            var response = await agent.RunAsync(
                "run",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(1, invocationCount);
            Assert.Equal(body, response.GetText());
            Assert.Equal(writesBefore, artifactFixture.Workspace.WriteFileCallCount);
        }
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static HarnessToolResultOffloadPolicy CreatePolicy(
        HarnessArtifactTestFixture fixture,
        int maximumInlineToolResultBytes) =>
        HarnessToolResultOffloadPolicy.Create(
            maximumInlineToolResultBytes,
            fixture.SessionId,
            HarnessToolResultOffloadDescriptions.Default,
            checkpoint: null);

    private static HarnessToolResultOffloadRequest CreateRequest(
        HarnessArtifactTestFixture fixture,
        object? rawResult,
        HarnessToolResultOffloadPolicy policy) =>
        new(
            rawResult,
            HarnessArtifactTestFixture.DefaultToolName,
            HarnessArtifactTestFixture.DefaultCallId,
            fixture.Binding,
            fixture.Accessor,
            policy,
            CreatedAtUtc,
            CancellationToken.None,
            ProgressAccessor: null);
}
