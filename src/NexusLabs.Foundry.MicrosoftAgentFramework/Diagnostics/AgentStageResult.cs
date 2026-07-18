using Microsoft.Extensions.AI;

namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IAgentStageResult"/>.
/// </summary>
internal sealed record AgentStageResult(
    string AgentName,
    ChatResponse? FinalResponse,
    IAgentRunDiagnostics? Diagnostics,
    StageOutcome Outcome = StageOutcome.Succeeded,
    string? PhaseName = null,
    IStageTermination? Termination = null) : IAgentStageResult;
