namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// The requested-versus-effective disposition of a single <see cref="FoundryHarnessFeature"/>
/// dimension for a specific <see cref="FoundryHarnessAgentConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Instances are produced exclusively by <see cref="FoundryHarnessAgentFactory.DescribeEffectiveDefaults"/>.
/// The constructor is not public; use the internal <see cref="Create"/> factory within the bundle
/// assembly to produce validated instances.
/// </para>
/// <para>
/// All properties are read-only. External callers may use <see langword="with"/> to shallow-copy an
/// instance but cannot change any property values.
/// </para>
/// </remarks>
public sealed record FoundryHarnessFeatureDisposition
{
    private FoundryHarnessFeatureDisposition() { }

    /// <summary>Gets the bundle dimension this disposition describes.</summary>
    public FoundryHarnessFeature Feature { get; private init; }

    /// <summary>Gets what the caller asked for, if anything.</summary>
    public FoundryHarnessFeatureRequestedState RequestedState { get; private init; }

    /// <summary>Gets what will actually happen once the bundle agent is built.</summary>
    public FoundryHarnessFeatureEffectiveState EffectiveState { get; private init; }

    /// <summary>
    /// Gets a human-readable explanation when <see cref="EffectiveState"/> cannot be changed by the
    /// caller (<see cref="FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable"/>) or when this
    /// dimension is not yet exposed by <see cref="FoundryHarnessAgentConfiguration"/>. <see langword="null"/>
    /// when the requested state was fully honored with no caveats.
    /// </summary>
    public string? Limitation { get; private init; }

    /// <summary>
    /// Creates a validated <see cref="FoundryHarnessFeatureDisposition"/> enforcing the coherence
    /// invariants between <paramref name="effectiveState"/>, <paramref name="requestedState"/>, and
    /// <paramref name="limitation"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Invariants enforced:
    /// <list type="bullet">
    /// <item><description>All enum arguments must be defined values.</description></item>
    /// <item><description>
    /// <see cref="FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable"/> and
    /// <see cref="FoundryHarnessFeatureRequestedState.NotConfigurable"/> must always co-occur:
    /// either both are set or neither is.
    /// </description></item>
    /// <item><description>
    /// <paramref name="limitation"/> must be non-null and non-whitespace when
    /// <paramref name="effectiveState"/> is <see cref="FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable"/>.
    /// </description></item>
    /// <item><description>
    /// When <paramref name="limitation"/> is non-null it must not be whitespace-only.
    /// </description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal static FoundryHarnessFeatureDisposition Create(
        FoundryHarnessFeature feature,
        FoundryHarnessFeatureRequestedState requestedState,
        FoundryHarnessFeatureEffectiveState effectiveState,
        string? limitation)
    {
        if (!Enum.IsDefined(feature))
        {
            throw new ArgumentOutOfRangeException(
                nameof(feature), feature,
                $"Feature must be a defined {nameof(FoundryHarnessFeature)} value.");
        }

        if (!Enum.IsDefined(requestedState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedState), requestedState,
                $"RequestedState must be a defined {nameof(FoundryHarnessFeatureRequestedState)} value.");
        }

        if (!Enum.IsDefined(effectiveState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveState), effectiveState,
                $"EffectiveState must be a defined {nameof(FoundryHarnessFeatureEffectiveState)} value.");
        }

        bool isAlwaysOnUnavoidable = effectiveState == FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable;
        bool isNotConfigurable = requestedState == FoundryHarnessFeatureRequestedState.NotConfigurable;

        if (isAlwaysOnUnavoidable != isNotConfigurable)
        {
            throw new ArgumentException(
                $"{nameof(FoundryHarnessFeatureEffectiveState)}.{nameof(FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable)} " +
                $"and {nameof(FoundryHarnessFeatureRequestedState)}.{nameof(FoundryHarnessFeatureRequestedState.NotConfigurable)} " +
                $"must always co-occur: either both are set or neither is.",
                nameof(effectiveState));
        }

        if (isAlwaysOnUnavoidable && string.IsNullOrWhiteSpace(limitation))
        {
            throw new ArgumentException(
                $"A non-empty {nameof(Limitation)} is required when {nameof(EffectiveState)} is " +
                $"{nameof(FoundryHarnessFeatureEffectiveState)}.{nameof(FoundryHarnessFeatureEffectiveState.AlwaysOnUnavoidable)}.",
                nameof(limitation));
        }

        if (limitation is not null && string.IsNullOrWhiteSpace(limitation))
        {
            throw new ArgumentException(
                $"{nameof(Limitation)} must be non-whitespace when provided.",
                nameof(limitation));
        }

        return new FoundryHarnessFeatureDisposition
        {
            Feature = feature,
            RequestedState = requestedState,
            EffectiveState = effectiveState,
            Limitation = limitation,
        };
    }
}
