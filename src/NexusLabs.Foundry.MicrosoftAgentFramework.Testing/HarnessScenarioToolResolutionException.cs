namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

/// <summary>
/// Indicates that a Harness scenario's declared generated functions could not be resolved
/// completely and uniquely from the source-generated provider.
/// </summary>
public sealed class HarnessScenarioToolResolutionException : Exception
{
    internal HarnessScenarioToolResolutionException(
        string message,
        bool generatedProviderUnavailable,
        IReadOnlyList<Type> missingFunctionTypes,
        IReadOnlyList<string> duplicateToolNames)
        : base(message)
    {
        GeneratedProviderUnavailable = generatedProviderUnavailable;
        MissingFunctionTypes = missingFunctionTypes;
        DuplicateToolNames = duplicateToolNames;
    }

    /// <summary>
    /// Gets whether no source-generated function provider was registered.
    /// </summary>
    public bool GeneratedProviderUnavailable { get; }

    /// <summary>
    /// Gets declared function-group types absent from the generated provider.
    /// </summary>
    public IReadOnlyList<Type> MissingFunctionTypes { get; }

    /// <summary>
    /// Gets duplicate generated tool names found across the declared function groups.
    /// </summary>
    public IReadOnlyList<string> DuplicateToolNames { get; }
}
