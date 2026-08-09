using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class MagenticPhaseRuntime
{
    internal required Workflow Workflow { get; init; }

    internal required MagenticManagerChatClient ManagerClient { get; init; }

    internal required MagenticParticipantChatClient ManifestAnalystClient { get; init; }

    internal required MagenticParticipantChatClient ContractCriticClient { get; init; }

    internal required MagenticPhaseProbe Probe { get; init; }

    internal required IReadOnlyList<AIAgent> Agents { get; init; }

    internal required int MaxRounds { get; init; }

    internal required int MaxStalls { get; init; }

    internal required int MaxResets { get; init; }

    internal required bool RequirePlanSignoff { get; init; }
}
