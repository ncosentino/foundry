namespace HarnessEvaluationApp;

internal sealed class CopilotSdkSessionDirectory : IDisposable
{
    private CopilotSdkSessionDirectory(string directoryPath)
    {
        DirectoryPath = directoryPath;
    }

    internal string DirectoryPath { get; }

    internal static CopilotSdkSessionDirectory Create(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        Directory.CreateDirectory(rootDirectory);
        var directoryPath = Path.Combine(
            rootDirectory,
            $"session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return new CopilotSdkSessionDirectory(directoryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
