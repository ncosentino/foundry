using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Workspace;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test fixture that wires up an <see cref="IAgentExecutionContextAccessor"/> scope, a captured
/// <see cref="HarnessExecutionBinding"/>, and a bound <c>WorkspaceAgentFileStore</c> using the
/// exact same construction sequence trusted production callers must follow. Mirrors the explicit
/// overload-chain style used by <see cref="HarnessCompositionTestFixture"/> — no optional
/// parameters.
/// </summary>
internal sealed class WorkspaceAgentFileStoreHarness : IDisposable
{
    internal const string DefaultUserId = "workspace-store-user-1";
    internal const string DefaultOrchestrationId = "workspace-store-orchestration-1";
    internal const string DefaultSessionId = "workspace-store-session-1";
    internal const int DefaultMaximumListEntries = 100;

    private readonly IDisposable _scope;
    private bool _disposed;

    private WorkspaceAgentFileStoreHarness(
        IAgentExecutionContextAccessor accessor,
        IWorkspace workspace,
        HarnessExecutionBinding binding,
        string sessionId,
        IDisposable scope,
        WorkspaceAgentFileStore store)
    {
        Accessor = accessor;
        Workspace = workspace;
        Binding = binding;
        SessionId = sessionId;
        _scope = scope;
        Store = store;
    }

    internal IAgentExecutionContextAccessor Accessor { get; }

    internal IWorkspace Workspace { get; }

    internal HarnessExecutionBinding Binding { get; }

    internal string SessionId { get; }

    internal WorkspaceAgentFileStore Store { get; }

    /// <summary>Missing-file classifier matching <see cref="InMemoryWorkspace"/>'s failure shape.</summary>
    internal static bool DefaultMissingFileClassifier(Exception failure) =>
        failure is FileNotFoundException;

    internal static WorkspaceAgentFileStoreHarness Create() =>
        Create(new InMemoryWorkspace());

    internal static WorkspaceAgentFileStoreHarness Create(IWorkspace workspace) =>
        Create(workspace, DefaultMissingFileClassifier);

    internal static WorkspaceAgentFileStoreHarness Create(
        IWorkspace workspace,
        WorkspaceMissingFileClassifier missingFileClassifier) =>
        Create(workspace, missingFileClassifier, DefaultMaximumListEntries);

    internal static WorkspaceAgentFileStoreHarness Create(
        IWorkspace workspace,
        WorkspaceMissingFileClassifier missingFileClassifier,
        int maximumListEntries) =>
        Create(
            new AgentExecutionContextAccessor(),
            DefaultUserId,
            DefaultOrchestrationId,
            DefaultSessionId,
            workspace,
            missingFileClassifier,
            maximumListEntries);

    internal static WorkspaceAgentFileStoreHarness Create(
        IAgentExecutionContextAccessor accessor,
        string userId,
        string orchestrationId,
        string sessionId,
        IWorkspace workspace,
        WorkspaceMissingFileClassifier missingFileClassifier,
        int maximumListEntries)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(missingFileClassifier);

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
                $"Test harness failed to capture a valid execution binding (status: '{capture.Status}').");
        }

        var store = new WorkspaceAgentFileStore(
            workspace,
            capture.Binding,
            accessor,
            sessionId,
            maximumListEntries,
            missingFileClassifier);

        return new WorkspaceAgentFileStoreHarness(
            accessor,
            workspace,
            capture.Binding,
            sessionId,
            scope,
            store);
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
