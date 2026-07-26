namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

/// <summary>
/// A complete requested-versus-effective report of every <see cref="FoundryHarnessFeature"/>
/// dimension for a specific <see cref="FoundryHarnessAgentConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// Obtain an instance from
/// <see cref="FoundryHarnessAgentFactory.DescribeEffectiveDefaults(FoundryHarnessAgentConfiguration)"/>
/// or its service-aware overload.
/// Because this report is a pure function of the configuration, it is valid both before and
/// after calling <see cref="FoundryHarnessAgentFactory.Create(FoundryHarnessAgentConfiguration)"/>
/// with the same configuration instance.
/// </para>
/// <para>
/// The constructor is not public; use the internal <see cref="Create"/> factory within the bundle
/// assembly to produce validated instances. All properties are read-only; the
/// <see cref="Dispositions"/> list is a defensive copy.
/// </para>
/// </remarks>
public sealed record FoundryHarnessEffectiveDefaults
{
    private FoundryHarnessEffectiveDefaults() { }

    /// <summary>
    /// Gets one <see cref="FoundryHarnessFeatureDisposition"/> per <see cref="FoundryHarnessFeature"/>
    /// value. The list is a defensive copy; external mutations to the original list have no effect.
    /// </summary>
    public IReadOnlyList<FoundryHarnessFeatureDisposition> Dispositions { get; private init; } = [];

    /// <summary>
    /// Creates a validated <see cref="FoundryHarnessEffectiveDefaults"/>, ensuring every
    /// <see cref="FoundryHarnessFeature"/> value is represented exactly once in
    /// <paramref name="dispositions"/> (no duplicates, no missing features). Stores a defensive copy.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="dispositions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="dispositions"/> contains a duplicate <see cref="FoundryHarnessFeature"/>, or is
    /// missing at least one <see cref="FoundryHarnessFeature"/> value.
    /// </exception>
    internal static FoundryHarnessEffectiveDefaults Create(
        IReadOnlyList<FoundryHarnessFeatureDisposition> dispositions)
    {
        ArgumentNullException.ThrowIfNull(dispositions);

        var allFeatures = Enum.GetValues<FoundryHarnessFeature>();
        var seen = new HashSet<FoundryHarnessFeature>();

        foreach (var disposition in dispositions)
        {
            if (!seen.Add(disposition.Feature))
            {
                throw new ArgumentException(
                    $"Dispositions contains a duplicate entry for " +
                    $"{nameof(FoundryHarnessFeature)}.{disposition.Feature}.",
                    nameof(dispositions));
            }
        }

        foreach (var feature in allFeatures)
        {
            if (!seen.Contains(feature))
            {
                throw new ArgumentException(
                    $"Dispositions is missing an entry for " +
                    $"{nameof(FoundryHarnessFeature)}.{feature}.",
                    nameof(dispositions));
            }
        }

        // Defensive copy so callers cannot mutate the backing list after construction.
        return new FoundryHarnessEffectiveDefaults
        {
            Dispositions = dispositions.ToList().AsReadOnly(),
        };
    }

    /// <summary>
    /// Gets the disposition recorded for the given <paramref name="feature"/>.
    /// </summary>
    /// <param name="feature">The bundle dimension to look up.</param>
    /// <returns>The matching <see cref="FoundryHarnessFeatureDisposition"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// No disposition was recorded for <paramref name="feature"/>.
    /// </exception>
    public FoundryHarnessFeatureDisposition GetDisposition(FoundryHarnessFeature feature)
    {
        foreach (var disposition in Dispositions)
        {
            if (disposition.Feature == feature)
                return disposition;
        }

        throw new InvalidOperationException(
            $"No disposition was recorded for Harness bundle feature '{feature}'.");
    }
}
