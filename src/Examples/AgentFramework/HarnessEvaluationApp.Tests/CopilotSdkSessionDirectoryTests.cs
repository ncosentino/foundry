namespace HarnessEvaluationApp.Tests;

public sealed class CopilotSdkSessionDirectoryTests
{
    [Fact]
    public void Create_UsesUniqueDisposableChildDirectories()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            $"foundry-copilot-sdk-session-tests-{Guid.NewGuid():N}");
        string firstPath;
        string secondPath;
        try
        {
            using (var first = CopilotSdkSessionDirectory.Create(rootDirectory))
            using (var second = CopilotSdkSessionDirectory.Create(rootDirectory))
            {
                firstPath = first.DirectoryPath;
                secondPath = second.DirectoryPath;
                Assert.NotEqual(firstPath, secondPath);
                Assert.StartsWith(rootDirectory, firstPath, StringComparison.Ordinal);
                Assert.StartsWith(rootDirectory, secondPath, StringComparison.Ordinal);
                Assert.True(Directory.Exists(firstPath));
                Assert.True(Directory.Exists(secondPath));
            }

            Assert.False(Directory.Exists(firstPath));
            Assert.False(Directory.Exists(secondPath));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
