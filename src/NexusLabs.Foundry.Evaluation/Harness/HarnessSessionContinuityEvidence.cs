namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Per-item evidence for the conversation/decision continuity dimension. Session continuity is scored
/// as successful only when every required decision reference and required structured-state key is
/// present in the run's retained session state.
/// </summary>
public sealed record HarnessSessionContinuityEvidence
{
    /// <summary>Gets the decision references the case requires the run to have retained.</summary>
    public required IReadOnlyList<string> RequiredDecisionReferences { get; init; }

    /// <summary>Gets the decision references actually present in the run's retained session state.</summary>
    public required IReadOnlyList<string> PresentDecisionReferences { get; init; }

    /// <summary>Gets the structured session-state keys the case requires the run to have retained.</summary>
    public required IReadOnlyList<string> RequiredStateKeys { get; init; }

    /// <summary>Gets the structured session-state keys actually present in the run's retained state.</summary>
    public required IReadOnlyList<string> PresentStateKeys { get; init; }
}
