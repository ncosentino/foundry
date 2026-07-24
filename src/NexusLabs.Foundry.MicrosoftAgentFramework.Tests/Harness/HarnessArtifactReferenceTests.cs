// Tests intentionally exercise the resolver's explicit CancellationToken parameter (including
// CancellationToken.None) directly. This is the behavior under test, not an oversight of
// TestContext.Current.CancellationToken.
#pragma warning disable xUnit1051

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Tests for <see cref="HarnessArtifactReference"/>, <see cref="HarnessArtifactIdentity"/>, and the
/// <see cref="HarnessArtifactResolver"/> outcomes reachable purely through reference/content
/// identity (T046): deterministic digest/path, "same content produces the same reference", the
/// <c>Reconstruct</c> validation contract for untrusted/serialized reference data, and the
/// <c>Resolved</c>/<c>Stale</c>/<c>Missing</c> resolver outcomes driven by workspace content state.
/// </summary>
public sealed class HarnessArtifactReferenceTests
{
    private const string ArtifactRoot = HarnessArtifactIdentity.DefaultArtifactRoot;
    private const int DefaultMaximumResolvedUtf8Bytes = HarnessArtifactTestFixture.DefaultMaximumResolvedUtf8Bytes;

    private static readonly DateTimeOffset CreatedAtUtc = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // --- Deterministic digest -------------------------------------------------------------

    [Fact]
    public void ComputeDigest_SameContent_ProducesIdenticalDigest()
    {
        var first = HarnessArtifactIdentity.ComputeDigest("identical payload content");
        var second = HarnessArtifactIdentity.ComputeDigest("identical payload content");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeDigest_ProducesLowercaseHexOfExpectedLength()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("payload");

        Assert.Equal(HarnessArtifactIdentity.DigestHexLength, digest.Length);
        Assert.Equal(digest, digest.ToLowerInvariant());
        Assert.True(HarnessArtifactIdentity.IsWellFormedDigest(digest));
    }

    [Fact]
    public void ComputeDigest_DifferentContent_ProducesDifferentDigest()
    {
        var first = HarnessArtifactIdentity.ComputeDigest("payload one");
        var second = HarnessArtifactIdentity.ComputeDigest("payload two");

        Assert.NotEqual(first, second);
    }

    // --- Deterministic content-addressed path ---------------------------------------------

    [Fact]
    public void BuildPath_IsShardedContentAddressedPathDerivedFromDigest()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("payload for path shape");

        var path = HarnessArtifactIdentity.BuildPath(digest);

