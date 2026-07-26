namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// Describes whether, and how, a single <see cref="FoundryHarnessFeature"/> dimension that has a
/// configurable backing object (a store, source, or options instance) is backed for a specific
/// <see cref="FoundryHarnessAgentConfiguration"/>.
/// </summary>
/// <remarks>
/// This is a separate axis from <see cref="FoundryHarnessFeatureRequestedState"/> and
/// <see cref="FoundryHarnessFeatureEffectiveState"/>: a dimension can be effectively enabled while
/// still leaving the caller's choice of backing implementation (upstream default versus explicit
/// instance) unreported by those two enums alone. See
/// <see cref="FoundryHarnessFeatureDisposition.BackingSelection"/>.
/// </remarks>
public enum FoundryHarnessFeatureBackingSelection
{
    /// <summary>
    /// This dimension has no configurable backing object, or the dimension is disabled/not
    /// exposed, so no backing decision applies.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// No backing object was supplied; the upstream bundle substitutes its own built-in default
    /// implementation. See <see cref="FoundryHarnessFeatureDisposition.BackingDescription"/> for the
    /// exact default semantics, sourced from the upstream XML documentation.
    /// </summary>
    UpstreamDefault,

    /// <summary>
    /// The caller supplied an explicit backing object, which the upstream bundle uses directly in
    /// place of its built-in default.
    /// </summary>
    CallerSupplied,
}
