using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;
using NexusLabs.Foundry.MicrosoftAgentFramework.Iterative;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace IterativeTripPlannerApp.Core;

/// <summary>
/// The result of a trip planner run, including the loop result, diagnostics,
/// and the final workspace state.
/// </summary>
public sealed record TripPlannerRunResult(
    IterativeLoopResult LoopResult,
    IAgentRunDiagnostics? Diagnostics,
    IWorkspace Workspace);
