using Microsoft.Agents.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Workspace;

/// <summary>
/// Internal bridge that adapts one authorized <see cref="IWorkspace"/> to MAF 1.15
/// <see cref="AgentFileStore"/> for the subset of semantics proven feasible by
/// <c>specs/001-maf-harness-first-class/evidence/workspace-identity-feasibility.md</c> (T020) and
/// restated/extended by <c>specs/001-maf-harness-first-class/evidence/harness-lifecycle-feasibility.md</c>
/// (T041).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Per-execution binding, not ambient.</strong> One instance is constructed for one
/// authorized <see cref="IWorkspace"/> and one immutable trusted execution identity (captured in
/// the constructor's <see cref="HarnessExecutionBinding"/> parameter). Every override calls
/// <see cref="HarnessExecutionBinding.EnsureCurrent"/> before touching the workspace, and again
/// afterward when the call is returning data or state to the caller. The workspace is never
/// selected from a path, model input, or restored session state — see T020's "Trusted identity
/// binding" and "Session and isolation constraints" sections.
/// </para>
/// <para>
/// <strong>Supported subset.</strong>
/// </para>
/// <list type="bullet">
///   <item><description><see cref="WriteAsync"/> — ordinary write via <c>TryWriteFile</c> only.
///   Never claims compare-exchange/CAS semantics; upstream <see cref="AgentFileStore.WriteAsync"/>
///   supplies no expected version/content, so mapping it to
///   <c>IWorkspace.TryCompareExchange</c> would fabricate a guarantee that does not exist.</description></item>
///   <item><description><see cref="ReadAsync"/> — conditional. Returns content on success; maps a
///   workspace failure to <see langword="null"/> only when the constructor-supplied
///   <see cref="WorkspaceMissingFileClassifier"/> recognizes it as a missing file; every other
///   failure propagates unchanged.</description></item>
///   <item><description><see cref="DeleteAsync"/> — permanently unsupported.
///   <see cref="IWorkspace"/> has no delete operation.</description></item>
///   <item><description><see cref="ListChildrenAsync"/> — supported with limits. Derived from a
///   full scan of <c>IWorkspace.GetFilePaths()</c> filtered in memory; the required
///   <c>maximumListEntries</c> cap bounds the returned result, not the O(total workspace files)
///   scan cost of the underlying enumeration (T020 evidence, lines 79-82; T041 evidence, Decision
///   5). Directory entries are returned before file entries, and directory names are
///   deduplicated case-insensitively using <see cref="WorkspacePath.PathComparer"/>.</description></item>
///   <item><description><see cref="FileExistsAsync"/> — supported; canonicalizes and fails closed
///   on an invalid path.</description></item>
///   <item><description><see cref="SearchAsync"/> — permanently unsupported. <see cref="IWorkspace"/>
///   cannot inspect size or bound a read before allocating full content; a bounded-search adapter
///   would require separate feasibility evidence before any profile may enable it.</description></item>
///   <item><description><see cref="CreateDirectoryAsync"/> — a validated no-op. Empty directories
///   created this way are not observable through <see cref="ListChildrenAsync"/> or
///   <see cref="SearchAsync"/>, because <see cref="IWorkspace"/> has no directory-as-object
///   concept.</description></item>
/// </list>
/// <para>
/// <strong>Cancellation.</strong> <see cref="IWorkspace"/> is synchronous, so the bridge cannot
/// interrupt an already-running workspace call, cannot claim atomic cancellation after a write may
/// already have completed, and never uses <see cref="Task.Run(Action)"/> to fabricate
/// interruption. Every override checks its cancellation token before
/// canonicalization, before invoking the underlying synchronous workspace call, and again
/// immediately afterward (and between listing items for <see cref="ListChildrenAsync"/>).
/// </para>
/// <para>
/// This type remains internal. <see cref="AgentFileStore"/>, <see cref="FileStoreEntry"/>, and
/// <see cref="FileSearchResult"/> are experimental (<c>MAAI001</c>); the suppression below is
/// scoped to this file because nearly every member of this bridge necessarily references that
/// experimental surface.
/// </para>
/// </remarks>
#pragma warning disable MAAI001 // AgentFileStore/FileStoreEntry/FileSearchResult are experimental; this bridge is the internal, evidence-gated adapter proven by T020/T041.
internal sealed class WorkspaceAgentFileStore : AgentFileStore
{
    private readonly IWorkspace _workspace;
    private readonly HarnessExecutionBinding _executionBinding;
    private readonly IAgentExecutionContextAccessor _executionContextAccessor;
    private readonly string _sessionId;
    private readonly int _maximumListEntries;
    private readonly WorkspaceMissingFileClassifier _missingFileClassifier;

