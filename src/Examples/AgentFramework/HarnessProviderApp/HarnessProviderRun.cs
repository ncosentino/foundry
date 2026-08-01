using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.Copilot;
using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

namespace HarnessProviderApp;

/// <summary>
/// Selects a chat provider from configuration and runs the Harness scenario against it.
/// </summary>
internal static class HarnessProviderRun
{
    internal const string NotePath = "notes/summary.md";

    internal const string ScriptedNote =
        "The Foundry Harness bundle composes the upstream complete-bundle agent from explicit configuration.";

    internal static async Task<int> ExecuteAsync(IConfiguration configuration)
    {
        var harnessSection = configuration.GetSection("Harness");
        var providerName = harnessSection["Provider"] ?? "scripted";
        var enableWebSearch = bool.TryParse(harnessSection["EnableWebSearch"], out var webSearch)
            && webSearch;

        using IChatClient chatClient = CreateChatClient(providerName, configuration);
        Console.WriteLine($"Provider: {providerName}");
        if (!IsScripted(providerName))
        {
            Console.WriteLine(
                "This run calls a real model using your local GitHub Copilot credentials.");
        }

        using var services = new ServiceCollection()
            .AddFoundryAgentFramework()
            .AddTransient<HarnessProviderTools>()
            .BuildServiceProvider();
        var runner = new HarnessScenarioRunner(
            services,
            services.GetRequiredService<IAgentExecutionContextAccessor>());
        var scenario = new HarnessProviderScenario(chatClient, enableWebSearch);

        var result = await runner.RunAsync(scenario);

        Console.WriteLine($"Session: {result.SessionId}");
        Console.WriteLine($"Tools executed: {string.Join(", ", result.ExecutedGeneratedToolNames)}");
        Console.WriteLine($"Response: {result.ResponseText}");

        var note = result.Workspace.TryReadFile(NotePath);
        if (note.Success)
        {
            Console.WriteLine($"Workspace note: {note.Value.Content}");
        }

        if (!result.Succeeded)
        {
            Console.Error.WriteLine(
                result.ExecutionError?.Message
                ?? result.VerificationError?.Message
                ?? result.HarnessVerificationError?.Message
                ?? "The scenario failed without an error.");
            return 1;
        }

        Console.WriteLine("Harness run succeeded.");
        return 0;
    }

    private static IChatClient CreateChatClient(
        string providerName,
        IConfiguration configuration)
    {
        if (IsScripted(providerName))
        {
            return new ScriptedProviderChatClient();
        }

        if (!string.Equals(providerName, "copilot", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unknown Harness:Provider value '{providerName}'. Use 'scripted' or 'copilot'.");
        }

        var copilotSection = configuration.GetSection("Copilot");
        return new CopilotChatClient(new CopilotChatClientOptions
        {
            DefaultModel = copilotSection["Model"] ?? "claude-sonnet-4.5",
            // Auto resolves the GitHub Copilot CLI's local credentials first, so a developer who
            // is already signed in needs no token in configuration and no environment variable.
            TokenSource = CopilotTokenSource.Auto,
        });
    }

    private static bool IsScripted(string providerName) =>
        string.Equals(providerName, "scripted", StringComparison.OrdinalIgnoreCase);
}
