namespace NexusLabs.Foundry.Evaluation.Experiments;

/// <summary>
/// Snapshots one registered item-scope provider and the identity used for canonical publication.
/// </summary>
internal sealed record ExperimentItemScopeRegistration<TCase, TOutput>(
    string Name,
    bool IsRequired,
    ExperimentItemScopeFailureMode FailureMode,
    IExperimentItemScopeProvider<TCase, TOutput> Provider);
