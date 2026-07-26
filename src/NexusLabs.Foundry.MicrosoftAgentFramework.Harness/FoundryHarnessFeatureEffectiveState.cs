namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Describes what will actually happen for a single <see cref="FoundryHarnessFeature"/>
/// dimension once the upstream bundle agent is constructed.
/// </summary>
public enum FoundryHarnessFeatureEffectiveState
{
    /// <summary>
    /// The dimension will be disabled (or, for opt-in dimensions, will not be included).
    /// </summary>
    Disabled,

    /// <summary>
    /// The dimension will be enabled.
    /// </summary>
    Enabled,

    /// <summary>
    /// The upstream bundle unconditionally enables this dimension and provides no supported
    /// way to disable it. Reported explicitly so callers are never told a default is disabled
    /// when the upstream package cannot actually disable it.
    /// </summary>
    AlwaysOnUnavoidable,
}
