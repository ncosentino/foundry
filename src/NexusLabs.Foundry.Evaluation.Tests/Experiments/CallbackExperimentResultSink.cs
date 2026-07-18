using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Tests.Experiments;

/// <summary>
/// Publishes experiment results through a caller-supplied test callback.
/// </summary>
internal sealed class CallbackExperimentResultSink<TCase, TOutput>(
    string name,
    bool isRequired,
    Func<
        ExperimentRunResult<TCase, TOutput>,
        CancellationToken,
        ValueTask<ExperimentSinkPublicationOperationResult>> publishAsync) :
    IExperimentResultSink<TCase, TOutput>
{
    public string Name => name;

    public bool IsRequired => isRequired;

    public ValueTask<ExperimentSinkPublicationOperationResult> PublishAsync(
        ExperimentRunResult<TCase, TOutput> result,
        CancellationToken cancellationToken) =>
        publishAsync(result, cancellationToken);
}
