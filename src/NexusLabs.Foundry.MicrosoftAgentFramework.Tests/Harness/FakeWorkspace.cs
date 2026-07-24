using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Configurable <see cref="IWorkspace"/> test double that wraps an inner workspace (an
/// <see cref="InMemoryWorkspace"/> by default), counts every call it receives, and optionally
/// overrides the outcome of <see cref="TryReadFile"/>. Used to prove
/// <c>WorkspaceAgentFileStore</c>'s exact call surface — e.g. that ordinary writes only ever
/// call <see cref="TryWriteFile"/> and never <see cref="TryCompareExchange"/>, and that a
/// non-missing read failure propagates unchanged rather than being swallowed.
/// </summary>
internal sealed class FakeWorkspace : IWorkspace
{
    private readonly IWorkspace _inner;
    private int _writeFileCallCount;
    private int _readFileCallCount;
    private int _compareExchangeCallCount;
    private int _fileExistsCallCount;
    private int _getFilePathsCallCount;

    internal FakeWorkspace()
        : this(new InMemoryWorkspace())
    {
    }

    internal FakeWorkspace(IWorkspace inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Number of times <see cref="TryWriteFile"/> was called.</summary>
    internal int WriteFileCallCount => _writeFileCallCount;

    /// <summary>Number of times <see cref="TryReadFile"/> was called.</summary>
    internal int ReadFileCallCount => _readFileCallCount;

    /// <summary>Number of times <see cref="TryCompareExchange"/> was called.</summary>
    internal int CompareExchangeCallCount => _compareExchangeCallCount;

    /// <summary>Number of times <see cref="FileExists"/> was called.</summary>
    internal int FileExistsCallCount => _fileExistsCallCount;

    /// <summary>Number of times <see cref="GetFilePaths"/> was called.</summary>
    internal int GetFilePathsCallCount => _getFilePathsCallCount;

    /// <summary>
    /// When set, replaces the result of <see cref="TryReadFile"/> entirely (bypassing the inner
    /// workspace). Used to force a non-missing failure so the caller's missing-file classifier
    /// negative path can be exercised deterministically.
    /// </summary>
    internal Func<string, WorkspaceResult<ReadFileResult>>? ReadFileOverride { get; set; }

    public WorkspaceResult<ReadFileResult> TryReadFile(string path)
    {
        Interlocked.Increment(ref _readFileCallCount);
        return ReadFileOverride is not null
            ? ReadFileOverride(path)
            : _inner.TryReadFile(path);
    }

    public WorkspaceResult<WriteFileResult> TryWriteFile(string path, string content)
    {
        Interlocked.Increment(ref _writeFileCallCount);
        return _inner.TryWriteFile(path, content);
    }

    public bool FileExists(string path)
    {
        Interlocked.Increment(ref _fileExistsCallCount);
        return _inner.FileExists(path);
    }

    public IEnumerable<string> GetFilePaths()
    {
        Interlocked.Increment(ref _getFilePathsCallCount);
        return _inner.GetFilePaths();
    }

    public ReadOnlyMemory<char> ReadFileAsMemory(string path) =>
        _inner.ReadFileAsMemory(path);

    public string ListDirectory(string directory, int maxDepth = 2) =>
        _inner.ListDirectory(directory, maxDepth);

    public WorkspaceResult<CompareExchangeResult> TryCompareExchange(string path, string expectedContent, string newContent)
    {
        Interlocked.Increment(ref _compareExchangeCallCount);
        return _inner.TryCompareExchange(path, expectedContent, newContent);
    }
}
