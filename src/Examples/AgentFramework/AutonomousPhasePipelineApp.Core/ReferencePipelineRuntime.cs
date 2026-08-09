using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Core;

internal sealed class ReferencePipelineRuntime
{
    internal required ReferencePipelineRequest Request { get; init; }

    internal required Workflow Workflow { get; init; }

    internal required ReferenceArtifactStore Artifacts { get; init; }

    internal required IdempotentDeliverySink Delivery { get; init; }

    internal required ConcurrentInvocationGate BackgroundGate { get; init; }

    internal required ConcurrentInvocationGate SpecialistGate { get; init; }

    internal required BackgroundWorkerChatClient EvidenceWorkerClient { get; init; }

    internal required BackgroundWorkerChatClient FeasibilityWorkerClient { get; init; }

    internal required ResearchCoordinatorChatClient ResearchClient { get; init; }

    internal required SpecialistChatClient RiskClient { get; init; }

    internal required SpecialistChatClient OperationsClient { get; init; }

    internal required SynthesisChatClient? SynthesisClient { get; init; }

    internal required MagenticPhaseProbe? MagenticProbe { get; init; }

    internal required IReadOnlyList<AIAgent> HarnessAgents { get; init; }
}
