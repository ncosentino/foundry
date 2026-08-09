using System.Text.Json;

using AutonomousPhasePipelineApp.Evaluation;

using NexusLabs.Foundry.Langfuse;

HostedEvaluationProtocol protocol =
    HostedEvaluationProtocol.FromEnvironment();
Directory.CreateDirectory(protocol.OutputDirectory);

var langfuseOptions = LangfuseOptions.FromEnvironment();
langfuseOptions.ServiceName =
    "foundry-autonomous-phase-evaluation";
langfuseOptions.ServiceVersion = protocol.CommitSha;
langfuseOptions.Environment = "evaluation-protocol";
langfuseOptions.Release = protocol.CommitSha;
langfuseOptions.ScoreFailureMode =
    LangfuseScoreFailureMode.Strict;
langfuseOptions.AdditionalActivitySources.Add(
    HostedEvaluationActivitySource.Name);
using ILangfuseSession langfuse =
    LangfuseTelemetry.Start(langfuseOptions);

HostedEvaluationDatasetPublication datasetPublication =
    await HostedEvaluationLangfusePublisher.PublishDatasetAsync(
        langfuse,
        protocol,
        CancellationToken.None);
await WriteJsonAsync(
    Path.Combine(
        protocol.OutputDirectory,
        "dataset-publication.json"),
    datasetPublication,
    HostedEvaluationJsonContext.Default
        .HostedEvaluationDatasetPublication);
await WriteJsonAsync(
    Path.Combine(
        protocol.OutputDirectory,
        "protocol-dataset.json"),
    HostedEvaluationDatasetCatalog.Create(),
    HostedEvaluationJsonContext.Default
        .IReadOnlyListHostedEvaluationDatasetItem);

LangfuseShutdownOutcome shutdown = langfuse.Shutdown(
    langfuseOptions.ShutdownTimeout);
Console.WriteLine(
    "AutonomousPhaseEvaluation:mode:protocol-only");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:dataset:{protocol.DatasetName}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:dataset-items:{datasetPublication.ObservedItems}/{datasetPublication.ExpectedItems}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:dataset-readback:{datasetPublication.ReadBackVerified}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:langfuse-enabled:{langfuse.IsEnabled}");
Console.WriteLine(
    $"AutonomousPhaseEvaluation:shutdown:{shutdown.Traces}");
return langfuse.IsEnabled &&
    !datasetPublication.ReadBackVerified
        ? 1
        : 0;

static async Task WriteJsonAsync<T>(
    string path,
    T value,
    System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
{
    await using FileStream stream = File.Create(path);
    await JsonSerializer.SerializeAsync(
        stream,
        value,
        typeInfo);
}
