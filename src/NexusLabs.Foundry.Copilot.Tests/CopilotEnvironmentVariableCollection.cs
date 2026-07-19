namespace NexusLabs.Foundry.Copilot.Tests;

/// <summary>
/// Serializes tests that mutate process-wide Copilot authentication environment variables.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CopilotEnvironmentVariableCollection
{
    /// <summary>
    /// Gets the xUnit collection name used by environment-variable tests.
    /// </summary>
    public const string Name = "Copilot environment variables";
}
