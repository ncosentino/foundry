using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Explicit, immutable result of <see cref="HarnessCompactionComposition.Compose"/>.
/// <see cref="ChatClient"/> is non-<see langword="null"/> only for
/// <see cref="HarnessCompactionCompositionStatus.Disabled"/> (the caller's original client, unchanged)
/// and <see cref="HarnessCompactionCompositionStatus.Success"/> (the wrapped client); every other status
/// is a rejected, fail-closed composition and carries no chat client at all.
/// </summary>
/// <remarks>
/// <see cref="Coordinator"/> is non-<see langword="null"/> only for
/// <see cref="HarnessCompactionCompositionStatus.Success"/> — the one
/// <see cref="HarnessCompactionRunCoordinator"/> instance shared between the returned
/// <see cref="HarnessHybridCompactionChatClient"/> and the outer
/// <see cref="Harness.HarnessGuardedAgent"/> <see cref="Harness.HarnessProviderComposition"/> builds
/// around it, so every nested provider call within one outer agent run observes and contributes to the
/// same delivered-digest set. It is always <see langword="null"/> for
/// <see cref="HarnessCompactionCompositionStatus.Disabled"/> and every rejected status: there is nothing
/// for <see cref="Harness.HarnessGuardedAgent"/> to coordinate when no compaction node was installed.
/// </remarks>
internal sealed record HarnessCompactionCompositionResult(
    HarnessCompactionCompositionStatus Status,
    IChatClient? ChatClient,
    HarnessCompactionRunCoordinator? Coordinator,
    string? Detail);
