using HarnessCompatibilityProbe;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

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
};

var agent = new ProbeChatClient().AsHarnessAgent(options);

Console.WriteLine($"{agent.GetType().FullName}:{agent.Name}");
return 0;
