namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Thrown when a <see cref="HarnessCaseSetManifest"/> is structurally invalid — for example a
/// malformed document, a hosted ID set that is not exactly the frozen set, a non-canonical trial
/// count, or a missing/malformed deterministic reference. A malformed manifest prevents the hosted
/// run from starting.
/// </summary>
public sealed class HarnessCaseSetManifestException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HarnessCaseSetManifestException"/> class.
    /// </summary>
    /// <param name="message">A description of why the manifest is invalid.</param>
    public HarnessCaseSetManifestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HarnessCaseSetManifestException"/> class.
    /// </summary>
    /// <param name="message">A description of why the manifest is invalid.</param>
    /// <param name="innerException">The underlying cause, such as a JSON parse failure.</param>
    public HarnessCaseSetManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
