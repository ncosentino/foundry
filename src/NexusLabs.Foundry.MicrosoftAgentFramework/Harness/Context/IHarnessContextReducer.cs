namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Context;

/// <summary>
/// Strategy-neutral, asynchronous, cancellable reduction abstraction <see cref="HarnessContextAssembler"/>
/// invokes over the current eligible entries. A reducer proposes one full replacement entries list; it is
/// never trusted directly by the assembler — every proposal is independently re-verified by
/// <see cref="HarnessCompactionVerifier"/>, and only a proposal that both verifies and strictly reduces
/// the estimated size is ever forwarded.
/// </summary>
internal interface IHarnessContextReducer
{
    /// <summary>
    /// Proposes a full replacement entries list for <paramref name="request"/>'s current entries. An
    /// implementation that fails should throw rather than return a sentinel/empty result, so
    /// <see cref="HarnessContextAssembler"/> never has to guess whether a returned list represents a
    /// deliberate reduction or a swallowed failure.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<HarnessContextEntry>> ReduceAsync(
        HarnessContextReductionRequest request, CancellationToken cancellationToken);
}
