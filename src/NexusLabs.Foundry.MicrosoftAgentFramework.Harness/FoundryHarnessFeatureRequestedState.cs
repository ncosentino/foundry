namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Describes what a caller explicitly asked for regarding a single
/// <see cref="FoundryHarnessFeature"/> dimension.
/// </summary>
public enum FoundryHarnessFeatureRequestedState
{
    /// <summary>
    /// The dimension is not configurable through <see cref="FoundryHarnessAgentConfiguration"/>;
    /// no request is possible.
    /// </summary>
    NotConfigurable,

    /// <summary>
    /// The dimension is configurable but the caller did not opt into it (an opt-in feature
    /// left at its "not supplied" value, such as an omitted store or evaluator collection).
    /// </summary>
    NotRequested,

    /// <summary>
    /// The caller explicitly requested this dimension be enabled.
    /// </summary>
    RequestedEnabled,

    /// <summary>
    /// The caller explicitly requested this dimension be disabled.
    /// </summary>
    RequestedDisabled,
}
