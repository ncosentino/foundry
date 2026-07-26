using Microsoft.Agents.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Tests;

/// <summary>
/// A minimal in-memory <see cref="AgentFileStore"/> fake used only to prove that supplying a
/// non-<see langword="null"/> <see cref="Bundle.FoundryHarnessAgentConfiguration.FileAccessStore"/>
/// flips the effective-defaults disposition for <see cref="Bundle.FoundryHarnessFeature.FileAccess"/>.
/// It performs no real file I/O.
/// </summary>
internal sealed class InMemoryAgentFileStoreFake : AgentFileStore
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public override Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        _files[path] = content;
        return Task.CompletedTask;
    }

    public override Task<string?> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(_files.TryGetValue(path, out var content) ? content : null);

    public override Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.Remove(path));

    public override Task<IReadOnlyList<FileStoreEntry>> ListChildrenAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileStoreEntry>>([]);

    public override Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(_files.ContainsKey(path));

    public override Task<IReadOnlyList<FileSearchResult>> SearchAsync(
        string directory,
        string regexPattern,
        string? globPattern = null,
        bool recursive = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FileSearchResult>>([]);

    public override Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
