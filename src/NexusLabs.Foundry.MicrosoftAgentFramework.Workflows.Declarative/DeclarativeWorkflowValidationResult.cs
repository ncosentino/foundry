namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// The outcome of parsing a declarative workflow document.
/// </summary>
/// <remarks>
/// Declarative workflows have no published JSON Schema, so parsing is the only validation available
/// and it happens at run time. This type exists so a host can validate a document deliberately —
/// at startup, in a test, or in a lint step — rather than discovering an authoring error partway
/// through a run.
/// </remarks>
public sealed record DeclarativeWorkflowValidationResult
{
    private DeclarativeWorkflowValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets whether the document parsed successfully.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the parse failure detail, or <see langword="null"/> when <see cref="IsValid"/> is
    /// <see langword="true"/>.
    /// </summary>
    public string? ErrorMessage { get; }

    internal static DeclarativeWorkflowValidationResult Valid() => new(true, null);

    internal static DeclarativeWorkflowValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}
