using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Tests.Harness;

internal sealed class HarnessServiceInspectingFunction(
    IServiceProvider expectedServices) : AIFunction
{
    public override string Name => "G2ServiceTool";

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(expectedServices, arguments.Services))
        {
            throw new InvalidOperationException(
                "The function invocation did not receive the composition service provider.");
        }

        return ValueTask.FromResult<object?>("service-result");
    }
}
