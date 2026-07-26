namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Public, privacy-safe mirror of <c>Harness.Context.HarnessContextAssemblyStage</c>: one step
/// performed while producing a compaction/assembly decision, recorded in execution order in
/// <see cref="HarnessContextDiagnostics.Stages"/> so the reduction path can be inspected without
/// exposing any entry content.
/// </summary>
public enum HarnessContextAssemblyStageCategory
{
    /// <summary>The initial (or a restarted) snapshot was captured from the configured provider.</summary>
    SnapshotCaptured,

    /// <summary>Recoverable rehydrated bodies were evicted ahead of every other stage.</summary>
    RecoverableBodyEviction,

    /// <summary>The configured reducer was invoked for one bounded attempt.</summary>
    ReducerAttempt,

    /// <summary>
    /// A newer snapshot version was observed after invoking the reducer; the in-flight proposal was
    /// discarded and assembly restarted deterministically from the newest snapshot.
    /// </summary>
    RestartedAfterMutation,

    /// <summary>The deterministic preservation-only fallback was evaluated.</summary>
    DeterministicFallback,
}