    /// <summary>
    /// Constructs a bridge bound to exactly one authorized workspace and one trusted execution
    /// identity.
    /// </summary>
    /// <param name="workspace">
    /// The authorized workspace this store may operate on. Must be reference-equal to
    /// <paramref name="executionBinding"/>'s captured workspace — the bridge never accepts a
    /// workspace the trusted binding did not itself authorize.
    /// </param>
    /// <param name="executionBinding">
    /// The trusted execution binding captured by <see cref="HarnessExecutionBinding.Capture"/>.
    /// Every operation revalidates against this binding before (and, where meaningful, after)
    /// touching <paramref name="workspace"/>.
    /// </param>
    /// <param name="executionContextAccessor">
    /// Accessor used to read the ambient execution context at validation time.
    /// </param>
    /// <param name="sessionId">
    /// The trusted session identity this store is bound to. Must match
    /// <paramref name="executionBinding"/>'s own captured session identity.
    /// </param>
    /// <param name="maximumListEntries">
    /// The maximum number of entries <see cref="ListChildrenAsync"/> may return. Must be greater
    /// than zero. Exceeding this cap is an explicit failure, never silent truncation.
    /// </param>
    /// <param name="missingFileClassifier">
    /// Explicit policy distinguishing a missing-file read failure from every other failure for the
    /// bound workspace implementation. Required because <see cref="IWorkspace"/> has no typed
    /// missing-file outcome.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="workspace"/>, <paramref name="executionBinding"/>,
    /// <paramref name="executionContextAccessor"/>, or <paramref name="missingFileClassifier"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sessionId"/> is null/empty/whitespace, does not match
    /// <paramref name="executionBinding"/>'s captured session identity, or
    /// <paramref name="workspace"/> is not the same instance <paramref name="executionBinding"/>
    /// authorized.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumListEntries"/> is not greater than zero.
    /// </exception>
    internal WorkspaceAgentFileStore(
        IWorkspace workspace,
        HarnessExecutionBinding executionBinding,
        IAgentExecutionContextAccessor executionContextAccessor,
        string sessionId,
        int maximumListEntries,
        WorkspaceMissingFileClassifier missingFileClassifier)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(executionBinding);
        ArgumentNullException.ThrowIfNull(executionContextAccessor);
        ArgumentNullException.ThrowIfNull(missingFileClassifier);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "A trusted non-empty session identity is required.",
                nameof(sessionId));
        }

        if (maximumListEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumListEntries),
                maximumListEntries,
                "The maximum list-entry cap must be greater than zero.");
        }

        if (!string.Equals(sessionId, executionBinding.SessionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supplied session identity must match the trusted execution binding's captured session identity.",
                nameof(sessionId));
        }

        if (!ReferenceEquals(workspace, executionBinding.Workspace))
        {
            throw new ArgumentException(
                "The supplied workspace must be the same instance authorized by the trusted execution binding.",
                nameof(workspace));
        }

        _workspace = workspace;
        _executionBinding = executionBinding;
        _executionContextAccessor = executionContextAccessor;
        _sessionId = sessionId;
        _maximumListEntries = maximumListEntries;
        _missingFileClassifier = missingFileClassifier;
    }

    /// <inheritdoc />
    public override Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();

        var canonicalPath = CanonicalizeFilePath(path);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var result = _workspace.TryWriteFile(canonicalPath, content);
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.Success)
        {
            throw result.Exception!;
        }

        EnsureBinding();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task<string?> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();

        var canonicalPath = CanonicalizeFilePath(path);
        cancellationToken.ThrowIfCancellationRequested();

        var result = _workspace.TryReadFile(canonicalPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (result.Success)
        {
            EnsureBinding();
            return Task.FromResult<string?>(result.Value.Content);
        }

        var failure = result.Exception!;
        if (_missingFileClassifier(failure))
        {
            EnsureBinding();
            return Task.FromResult<string?>(null);
        }

        throw failure;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Permanently unsupported. <see cref="IWorkspace"/> has no delete operation; profiles must
    /// disable or omit tools/paths that could invoke this method rather than relying on this
    /// runtime failure as the primary capability-selection mechanism.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();
        _ = CanonicalizeFilePath(path);
        cancellationToken.ThrowIfCancellationRequested();

        throw new NotSupportedException(
            "WorkspaceAgentFileStore does not support delete. IWorkspace exposes no delete " +
            "operation. Profiles must disable or omit tools/paths that could invoke " +
            "AgentFileStore.DeleteAsync rather than relying on this runtime failure as the " +
            "primary capability-selection mechanism.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Full-scan of <see cref="IWorkspace.GetFilePaths"/>, filtered in memory to direct children of
    /// <paramref name="directory"/>. The required <c>maximumListEntries</c> cap (supplied at
    /// construction) bounds the <em>returned</em> list only — the underlying scan remains
    /// O(total workspace files) and is an accepted, documented limitation (T020 evidence, lines
    /// 79-82), not a bound this method can enforce. Directory entries are listed before file
    /// entries; names are deduplicated case-insensitively via
    /// <see cref="WorkspacePath.PathComparer"/>. Directories that
    /// exist only via a prior <see cref="CreateDirectoryAsync"/> no-op (i.e., contain no files) are
    /// not observable here.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The number of direct-child entries discovered exceeds the configured
    /// <c>maximumListEntries</c> cap. The cap is enforced as an explicit failure, never silent
    /// truncation.
    /// </exception>
    public override Task<IReadOnlyList<FileStoreEntry>> ListChildrenAsync(string directory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();

        var canonicalDirectory = CanonicalizeDirectoryPath(directory);
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = canonicalDirectory.Length > 0 ? canonicalDirectory + "/" : string.Empty;
        var directoryNames = new SortedSet<string>(WorkspacePath.PathComparer);
        var fileNames = new SortedSet<string>(WorkspacePath.PathComparer);

        foreach (var filePath in _workspace.GetFilePaths())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string remainder;
            if (prefix.Length > 0)
            {
                if (filePath.Length <= prefix.Length ||
                    !filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                remainder = filePath[prefix.Length..];
            }
            else
            {
                remainder = filePath;
            }

            if (remainder.Length == 0)
            {
                continue;
            }

            var slashIndex = remainder.IndexOf('/');
            if (slashIndex < 0)
            {
                fileNames.Add(remainder);
            }
            else
            {
                directoryNames.Add(remainder[..slashIndex]);
            }

            if (directoryNames.Count + fileNames.Count > _maximumListEntries)
            {
                throw new InvalidOperationException(
                    $"Workspace directory listing for '{canonicalDirectory}' exceeded the configured " +
                    $"cap of {_maximumListEntries} entries. The underlying scan is not bounded by this " +
                    "cap; narrow the listed directory or reconstruct the bridge with a higher cap.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var entries = new List<FileStoreEntry>(directoryNames.Count + fileNames.Count);
        foreach (var name in directoryNames)
        {
            entries.Add(new FileStoreEntry(name, FileStoreEntry.Directory));
        }
        foreach (var name in fileNames)
        {
            entries.Add(new FileStoreEntry(name, FileStoreEntry.File));
        }

        EnsureBinding();
        return Task.FromResult<IReadOnlyList<FileStoreEntry>>(entries);
    }

    /// <inheritdoc />
    public override Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();

        var canonicalPath = CanonicalizeFilePath(path);
        cancellationToken.ThrowIfCancellationRequested();

        var exists = _workspace.FileExists(canonicalPath);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureBinding();
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Permanently unsupported. <see cref="IWorkspace"/> cannot inspect file size or bound a read
    /// before allocating full content, so no generic, safe implementation of this method exists.
    /// Enabling search would require a separately proven bounded-search adapter (see
    /// <c>specs/001-maf-harness-first-class/evidence/workspace-identity-feasibility.md</c>).
    /// </remarks>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Task<IReadOnlyList<FileSearchResult>> SearchAsync(
        string directory,
        string regexPattern,
        string? globPattern = null,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();
        _ = CanonicalizeDirectoryPath(directory);
        cancellationToken.ThrowIfCancellationRequested();

        throw new NotSupportedException(
            "WorkspaceAgentFileStore does not support generic content search. IWorkspace cannot " +
            "inspect file size or bound a read before allocating full content. Enabling search " +
            "requires a separately proven bounded-search adapter (see " +
            "specs/001-maf-harness-first-class/evidence/workspace-identity-feasibility.md).");
    }

    /// <inheritdoc />
    /// <remarks>
    /// A validated no-op. <see cref="IWorkspace"/> has no directory-as-object concept, so an
    /// "empty" directory created this way is not observable through <see cref="ListChildrenAsync"/>
    /// or <see cref="SearchAsync"/>. Valid only for profiles that accept a file-materialized
    /// directory model.
    /// </remarks>
    public override Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureBinding();

        _ = CanonicalizeDirectoryPath(path);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the trusted execution binding against the current ambient execution context,
    /// throwing <see cref="InvalidOperationException"/> if the binding is no longer valid (missing
    /// context, changed identity, changed session, or changed/missing workspace).
    /// </summary>
    private void EnsureBinding() =>
        _executionBinding.EnsureCurrent(_executionContextAccessor, _sessionId);

    private static string CanonicalizeFilePath(string path)
    {
        RejectRootedPath(path);
        return WorkspacePath.Canonicalize(path);
    }

    private static string CanonicalizeDirectoryPath(string path)
    {
        RejectRootedPath(path);
        return WorkspacePath.CanonicalizeDirectory(path);
    }

    private static void RejectRootedPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Path.IsPathRooted(path) ||
            path.StartsWith('\\') ||
            path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            throw new ArgumentException(
                "Workspace file-store paths must be relative logical paths.",
                nameof(path));
        }
    }
}
#pragma warning restore MAAI001
