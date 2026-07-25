namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The deterministic set of entry ids a <see cref="HarnessHybridContextPolicy"/> requires an upstream
/// reducer to preserve, in their original relative order. Never includes a
/// <see cref="HarnessContextEntryKind.Summary"/> entry — a summary is always reducible and can never
/// substitute for a required entry.
/// </summary>
internal sealed record HarnessPreservationSelection
{
    private HarnessPreservationSelection(
        IReadOnlyList<string> requiredEntryIds, string preservationLabel, int preservationVersion)
    {
        RequiredEntryIds = requiredEntryIds;
        PreservationLabel = preservationLabel;
        PreservationVersion = preservationVersion;
    }

    /// <summary>
    /// Every entry id the policy requires an upstream reducer to preserve, ordered exactly as the
    /// entries appeared in the original entry set this selection was computed from.
    /// </summary>
    internal IReadOnlyList<string> RequiredEntryIds { get; }

    /// <summary>The originating policy's declared preservation scheme label.</summary>
    internal string PreservationLabel { get; }

    /// <summary>The originating policy's declared preservation scheme version.</summary>
    internal int PreservationVersion { get; }

    /// <exception cref="ArgumentNullException">
    /// <paramref name="requiredEntryIds"/> or <paramref name="preservationLabel"/> is <see langword="null"/>.
    /// </exception>
    internal static HarnessPreservationSelection Create(
        IReadOnlyList<string> requiredEntryIds, string preservationLabel, int preservationVersion)
    {
        ArgumentNullException.ThrowIfNull(requiredEntryIds);
        ArgumentNullException.ThrowIfNull(preservationLabel);

        return new HarnessPreservationSelection(requiredEntryIds, preservationLabel, preservationVersion);
    }
}
