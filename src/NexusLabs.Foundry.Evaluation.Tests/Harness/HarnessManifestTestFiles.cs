using System.IO;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

/// <summary>
/// Locates the on-disk <c>harness-001</c> v1.0 manifest by walking up from the test output directory.
/// </summary>
internal static class HarnessManifestTestFiles
{
    private const string RelativeManifestPath =
        "artifacts/eval/case-sets/harness-001/v1.0/manifest.json";

    /// <summary>
    /// Attempts to read the frozen manifest JSON from disk. Returns <see langword="null"/> when the
    /// artifact cannot be located (for example when the tests run from a packaged output without the
    /// repository tree).
    /// </summary>
    /// <returns>The manifest JSON text, or <see langword="null"/> when not found.</returns>
    public static string? TryReadManifestJson()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, RelativeManifestPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        return null;
    }
}
