using AotHarnessApp;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

using var services = new ServiceCollection()
    .AddFoundryAgentFramework()
    .AddTransient<AotHarnessTool>()
    .BuildServiceProvider();
var runner = new HarnessScenarioRunner(
    services,
    services.GetRequiredService<IAgentExecutionContextAccessor>());
var scenario = new AotHarnessScenario();

HarnessScenarioRunResult result;
try
{
    result = await runner.RunAsync(scenario);
}
catch (HarnessScenarioToolResolutionException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

if (result.Session is null)
{
    Console.Error.WriteLine("The Harness scenario did not create a session.");
    return 2;
}

if (!result.ExecutedGeneratedToolNames.SequenceEqual(["WriteWorkspace"], StringComparer.Ordinal))
{
    Console.Error.WriteLine("The generated workspace tool did not execute exactly once.");
    return 3;
}

if (!string.Equals(
    result.ResponseText,
    AotHarnessScenario.ExpectedResponse,
    StringComparison.Ordinal))
{
    Console.Error.WriteLine("The scripted Harness response did not match the expected value.");
    return 4;
}

var output = result.Workspace.TryReadFile(AotHarnessScenario.OutputPath);
if (!output.Success ||
    !string.Equals(
        output.Value.Content,
        AotHarnessScenario.ExpectedWorkspaceContent,
        StringComparison.Ordinal))
{
    Console.Error.WriteLine("The generated tool did not write the expected workspace artifact.");
    return 5;
}

if (!result.Succeeded || scenario.EffectiveDefaults is null)
{
    Console.Error.WriteLine(
        result.ExecutionError?.Message
        ?? result.VerificationError?.Message
        ?? result.HarnessVerificationError?.Message
        ?? "The effective-default report was unavailable.");
    return 6;
}

Console.WriteLine(
    $"AotHarnessApp:{result.SessionId}:{result.ResponseText}:" +
    $"{string.Join(',', result.ExecutedGeneratedToolNames)}");
return 0;
