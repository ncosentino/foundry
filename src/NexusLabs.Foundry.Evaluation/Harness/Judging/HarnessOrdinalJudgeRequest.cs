namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Provides captured post-hoc evidence for one advisory ordinal judge invocation.
/// </summary>
public sealed record HarnessOrdinalJudgeRequest
{
    /// <summary>
    /// Initializes one ordinal judge request.
    /// </summary>
    /// <param name="caseId">The hosted case identifier.</param>
    /// <param name="casePrompt">The frozen case prompt.</param>
    /// <param name="deterministicReference">The deterministic reference summary.</param>
    /// <param name="generatorModelFamily">The model family that generated the response.</param>
    /// <param name="response">The captured final-response artifact text.</param>
    /// <param name="trajectory">The normalized tool-call/result trajectory.</param>
    /// <exception cref="ArgumentException">A required value is blank.</exception>
    public HarnessOrdinalJudgeRequest(
        string caseId,
        string casePrompt,
        string deterministicReference,
        string generatorModelFamily,
        string response,
        string trajectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(casePrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorModelFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(trajectory);
        CaseId = caseId;
        CasePrompt = casePrompt;
        DeterministicReference = deterministicReference;
        GeneratorModelFamily = generatorModelFamily;
        Response = response;
        Trajectory = trajectory;
    }

    /// <summary>Gets the hosted case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the frozen case prompt.</summary>
    public string CasePrompt { get; }

    /// <summary>Gets the deterministic reference summary.</summary>
    public string DeterministicReference { get; }

    /// <summary>Gets the model family that generated the response.</summary>
    public string GeneratorModelFamily { get; }

    /// <summary>Gets the captured final-response artifact text.</summary>
    public string Response { get; }

    /// <summary>Gets the normalized tool-call/result trajectory.</summary>
    public string Trajectory { get; }
}