        Assert.Equal($"{ArtifactRoot}/{digest[..2]}/{digest[2..4]}/{digest}", path);
    }

    [Fact]
    public void BuildPath_SameDigest_ProducesIdenticalPath()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("payload for path determinism");

        var first = HarnessArtifactIdentity.BuildPath(digest);
        var second = HarnessArtifactIdentity.BuildPath(digest);

        Assert.Equal(first, second);
    }

    // --- Same content -> same reference/path ----------------------------------------------

    [Fact]
    public void Create_SameContent_ProducesEqualReferencesWithSameIdAndPath()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var first = HarnessArtifactReference.Create(
            fixture.Binding,
            "identical tool result content",
            "description",
            "tool-name",
            "call-1",
            CreatedAtUtc);
        var second = HarnessArtifactReference.Create(
            fixture.Binding,
            "identical tool result content",
            "description",
            "tool-name",
            "call-1",
            CreatedAtUtc);

        Assert.Equal(first.ReferenceId, second.ReferenceId);
        Assert.Equal(first.WorkspacePath, second.WorkspacePath);
        Assert.Equal(first.ContentDigest, second.ContentDigest);
        Assert.Equal(HarnessArtifactIdentity.DefaultArtifactRoot, first.ArtifactRoot);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_DifferentContent_ProducesDifferentReferenceIdAndPath()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var first = HarnessArtifactReference.Create(
            fixture.Binding, "content A", "description", "tool-name", "call-1", CreatedAtUtc);
        var second = HarnessArtifactReference.Create(
            fixture.Binding, "content B", "description", "tool-name", "call-1", CreatedAtUtc);

        Assert.NotEqual(first.ReferenceId, second.ReferenceId);
        Assert.NotEqual(first.WorkspacePath, second.WorkspacePath);
        Assert.NotEqual(first.ContentDigest, second.ContentDigest);
    }

    [Fact]
    public void Create_ReferenceId_MatchesArtifactUriSchemeForDigest()
    {
        const string content = "content used to check reference id shape";
        using var fixture = HarnessArtifactTestFixture.Create();
        var reference = HarnessArtifactReference.Create(
            fixture.Binding,
            content,
            "description",
            "tool-name",
            "call-1",
            CreatedAtUtc);

        Assert.Equal($"artifact://sha256/{reference.ContentDigest}", reference.ReferenceId);
    }

    // --- Metadata validation ----------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstruct_EmptyOrWhiteSpaceOwnerUserId_ThrowsArgumentException(string ownerUserId)
    {
        Assert.Throws<ArgumentException>(() =>
            ReconstructReference(content: "content", ownerUserId: ownerUserId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstruct_EmptyOrWhiteSpaceOwnerOrchestrationId_ThrowsArgumentException(string ownerOrchestrationId)
    {
        Assert.Throws<ArgumentException>(() =>
            ReconstructReference(content: "content", ownerOrchestrationId: ownerOrchestrationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstruct_EmptyOrWhiteSpaceOwnerSessionId_ThrowsArgumentException(string ownerSessionId)
    {
        Assert.Throws<ArgumentException>(() =>
            ReconstructReference(content: "content", ownerSessionId: ownerSessionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhiteSpaceCreatingToolName_ThrowsArgumentException(string creatingToolName)
    {
        Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Create(
                CreateBinding(),
                "content",
                "description",
                creatingToolName,
                "call-1",
                CreatedAtUtc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhiteSpaceCreatingCallId_ThrowsArgumentException(string creatingCallId)
    {
        Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Create(
                CreateBinding(),
                "content",
                "description",
                "tool-name",
                creatingCallId,
                CreatedAtUtc));
    }

    [Fact]
    public void Create_DescriptionExceedsMaximumLength_ThrowsArgumentException()
    {
        var oversizedDescription = new string('d', HarnessArtifactReference.MaximumDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Create(
                CreateBinding(),
                "content",
                oversizedDescription,
                "tool-name",
                "call-1",
                CreatedAtUtc));
    }

    [Fact]
    public void Create_DescriptionAtMaximumLength_Succeeds()
    {
        var boundaryDescription = new string('d', HarnessArtifactReference.MaximumDescriptionLength);

        var reference = HarnessArtifactReference.Create(
            CreateBinding(),
            "content",
            boundaryDescription,
            "tool-name",
            "call-1",
            CreatedAtUtc);

        Assert.Equal(boundaryDescription, reference.Description);
    }

    [Fact]
    public void Create_BindingWithoutWorkspace_ThrowsArgumentException()
    {
        var accessor = new AgentExecutionContextAccessor();
        using var scope = accessor.BeginScope(new AgentExecutionContext("user-1", "orchestration-1"));
        var capture = HarnessExecutionBinding.Capture(accessor, "session-1", requireWorkspace: false);

        Assert.Equal(HarnessExecutionBindingStatus.Valid, capture.Status);
        var binding = Assert.IsType<HarnessExecutionBinding>(capture.Binding);

        var exception = Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Create(
                binding,
                "content",
                "description",
                "tool-name",
                "call-1",
                CreatedAtUtc));

        Assert.Equal("executionBinding", exception.ParamName);
    }

    // --- Reconstruct: malformed/path/digest inputs fail closed ------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("tooshort")]
    [InlineData("ZZ00000000000000000000000000000000000000000000000000000000000")] // uppercase, wrong length
    [InlineData("gg00000000000000000000000000000000000000000000000000000000000")] // non-hex character, wrong length
    public void Reconstruct_MalformedDigest_ThrowsArgumentException(string malformedDigest)
    {
        Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Reconstruct(
                "irrelevant/placeholder/path",
                malformedDigest,
                10,
                "description",
                "user-1",
                "orchestration-1",
                "session-1",
                "tool-name",
                "call-1",
                CreatedAtUtc));
    }

    [Fact]
    public void Reconstruct_UppercaseWellFormedLengthDigest_ThrowsArgumentException()
    {
        var lowercaseDigest = HarnessArtifactIdentity.ComputeDigest("content for uppercase-rejection test");
        var uppercaseDigest = lowercaseDigest.ToUpperInvariant();

        Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Reconstruct(
                "irrelevant/placeholder/path",
                uppercaseDigest,
                10,
                "description",
                "user-1",
                "orchestration-1",
                "session-1",
                "tool-name",
                "call-1",
                CreatedAtUtc));
    }

    [Fact]
    public void Reconstruct_WorkspacePathDoesNotMatchDigest_ThrowsArgumentException()
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("content whose digest will not match the supplied path");
        var pathForDifferentDigest = HarnessArtifactIdentity.BuildPath(
            HarnessArtifactIdentity.ComputeDigest("entirely different content"));

        var exception = Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Reconstruct(
                pathForDifferentDigest,
                digest,
                10,
                "description",
                "user-1",
                "orchestration-1",
                "session-1",
                "tool-name",
                "call-1",
                CreatedAtUtc));
        Assert.Equal("workspacePath", exception.ParamName);
    }

    [Fact]
    public void Reconstruct_MatchingPathAndDigest_Succeeds()
    {
        const string content = "content used to prove a correctly matched path/digest pair reconstructs";
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var path = HarnessArtifactIdentity.BuildPath(digest);
        var byteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);

        var reference = HarnessArtifactReference.Reconstruct(
            path, digest, byteSize, "description", "user-1", "orchestration-1", "session-1", "tool-name", "call-1", CreatedAtUtc);

        Assert.Equal(path, reference.WorkspacePath);
        Assert.Equal(digest, reference.ContentDigest);
    }

    [Fact]
    public void Reconstruct_PathUnderDifferentRoot_ThrowsArgumentException()
    {
        const string content = "content whose path must remain pinned to the default artifact root";
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var foreignRootPath =
            $".foundry/other-artifacts/{digest[..2]}/{digest[2..4]}/{digest}";

        var exception = Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Reconstruct(
                foreignRootPath,
                digest,
                HarnessArtifactIdentity.ComputeUtf8ByteLength(content),
                "description",
                "user-1",
                "orchestration-1",
                "session-1",
                "tool-name",
                "call-1",
                CreatedAtUtc));

        Assert.Equal("workspacePath", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reconstruct_NonPositiveContentByteSize_ThrowsArgumentException(int contentByteSize)
    {
        var digest = HarnessArtifactIdentity.ComputeDigest("content for byte-size validation");
        var path = HarnessArtifactIdentity.BuildPath(digest);

        Assert.Throws<ArgumentException>(() =>
            HarnessArtifactReference.Reconstruct(
                path, digest, contentByteSize, "description", "user-1", "orchestration-1", "session-1", "tool-name", "call-1", CreatedAtUtc));
    }

    // --- Resolver outcomes reachable via reference/content identity alone ------------------

    [Fact]
    public void Resolve_ContentPresentAndUnmodified_ReturnsResolvedWithExactContent()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "the exact original tool result content, byte for byte";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);

        var resolution = fixture.Resolver.Resolve(reference, DefaultMaximumResolvedUtf8Bytes, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, resolution.Status);
        Assert.Equal(content, resolution.Content);
        Assert.Equal(reference.ContentDigest, resolution.ObservedContentDigest);
        Assert.Equal(reference.ContentByteSize, resolution.ObservedContentByteSize);
    }

    [Fact]
    public void Resolve_ContentMutatedOutOfBandAfterCreation_ReturnsStaleWithNoContent()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string originalContent = "original content recorded by the reference";
        var reference = fixture.CreateReference(originalContent, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, originalContent);

        // Out-of-band mutation: something overwrote the content-addressed path directly, without
        // minting a new reference — the recorded digest is now stale.
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, "a completely different mutated body");

        var resolution = fixture.Resolver.Resolve(reference, DefaultMaximumResolvedUtf8Bytes, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Stale, resolution.Status);
        Assert.Null(resolution.Content);
        Assert.NotEqual(reference.ContentDigest, resolution.ObservedContentDigest);
    }

    [Fact]
    public void Resolve_ReferenceNeverWrittenToWorkspace_ReturnsMissingWithNoContent()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        var reference = fixture.CreateReference("content that is never actually persisted", CreatedAtUtc);
        // Deliberately never write anything to reference.WorkspacePath.

        var resolution = fixture.Resolver.Resolve(reference, DefaultMaximumResolvedUtf8Bytes, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Missing, resolution.Status);
        Assert.Null(resolution.Content);
        Assert.Null(resolution.ObservedContentByteSize);
    }

    [Fact]
    public void Resolve_MultibyteUtf8Content_ExactBudgetResolvesButOneByteLowerIsOverBudget()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        const string content = "🙂é";
        var reference = fixture.CreateReference(content, CreatedAtUtc);
        fixture.Workspace.TryWriteFile(reference.WorkspacePath, content);

        var exactUtf8ByteBudget = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);
        Assert.True(exactUtf8ByteBudget > content.Length);

        var resolved = fixture.Resolver.Resolve(reference, exactUtf8ByteBudget, CancellationToken.None);
        var overBudget = fixture.Resolver.Resolve(reference, exactUtf8ByteBudget - 1, CancellationToken.None);

        Assert.Equal(HarnessArtifactResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(content, resolved.Content);
        Assert.Equal(exactUtf8ByteBudget, resolved.ObservedContentByteSize);
        Assert.Equal(HarnessArtifactResolutionStatus.OverBudget, overBudget.Status);
        Assert.Null(overBudget.Content);
        Assert.Equal(exactUtf8ByteBudget, overBudget.ObservedContentByteSize);
    }

    private static HarnessExecutionBinding CreateBinding()
    {
        using var fixture = HarnessArtifactTestFixture.Create();
        return fixture.Binding;
    }

    private static HarnessArtifactReference ReconstructReference(
        string content,
        string description = "description",
        string ownerUserId = "user-1",
        string ownerOrchestrationId = "orchestration-1",
        string ownerSessionId = "session-1",
        string creatingToolName = "tool-name",
        string creatingCallId = "call-1")
    {
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var path = HarnessArtifactIdentity.BuildPath(digest);
        var byteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);

        return HarnessArtifactReference.Reconstruct(
            path,
            digest,
            byteSize,
            description,
            ownerUserId,
            ownerOrchestrationId,
            ownerSessionId,
            creatingToolName,
            creatingCallId,
            CreatedAtUtc);
    }
}
