namespace NexusLabs.Foundry.Langfuse.Tests;

/// <summary>
/// Captures the hosted dataset fields projected by a case-source mapping test.
/// </summary>
internal sealed record MappedLangfuseDatasetCase(
    string Input,
    string ExpectedOutput,
    string Metadata,
    string? SourceTraceId,
    string? SourceObservationId);
