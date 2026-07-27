namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item evidence for the cancellation/timeout dimension: the terminal category the case expected
/// for this run, the category actually observed, and whether the run nonetheless produced a
/// success-shaped output. A cancellation/timeout is scored appropriate only when the observed category
/// matches the expected category and no success-shaped output was produced.
/// </summary>
public sealed record HarnessCancellationEvidence
{
    /// <summary>Gets the terminal category the case expected for this run.</summary>
    public required HarnessRunTerminalCategory ExpectedCategory { get; init; }

    /// <summary>Gets the terminal category actually observed for this run.</summary>
    public required HarnessRunTerminalCategory ObservedCategory { get; init; }

    /// <summary>
    /// Gets a value indicating whether the run produced a success-shaped output despite a
    /// cancellation/timeout terminal category.
    /// </summary>
    public required bool ProducedSuccessShapedOutput { get; init; }
}
