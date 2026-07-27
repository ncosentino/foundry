namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Provides captured post-hoc evidence for one advisory pairwise judge invocation.
/// </summary>
public sealed record HarnessPairwiseJudgeRequest
{
    /// <summary>
    /// Initializes one pairwise judge request.
    /// </summary>
    /// <param name="caseId">The hosted case identifier.</param>
    /// <param name="casePrompt">The frozen case prompt.</param>
    /// <param name="deterministicReference">The deterministic reference summary.</param>
    /// <param name="generatorModelFamily">The model family that generated the candidates.</param>
    /// <param name="leftResponse">The left final-response artifact text.</param>
    /// <param name="rightResponse">The right final-response artifact text.</param>
    /// <param name="leftTrajectory">The normalized left tool-call/result trajectory.</param>
    /// <param name="rightTrajectory">The normalized right tool-call/result trajectory.</param>
    /// <exception cref="ArgumentException">A required value is blank.</exception>
    public HarnessPairwiseJudgeRequest(
        string caseId,
        string casePrompt,
        string deterministicReference,
        string generatorModelFamily,
        string leftResponse,
        string rightResponse,
        string leftTrajectory,
        string rightTrajectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(casePrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorModelFamily);
        ArgumentException.ThrowIfNullOrWhiteSpace(leftResponse);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightResponse);
        ArgumentException.ThrowIfNullOrWhiteSpace(leftTrajectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(rightTrajectory);
        CaseId = caseId;
        CasePrompt = casePrompt;
        DeterministicReference = deterministicReference;
        GeneratorModelFamily = generatorModelFamily;
        LeftResponse = leftResponse;
        RightResponse = rightResponse;
        LeftTrajectory = leftTrajectory;
        RightTrajectory = rightTrajectory;
    }

    /// <summary>Gets the hosted case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the frozen case prompt.</summary>
    public string CasePrompt { get; }

    /// <summary>Gets the deterministic reference summary.</summary>
    public string DeterministicReference { get; }

    /// <summary>Gets the model family that generated the candidates.</summary>
    public string GeneratorModelFamily { get; }

    /// <summary>Gets the left final-response artifact text.</summary>
    public string LeftResponse { get; }

    /// <summary>Gets the right final-response artifact text.</summary>
    public string RightResponse { get; }

    /// <summary>Gets the normalized left tool-call/result trajectory.</summary>
    public string LeftTrajectory { get; }

    /// <summary>Gets the normalized right tool-call/result trajectory.</summary>
    public string RightTrajectory { get; }
}
