// DevUIApp — Demonstrates Foundry agents appearing in MAF DevUI
//
// Run this app and navigate to http://localhost:5250/devui to see the
// [FoundryAgent]-declared agents (DataAssistant, SummaryAgent) listed
// in the DevUI web interface and chat with them via Copilot.
//
// Requirements:
//   - GitHub Copilot CLI must be authenticated (run `gh auth login` first)
//   - No API keys needed — auth flows through your GitHub OAuth token

using Microsoft.Agents.AI.DevUI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.DevUI;
using NexusLabs.Foundry.Copilot;
using NexusLabs.Foundry.Needlr.MicrosoftAgentFramework;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5250");

var copilotSection = builder.Configuration.GetSection("Copilot");
var copilotOptions = new CopilotChatClientOptions
{
    DefaultModel = copilotSection["Model"] ?? "gpt-4.1",
};
IChatClient chatClient = new CopilotChatClient(copilotOptions);

// Needlr container setup with Foundry Agent Framework integration
var needlrServices = new Syringe()
    .UsingReflection()
    .UsingAgentFramework(af => af.UsingChatClient(chatClient))
    .BuildServiceProvider();

// Bridge the IAgentFactory from Needlr's service provider into the web host
var agentFactory = needlrServices.GetRequiredService<IAgentFactory>();

builder.Services.AddSingleton(agentFactory);
builder.Services.AddSingleton(chatClient);

// Bridge Foundry agents to DevUI using IAgentFactory for tool wiring and telemetry.
builder.Services.AddFoundryDevUI();

// MAF OpenAI hosting + DevUI infrastructure
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();
builder.AddDevUI();

var app = builder.Build();

// Map hosting and DevUI endpoints
app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();

// Verification endpoint: list registered agents as JSON
app.MapGet("/", () =>
{
    return Results.Content("""
        <!DOCTYPE html>
        <html><body>
        <h1>Foundry DevUI Example</h1>
        <ul>
          <li><a href="/devui">/devui</a> — MAF DevUI web interface</li>
          <li><a href="/v1/entities">/v1/entities</a> — Agent discovery API (JSON)</li>
        </ul>
        </body></html>
        """, "text/html");
});

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Foundry DevUI Example                                      ║");
Console.WriteLine($"║  LLM: Copilot ({copilotOptions.DefaultModel})                               ║");
Console.WriteLine("║  Open http://localhost:5250/devui in your browser            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");

app.Run();
