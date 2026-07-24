using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Harness;

internal sealed record HarnessGeneratedToolResolution(
    HarnessGeneratedToolResolutionStatus Status,
    IReadOnlyList<AIFunction> Functions,
    IReadOnlyList<Type> MissingFunctionTypes,
    IReadOnlyList<string> DuplicateToolNames);
