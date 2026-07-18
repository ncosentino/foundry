using System.Text.Json;

namespace NexusLabs.Foundry.Langfuse;

/// <summary>
/// Wire projection of a dataset returned by the Langfuse v2 dataset endpoints.
/// </summary>
internal sealed record LangfuseDatasetRef
{
    /// <summary>Gets the dataset identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the dataset name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the optional dataset description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets optional structured dataset metadata.</summary>
    public JsonElement? Metadata { get; init; }

    /// <summary>Gets the optional input JSON Schema.</summary>
    public JsonElement? InputSchema { get; init; }

    /// <summary>Gets the optional expected-output JSON Schema.</summary>
    public JsonElement? ExpectedOutputSchema { get; init; }

    /// <summary>Gets the dataset creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the dataset update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
