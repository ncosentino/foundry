namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Deterministic source of the current <see cref="HarnessContextSnapshot"/>. There is no ambient or
/// implicit snapshot — <see cref="HarnessContextAssembler"/> only ever observes the current entries and
/// their version through this explicit accessor, which lets a caller inject a new entry between two
/// captures and have the assembler notice via <see cref="HarnessContextSnapshot.Version"/> rather than
/// by re-comparing entry content.
/// </summary>
internal interface IHarnessContextSnapshotProvider
{
    /// <summary>
    /// Captures the current snapshot. Must never return a version lower than a previously returned
    /// version, and must return the identical version whenever the entries have not changed since the
    /// previous capture.
    /// </summary>
    HarnessContextSnapshot CaptureSnapshot();
}
