using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework;

internal static class GeneratedAIFunctionResolver
{
    internal static bool TryResolve(
        IAIFunctionProvider? provider,
        IServiceProvider serviceProvider,
        Type functionType,
        [NotNullWhen(true)] out IReadOnlyList<AIFunction>? functions)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(functionType);

        if (provider?.TryGetFunctions(
            functionType,
            serviceProvider,
            out var generated) == true)
        {
            functions = generated;
            return true;
        }

        functions = null;
        return false;
    }
}
