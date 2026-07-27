namespace NexusLabs.Foundry.Evaluation.Harness.Judging;

/// <summary>
/// Represents invalid or unparseable structured output returned by an advisory Harness judge.
/// </summary>
public sealed class HarnessJudgeEvaluationException : Exception
{
    /// <summary>
    /// Initializes an exception with a descriptive message.
    /// </summary>
    /// <param name="message">The failure description.</param>
    public HarnessJudgeEvaluationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes an exception with a descriptive message and underlying parse failure.
    /// </summary>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying exception.</param>
    public HarnessJudgeEvaluationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
