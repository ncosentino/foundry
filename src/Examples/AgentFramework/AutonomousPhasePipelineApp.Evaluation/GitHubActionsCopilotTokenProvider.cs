using NexusLabs.Foundry.Copilot;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class GitHubActionsCopilotTokenProvider :
    ICopilotTokenProvider
{
    private readonly string _token = ResolveToken();

    public Task<string> GetTokenAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_token);
    }

    private static string ResolveToken()
    {
        string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? throw new InvalidOperationException(
                "GITHUB_TOKEN is required for hosted Copilot evaluation.");
        if (!token.StartsWith("ghs_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "GITHUB_TOKEN is not a GitHub Actions installation token.");
        }

        return token;
    }
}
