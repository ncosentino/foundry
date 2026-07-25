namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// One step <see cref="HarnessContextAssembler"/> performed while producing a
/// <see cref="HarnessContextAssemblyResult"/>, recorded in execution order in
/// <see cref="HarnessContextAssemblyResult.Stages"/> so the reduction path can be inspected without
/// exposing any entry content.
/// </summary>
internal enum HarnessContextAssemblyStage
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
