namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Workspace;

/// <summary>
/// Classifies whether an <see cref="MicrosoftAgentFramework.Workspace.IWorkspace"/> read failure
/// represents a missing file. <see cref="Harness.Workspace.WorkspaceAgentFileStore"/> maps a
/// classified-missing failure to <see langword="null"/> and propagates every other failure
/// unchanged, matching the T020 <c>workspace-identity-feasibility.md</c> failure-mapping
/// contract: <c>IWorkspace</c> has no typed missing-file outcome, so a generic bridge cannot
/// distinguish "missing" from any other failure without an explicit, caller-supplied policy.
/// </summary>
/// <param name="failure">
/// The exception carried by a failed <see cref="MicrosoftAgentFramework.Workspace.WorkspaceResult{T}"/>
/// returned from <see cref="MicrosoftAgentFramework.Workspace.IWorkspace.TryReadFile"/>.
/// </param>
/// <returns>
/// <see langword="true"/> when <paramref name="failure"/> represents a missing file for the bound
/// workspace implementation; <see langword="false"/> when it represents any other failure that
/// must be surfaced to the caller.
/// </returns>
internal delegate bool WorkspaceMissingFileClassifier(Exception failure);
