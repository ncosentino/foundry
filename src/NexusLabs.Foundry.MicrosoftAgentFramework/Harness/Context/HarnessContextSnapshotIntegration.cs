namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// The explicit, required session/snapshot integration hook a <see cref="HarnessHybridProfile"/> must
/// supply: given the freshly-adapted baseline entries for one provider call — themselves derived only
/// from the actual, already-loaded, reference-bearing request messages — returns the
/// <see cref="IHarnessContextSnapshotProvider"/> <see cref="HarnessContextAssembler"/> should observe for
/// that call. There is no default — a caller who wants only the exact baseline entries with no further
/// concurrent-mutation tracking can return a provider that always reports the same version over
/// <paramref name="baselineEntries"/> unchanged; a caller integrating with live session/message-injection
/// state supplies a provider that reflects it.
/// </summary>
/// <remarks>
/// This is the one explicit host seam allowed to augment the baseline entries with a selected
/// <see cref="HarnessArtifactRecoverableContextSegment"/> — always via
/// <see cref="HarnessContextSnapshotAugmentation.WithRecoverableSegment"/>, which validates the augmented
/// entry's id is unique and that a matching durable <see cref="HarnessContextEntryKind.ArtifactReference"/>
/// entry already exists in <paramref name="baselineEntries"/>. This runs after the per-service history
/// decorator (if configured) has already observed and persisted the reference-bearing baseline request, so
/// the transient recovered body this seam adds is never visible to anything upstream of it.
/// </remarks>
internal delegate IHarnessContextSnapshotProvider HarnessContextSnapshotIntegration(
    IReadOnlyList<HarnessContextEntry> baselineEntries);
