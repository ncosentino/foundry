namespace NexusLabs.Foundry.Copilot.Tests;

internal static class LiveCopilotTestGuard
{
    internal static void RequirePitCrewOptIn()
    {
        var isGitHubActions = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isPitCrew = string.Equals(
            Environment.GetEnvironmentVariable("FOUNDRY_LIVE_COPILOT_RUNNER"),
            "pitcrew",
            StringComparison.OrdinalIgnoreCase);
        var isApproved = string.Equals(
            Environment.GetEnvironmentVariable("FOUNDRY_ALLOW_LIVE_COPILOT_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (!isGitHubActions || !isPitCrew || !isApproved)
        {
            Assert.Skip(
                "Live Copilot tests require explicit approval on a PitCrew GitHub Actions runner.");
        }
    }
}
