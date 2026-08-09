using System.Text.Json;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedIncrementalResultWriter(
    string outputDirectory,
    string runId)
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    internal async Task WriteBlockAsync(
        HostedEvaluationBlockResult result,
        CancellationToken cancellationToken)
    {
        string blockDirectory = Path.Combine(
            outputDirectory,
            "blocks");
        Directory.CreateDirectory(blockDirectory);
        string path = Path.Combine(
            blockDirectory,
            $"{Sanitize(result.BlockId)}.json");
        await WriteAtomicAsync(
            path,
            result,
            ProviderProbeJsonContext.Default.HostedEvaluationBlockResult,
            cancellationToken);
    }

    internal Task WriteStatusAsync(
        string state,
        int completedBlocks,
        int totalBlocks,
        CancellationToken cancellationToken) =>
        WriteAtomicAsync(
            Path.Combine(
                outputDirectory,
                "run-status.json"),
            new HostedEvaluationRunStatus(
                SchemaVersion: 1,
                RunId: runId,
                State: state,
                CompletedBlocks: completedBlocks,
                TotalBlocks: totalBlocks,
                UpdatedAtUtc: DateTimeOffset.UtcNow),
            ProviderProbeJsonContext.Default.HostedEvaluationRunStatus,
            cancellationToken);

    private async Task WriteAtomicAsync<T>(
        string path,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path)
                ?? outputDirectory);
            string temporaryPath =
                $"{path}.{Guid.NewGuid():N}.tmp";
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    typeInfo,
                    cancellationToken);
            }

            File.Move(
                temporaryPath,
                path,
                overwrite: true);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static string Sanitize(string value) =>
        string.Concat(
            value.Select(character =>
                char.IsLetterOrDigit(character) ||
                character is '-' or '_'
                    ? character
                    : '-'));
}
