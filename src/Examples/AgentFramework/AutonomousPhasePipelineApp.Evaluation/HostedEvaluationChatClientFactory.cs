using AutonomousPhasePipelineApp.Core;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal delegate IChatClient HostedEvaluationChatClientFactory(
    string agentId,
    bool isChild,
    ReferenceSynthesisBudget budget,
    HostedEvaluationAgentRole role,
    HostedFaultPlan faultPlan,
    HostedFaultState faultState,
    MagenticPhaseProbe? ledgerProbe,
    ICollection<IDisposable> resources);
