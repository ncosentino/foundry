using System.Text.Json;

using AutonomousPhasePipelineApp.Evaluation;

string model = Environment.GetEnvironmentVariable("EVAL_MODEL")
    ?? "gpt-4.1";
string commitSha = Environment.GetEnvironmentVariable("GITHUB_SHA")
    ?? "local-uncommitted";
string outputDirectory =
    Environment.GetEnvironmentVariable("EVAL_OUTPUT_DIRECTORY")
    ?? Path.Combine(
        "artifacts",
        "autonomous-phase-evaluation");
Directory.CreateDirectory(outputDirectory);
string outputPath = Path.Combine(
    outputDirectory,
    "provider-probe.json");

ProviderProbeResult result = await CopilotProviderProbe.RunAsync(
    model,
    commitSha,
    CancellationToken.None);
await using (FileStream stream = File.Create(outputPath))
{
    await JsonSerializer.SerializeAsync(
        stream,
        result,
        ProviderProbeJsonContext.Default.ProviderProbeResult);
}

Console.WriteLine(
    $"AutonomousPhaseEvaluation:provider-probe:{(result.Succeeded ? "passed" : "blocked")}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:stage:{result.Stage}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:artifact:{Path.GetFullPath(outputPath)}");
if (!result.Succeeded)
{
    Console.Error.WriteLine(
        $"AutonomousPhaseEvaluation:error:{result.ErrorType}:{result.ErrorMessage}");
    return 1;
}

return 0;
