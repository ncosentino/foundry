using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test-only <see cref="IHarnessContextReducer"/> whose behavior per invocation is fully controlled by
/// an injected callback, so tests can script unchanged/growing/reducing proposals, throw an exception,
/// inject a new entry mid-reduction via a captured <see cref="HarnessMutableContextSnapshotProvider"/>,
/// or observe cancellation — deterministically and without any real timing or races.
/// </summary>
internal sealed class HarnessScriptedContextReducer : IHarnessContextReducer
{
    private readonly Func<HarnessContextReductionRequest, CancellationToken, Task<IReadOnlyList<HarnessContextEntry>>> _callback;

    internal HarnessScriptedContextReducer(
        Func<HarnessContextReductionRequest, CancellationToken, Task<IReadOnlyList<HarnessContextEntry>>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _callback = callback;
    }

    /// <summary>The number of times <see cref="ReduceAsync"/> has been invoked.</summary>
    internal int InvocationCount { get; private set; }

    public Task<IReadOnlyList<HarnessContextEntry>> ReduceAsync(
        HarnessContextReductionRequest request, CancellationToken cancellationToken)
    {
        InvocationCount++;
        return _callback(request, cancellationToken);
    }
}
