namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>Explicit, categorical outcome of one <see cref="HarnessCompactionComposition.Compose"/> call.</summary>
internal enum HarnessCompactionCompositionStatus
{
    /// <summary>
    /// The Compaction capability is not enabled and no <see cref="HarnessHybridProfile"/> was supplied:
    /// the caller's chat client is returned completely unchanged, and no compaction node is installed —
    /// the existing baseline pipeline is preserved exactly.
    /// </summary>
    Disabled,

    /// <summary>Exactly one <see cref="HarnessHybridCompactionChatClient"/> was installed.</summary>
    Success,

    /// <summary>
    /// The Compaction capability is enabled but no <see cref="HarnessHybridProfile"/> was supplied.
    /// Fails closed before any agent construction proceeds.
    /// </summary>
    CapabilityEnabledWithoutProfile,

    /// <summary>
    /// A <see cref="HarnessHybridProfile"/> was supplied but the Compaction capability is not enabled on
    /// the supplied capability profile. Fails closed before any agent construction proceeds.
    /// </summary>
    ProfileSuppliedWithoutCapabilityEnabled,

    /// <summary>
    /// The supplied capability profile carries no Compaction capability evidence at all, or is not
    /// itself executable.
    /// </summary>
    ProfileNotExecutable,

    /// <summary>
    /// The supplied chat client already contains a <see cref="HarnessHybridCompactionChatClient"/>.
    /// Exactly one hybrid compaction component may ever be installed.
    /// </summary>
    ExistingCompactionComponent,
}
