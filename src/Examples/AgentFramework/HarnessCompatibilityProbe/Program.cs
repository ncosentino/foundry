using HarnessCompatibilityProbe;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework;

if (!AgentFrameworkGeneratedBootstrap.TryGetAIFunctionProvider(out var functionProvider) ||
    !functionProvider.TryGetFunctions(
        typeof(ProbeFunctions),
        ProbeServiceProvider.Instance,
        out var functions) ||
    functions.Count != 1 ||
    !string.Equals(functions[0].Name, "Echo", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Generated tool provider was not available.");
    return 1;
}

var options = new HarnessAgentOptions
{
    Name = "foundry-harness-compatibility-probe",
    HarnessInstructions = string.Empty,
    DisableAgentModeProvider = true,
    DisableAgentSkillsProvider = true,
    DisableFileMemory = true,
    DisableOpenTelemetry = true,
    DisableTodoProvider = true,
    DisableWebSearch = true,
    ChatOptions = new ChatOptions
    {
        Tools = [.. functions],
    },
};

var agent = new ProbeChatClient(functions[0].Name).AsHarnessAgent(options);
var response = await agent.RunAsync("Execute the generated probe tool.");
var responseText = response.GetText();

Console.WriteLine($"{agent.GetType().FullName}:{agent.Name}:{responseText}");
if (!string.Equals(responseText, "tool-result:aot", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Generated tool result did not round-trip through Harness.");
    return 2;
}

return 0;
