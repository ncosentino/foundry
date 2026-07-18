namespace NexusLabs.Foundry.Langfuse;

/// <summary>
/// Coordinates project-resource creation across Langfuse client instances.
/// </summary>
/// <remarks>
/// Score-config and custom-model creation do not provide a provider-side idempotency key. Hosted
/// applications that initialize those resources from multiple processes should register a
/// distributed implementation before calling
/// <see cref="LangfuseServiceCollectionExtensions.AddFoundryLangfuse(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{LangfuseOptions})"/>.
/// Standalone applications can assign an implementation to
/// <see cref="LangfuseOptions.ResourceLockProvider"/>.
/// </remarks>
public interface ILangfuseResourceLockProvider
{
    /// <summary>
    /// Acquires exclusive ownership of a resource key without caller cancellation.
    /// </summary>
    /// <param name="key">The stable opaque key supplied by Foundry.</param>
    /// <returns>
    /// An asynchronous lease whose disposal releases ownership.
    /// </returns>
    ValueTask<IAsyncDisposable> AcquireAsync(LangfuseResourceLockKey key);

    /// <summary>
    /// Acquires exclusive ownership of a resource key.
    /// </summary>
    /// <param name="key">The stable opaque key supplied by Foundry.</param>
    /// <param name="cancellationToken">
    /// A token that can cancel the wait before ownership is acquired.
    /// </param>
    /// <returns>
    /// An asynchronous lease whose disposal releases ownership.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before ownership was acquired.
    /// </exception>
    ValueTask<IAsyncDisposable> AcquireAsync(
        LangfuseResourceLockKey key,
        CancellationToken cancellationToken);
}
