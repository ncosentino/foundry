namespace NexusLabs.Foundry.MicrosoftAgentFramework.Workflows.Declarative;

/// <summary>
/// Thrown when a declarative workflow document cannot be parsed.
/// </summary>
/// <remarks>
/// Upstream reports authoring errors through several exception types originating in the Power
/// Platform object model. They are normalized here so a host can catch one type, and so the parser
/// detail stays available on <see cref="Exception.InnerException"/> without being the thing callers
/// have to match on.
/// </remarks>
public sealed class DeclarativeWorkflowParseException : Exception
{
    /// <param name="innerException">The underlying parse failure.</param>
    public DeclarativeWorkflowParseException(Exception innerException)
        : base(
            $"The declarative workflow document could not be parsed: {innerException.Message}",
            innerException)
    {
    }
}
