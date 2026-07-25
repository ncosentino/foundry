using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only <see cref="IHarnessContextSnapshotProvider"/> holding a mutable, versioned entries list.
/// <see cref="Inject"/> appends a new entry and increments the version, deterministically simulating a
/// newly arrived message without any real timing or background thread — tests call it synchronously
/// from inside a scripted reducer callback to exercise <see cref="HarnessContextAssembler"/>'s
/// version-recheck-and-restart behavior.
/// </summary>
internal sealed class HarnessMutableContextSnapshotProvider : IHarnessContextSnapshotProvider
{
    private readonly List<HarnessContextEntry> _entries;
    private long _version;

    internal HarnessMutableContextSnapshotProvider(IReadOnlyList<HarnessContextEntry> initialEntries)
    {
        ArgumentNullException.ThrowIfNull(initialEntries);
        _entries = [.. initialEntries];
        _version = 0;
    }

    /// <summary>The number of times <see cref="CaptureSnapshot"/> has been called.</summary>
    internal int CaptureCount { get; private set; }

    /// <summary>
    /// Optional hook invoked at the start of every <see cref="CaptureSnapshot"/> call, before the
    /// counter increments or the snapshot is built — lets a test deterministically cancel a token or
    /// otherwise react exactly when <see cref="HarnessContextAssembler"/> re-queries this provider,
    /// without any real timing or races.
    /// </summary>
    internal Action? OnCapture { get; set; }

    public HarnessContextSnapshot CaptureSnapshot()
    {
        OnCapture?.Invoke();
        CaptureCount++;
        return HarnessContextSnapshot.Create(_version, [.. _entries]);
    }

    /// <summary>Appends <paramref name="entry"/> to the end of the current entries and bumps the version.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    internal void Inject(HarnessContextEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        _version++;
    }
}
