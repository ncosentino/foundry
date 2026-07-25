using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

/// <summary>
/// Test double <see cref="AIFunction"/> that returns a preconfigured raw CLR object unchanged,
/// bypassing <c>AIFunctionFactory.Create</c>'s JSON round-trip (which would otherwise convert any
/// return value, including a <c>HarnessArtifactRecoverableContextSegment</c>, into a
/// <see cref="System.Text.Json.JsonElement"/> before it ever reaches a <c>FunctionInvoker</c>).
/// Mirrors how a hand-authored rehydration tool would be implemented in production specifically to
/// preserve the raw segment type end-to-end.
/// </summary>
internal sealed class HarnessRawResultFunction(
    string name,
    object? rawResult,
    Action? onInvoked = null) : AIFunction
{
    public override string Name { get; } = name;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        onInvoked?.Invoke();
        return ValueTask.FromResult(rawResult);
    }
}
