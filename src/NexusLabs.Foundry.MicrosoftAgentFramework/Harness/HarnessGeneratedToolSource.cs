using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed class HarnessGeneratedToolSource
{
    private readonly IAIFunctionProvider _provider;
    private readonly IServiceProvider _serviceProvider;

    internal HarnessGeneratedToolSource(
        IAIFunctionProvider provider,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _provider = provider;
        _serviceProvider = serviceProvider;
    }

    internal HarnessGeneratedToolResolution Resolve(
        IReadOnlyList<Type> functionTypes)
    {
        ArgumentNullException.ThrowIfNull(functionTypes);

        var resolvedFunctions = new List<AIFunction>();
        var missingTypes = new List<Type>();
        foreach (var functionType in functionTypes.Distinct())
        {
            if (!GeneratedAIFunctionResolver.TryResolve(
                _provider,
                _serviceProvider,
                functionType,
                out var functions))
            {
                missingTypes.Add(functionType);
                continue;
            }

            resolvedFunctions.AddRange(functions);
        }

        if (missingTypes.Count > 0)
        {
            return new HarnessGeneratedToolResolution(
                HarnessGeneratedToolResolutionStatus.MissingGeneratedFunctionType,
                [],
                missingTypes.AsReadOnly(),
                []);
        }

        var duplicateNames = resolvedFunctions
            .GroupBy(function => function.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            return new HarnessGeneratedToolResolution(
                HarnessGeneratedToolResolutionStatus.DuplicateToolName,
                [],
                [],
                duplicateNames.AsReadOnly());
        }

        return new HarnessGeneratedToolResolution(
            HarnessGeneratedToolResolutionStatus.Success,
            resolvedFunctions.AsReadOnly(),
            [],
            []);
    }
}
