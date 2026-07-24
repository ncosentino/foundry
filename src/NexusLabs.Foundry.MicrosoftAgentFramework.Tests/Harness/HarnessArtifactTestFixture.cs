using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test fixture that wires up an <see cref="IAgentExecutionContextAccessor"/> scope, a captured
/// <see cref="HarnessExecutionBinding"/>, a <see cref="FakeWorkspace"/>, and a bound
/// <see cref="HarnessArtifactResolver"/>/<see cref="HarnessArtifactRehydration"/> pair using the
/// same explicit construction sequence trusted production callers must follow. Mirrors the style
/// used by <see cref="WorkspaceAgentFileStoreHarness"/> and <see cref="HarnessCompositionTestFixture"/>
/// — no optional parameters.
/// </summary>
internal sealed class HarnessArtifactTestFixture : IDisposable
{
    internal const string DefaultUserId = "artifact-user-1";
    internal const string DefaultOrchestrationId = "artifact-orchestration-1";
    internal const string DefaultSessionId = "artifact-session-1";
    internal const string DefaultToolName = "search-results";
    internal const string DefaultCallId = "call-1";
    internal const string DefaultDescription = "test artifact reference";
    internal const int DefaultMaximumResolvedUtf8Bytes = 1_000_000;

    private readonly IDisposable _scope;
    private bool _disposed;

    private HarnessArtifactTestFixture(
        IAgentExecutionContextAccessor accessor,
        FakeWorkspace workspace,
        HarnessExecutionBinding binding,
        string sessionId,
        HarnessArtifactResolver resolver,
        HarnessArtifactRehydration rehydration,
        IDisposable scope)
    {
        Accessor = accessor;
        Workspace = workspace;
        Binding = binding;
        SessionId = sessionId;
        Resolver = resolver;
        Rehydration = rehydration;
        _scope = scope;
    }

    internal IAgentExecutionContextAccessor Accessor { get; }

    internal FakeWorkspace Workspace { get; }

    internal HarnessExecutionBinding Binding { get; }

    internal string SessionId { get; }

    internal HarnessArtifactResolver Resolver { get; }

    internal HarnessArtifactRehydration Rehydration { get; }

    internal static HarnessArtifactTestFixture Create() =>
        Create(new FakeWorkspace());

    internal static HarnessArtifactTestFixture Create(FakeWorkspace workspace) =>
        Create(
            new AgentExecutionContextAccessor(),
            DefaultUserId,
            DefaultOrchestrationId,
            DefaultSessionId,
            workspace);

    internal static HarnessArtifactTestFixture Create(
        IAgentExecutionContextAccessor accessor,
        string userId,
        string orchestrationId,
        string sessionId,
        FakeWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(workspace);

        var scope = accessor.BeginScope(
            new AgentExecutionContext(userId, orchestrationId, Workspace: workspace));

        var capture = HarnessExecutionBinding.Capture(
            accessor,
            sessionId,
            requireWorkspace: true);
        if (capture.Status != HarnessExecutionBindingStatus.Valid || capture.Binding is null)
        {
            scope.Dispose();
            throw new InvalidOperationException(
                $"Test fixture failed to capture a valid execution binding (status: '{capture.Status}').");
        }

        var resolver = new HarnessArtifactResolver(capture.Binding, accessor, sessionId);
        var rehydration = new HarnessArtifactRehydration(resolver);

        return new HarnessArtifactTestFixture(
            accessor,
            workspace,
            capture.Binding,
            sessionId,
            resolver,
            rehydration,
            scope);
    }

    /// <summary>
    /// Builds a <see cref="HarnessArtifactReference"/> for <paramref name="content"/>, sourcing
    /// ownership from this fixture's captured trusted binding (never from arbitrary caller input),
    /// matching how a real offload transform would mint a reference.
    /// </summary>
    internal HarnessArtifactReference CreateReference(string content, DateTimeOffset createdAtUtc) =>
        HarnessArtifactReference.Create(
            Binding,
            content,
            DefaultDescription,
            DefaultToolName,
            DefaultCallId,
            createdAtUtc);

    /// <summary>
    /// Builds a <see cref="HarnessArtifactReference"/> for <paramref name="content"/> whose owner
    /// identity is deliberately foreign to this fixture's trusted binding by going through the
    /// untrusted reconstruction path — simulating a forged or cross-tenant reference that must never
    /// resolve successfully.
    /// </summary>
    internal HarnessArtifactReference CreateForeignOwnedReference(string content, DateTimeOffset createdAtUtc)
    {
        var digest = HarnessArtifactIdentity.ComputeDigest(content);
        var byteSize = HarnessArtifactIdentity.ComputeUtf8ByteLength(content);
        var workspacePath = HarnessArtifactIdentity.BuildPath(digest);

        return HarnessArtifactReference.Reconstruct(
            workspacePath,
            digest,
            byteSize,
            DefaultDescription,
            "someone-elses-user-id",
            "someone-elses-orchestration-id",
            SessionId,
            DefaultToolName,
            DefaultCallId,
            createdAtUtc);
    }

    /// <summary>Ends the execution-context scope, reverting the ambient context.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scope.Dispose();
    }
}
