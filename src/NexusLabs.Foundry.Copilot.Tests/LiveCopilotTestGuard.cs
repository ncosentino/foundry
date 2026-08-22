namespace NexusLabs.Foundry.Copilot.Tests;

internal static class LiveCopilotTestGuard
{
    internal static void RequireLocalOptIn()
    {
        var isGitHubActions = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isApproved = string.Equals(
            Environment.GetEnvironmentVariable("FOUNDRY_ALLOW_LIVE_COPILOT_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (isGitHubActions || !isApproved)
        {
            Assert.Skip(
                "Live Copilot tests require explicit local opt-in and are prohibited in GitHub Actions.");
        }
    }
}
