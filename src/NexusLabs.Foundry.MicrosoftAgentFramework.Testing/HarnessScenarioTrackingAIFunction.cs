using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Testing;

internal sealed class HarnessScenarioTrackingAIFunction(
    AIFunction innerFunction,
    ConcurrentQueue<string> executedToolNames) : AIFunction
{
    public override string Name => innerFunction.Name;

    public override string Description => innerFunction.Description;

    public override JsonElement JsonSchema => innerFunction.JsonSchema;

    public override JsonElement? ReturnJsonSchema => innerFunction.ReturnJsonSchema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        executedToolNames.Enqueue(innerFunction.Name);
        return innerFunction.InvokeAsync(arguments, cancellationToken);
    }
}
