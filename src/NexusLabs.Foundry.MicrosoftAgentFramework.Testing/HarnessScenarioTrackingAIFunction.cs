using System.Collections.Concurrent;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

internal sealed class HarnessScenarioTrackingAIFunction : DelegatingAIFunction
{
    private readonly ConcurrentQueue<string> _executedToolNames;

    internal HarnessScenarioTrackingAIFunction(
        AIFunction innerFunction,
        ConcurrentQueue<string> executedToolNames)
        : base(innerFunction)
    {
        _executedToolNames = executedToolNames;
    }

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        _executedToolNames.Enqueue(InnerFunction.Name);
        return base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
