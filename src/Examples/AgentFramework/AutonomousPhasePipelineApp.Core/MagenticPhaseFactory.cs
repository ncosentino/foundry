using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal static class MagenticPhaseFactory
{
    internal const string ManagerName = "MagenticManager";
    internal const string ManifestAnalystName = "ManifestAnalyst";
    internal const string ContractCriticName = "ContractCritic";

    internal static MagenticPhaseRuntime CreateStallThenRecover(
        ReferenceArtifactStore artifacts,
        string runId,
        MagenticPhaseProbe probe,
        bool requirePlanSignoff)
    {
        Func<IReadOnlyList<ChatMessage>, string>[] responses =
        [
            MagenticScriptedResponses.Static("Initial facts"),
            MagenticScriptedResponses.Static("Initial synthesis plan"),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: false,
                isInLoop: true,
                isProgressBeingMade: false,
                ManifestAnalystName,
                MagenticScriptedResponses.Static("The initial plan is stalled.")),
            MagenticScriptedResponses.Static("Updated facts after stall"),
            MagenticScriptedResponses.Static("Replanned synthesis approach"),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: false,
                isInLoop: false,
                isProgressBeingMade: true,
                ManifestAnalystName,
                messages => MagenticScriptedResponses.InstructionWithManifestId(
                    messages,
                    ManifestAnalystName)),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: true,
                isInLoop: false,
                isProgressBeingMade: true,
                ManifestAnalystName,
                MagenticScriptedResponses.Static("The artifact contract is satisfied.")),
            MagenticScriptedResponses.Static(
                ReferenceSynthesisArtifacts.Valid),
        ];
        return Create(
            artifacts,
            runId,
            probe,
            responses,
            requirePlanSignoff,
            maxRounds: 8,
            maxStalls: 0,
            maxResets: 2);
    }

    internal static MagenticPhaseRuntime CreateReplanRecovery(
        ReferenceArtifactStore artifacts,
        string runId,
        MagenticPhaseProbe probe)
    {
        Func<IReadOnlyList<ChatMessage>, string>[] responses =
        [
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: false,
                isInLoop: false,
                isProgressBeingMade: true,
                ManifestAnalystName,
                messages => MagenticScriptedResponses.InstructionWithManifestId(
                    messages,
                    ManifestAnalystName)),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: true,
                isInLoop: false,
                isProgressBeingMade: true,
                ManifestAnalystName,
                MagenticScriptedResponses.Static("The artifact contract is satisfied.")),
            MagenticScriptedResponses.Static(
                ReferenceSynthesisArtifacts.Valid),
        ];
        return Create(
            artifacts,
            runId,
            probe,
            responses,
            requirePlanSignoff: true,
            maxRounds: 8,
            maxStalls: 0,
            maxResets: 2);
    }

    internal static MagenticPhaseRuntime CreateRevisionThenApprove(
        ReferenceArtifactStore artifacts,
        string runId,
        MagenticPhaseProbe probe)
    {
        Func<IReadOnlyList<ChatMessage>, string>[] responses =
        [
            MagenticScriptedResponses.Static("Initial facts"),
            MagenticScriptedResponses.Static("Initial plan needing review"),
            MagenticScriptedResponses.Static("Revised facts"),
            MagenticScriptedResponses.Static("Revised approved plan"),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: true,
                isInLoop: false,
                isProgressBeingMade: true,
                ManifestAnalystName,
                MagenticScriptedResponses.Static("The revised plan is complete.")),
            MagenticScriptedResponses.Static(
                ReferenceSynthesisArtifacts.Valid),
        ];
        return Create(
            artifacts,
            runId,
            probe,
            responses,
            requirePlanSignoff: true,
            maxRounds: 4,
            maxStalls: 1,
            maxResets: 2);
    }

    internal static MagenticPhaseRuntime CreateInvalidSpeaker(
        ReferenceArtifactStore artifacts,
        string runId,
        MagenticPhaseProbe probe)
    {
        Func<IReadOnlyList<ChatMessage>, string>[] responses =
        [
            MagenticScriptedResponses.Static("Initial facts"),
            MagenticScriptedResponses.Static("Initial plan"),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: false,
                isInLoop: false,
                isProgressBeingMade: true,
                "UnknownParticipant",
                MagenticScriptedResponses.Static("Continue.")),
            MagenticScriptedResponses.Static(
                ReferenceSynthesisArtifacts.Valid),
        ];
        return Create(
            artifacts,
            runId,
            probe,
            responses,
            requirePlanSignoff: false,
            maxRounds: 4,
            maxStalls: 1,
            maxResets: 2);
    }

    internal static MagenticPhaseRuntime CreateInvalidFinalArtifact(
        ReferenceArtifactStore artifacts,
        string runId,
        MagenticPhaseProbe probe)
    {
        Func<IReadOnlyList<ChatMessage>, string>[] responses =
        [
            MagenticScriptedResponses.Static("Initial facts"),
            MagenticScriptedResponses.Static("Initial plan"),
            MagenticScriptedResponses.Ledger(
                isRequestSatisfied: true,
                isInLoop: false,
                isProgressBeingMade: true,
                ManifestAnalystName,
                MagenticScriptedResponses.Static("Complete.")),
            MagenticScriptedResponses.Static(
                ReferenceSynthesisArtifacts.Invalid),
        ];
        return Create(
            artifacts,
            runId,
            probe,
            responses,
            requirePlanSignoff: false,
            maxRounds: 4,
            maxStalls: 1,
            maxResets: 2);
    }

    private static MagenticPhaseRuntime Create(
        ReferenceArtifactStore artifacts,
        string runId,
        MagenticPhaseProbe probe,
        IReadOnlyList<Func<IReadOnlyList<ChatMessage>, string>> managerResponses,
        bool requirePlanSignoff,
        int maxRounds,
        int maxStalls,
        int maxResets)
    {
        var managerClient = new MagenticManagerChatClient(
            managerResponses,
            probe);
        AIAgent manager = ReferencePipelineFactory.CreateHarnessAgent(
            ManagerName,
            "Plans and coordinates the phase-local Magentic team.",
            managerClient,
            tools: [],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);

        AIFunction readManifest = AIFunctionFactory.Create(
            (string manifestId) => artifacts.ReadManifestBundle(
                runId,
                manifestId),
            new AIFunctionFactoryOptions
            {
                Name = ReferencePipelineFactory.ReadManifestToolName,
                Description =
                    "Reads the accepted manifest and its authorized artifact bodies.",
            });
        var manifestAnalystClient = new MagenticParticipantChatClient(
            ManifestAnalystName,
            "Manifest analysis completed.",
            ReferencePipelineFactory.ReadManifestToolName);
        AIAgent manifestAnalyst = ReferencePipelineFactory.CreateHarnessAgent(
            ManifestAnalystName,
            "Reads and analyzes the accepted artifact manifest.",
            manifestAnalystClient,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);
        var contractCriticClient = new MagenticParticipantChatClient(
            ContractCriticName,
            "Contract critique completed.",
            readManifestToolName: null);
        AIAgent contractCritic = ReferencePipelineFactory.CreateHarnessAgent(
            ContractCriticName,
            "Checks explicit gaps and the synthesis artifact contract.",
            contractCriticClient,
            tools: [],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);

        Workflow workflow = new MagenticWorkflowBuilder(manager)
            .AddParticipants([manifestAnalyst, contractCritic])
            .WithName("phase-local-magentic-synthesis.v1")
            .WithDescription(
                "Compares Magentic planning inside one fixed macro synthesis phase.")
            .RequirePlanSignoff(requirePlanSignoff)
            .WithMaxRounds(maxRounds)
            .WithMaxStalls(maxStalls)
            .WithMaxResets(maxResets)
            .WithPromptOverrides(
                new MagenticPromptOverrides
                {
                    FinalAnswerPrompt =
                        """
                        Complete the synthesis task using only accepted evidence:
                        {task}

                        Return one JSON object containing nonempty summary,
                        evidence, and recommendation fields.
                        """,
                })
            .Build();

        return new MagenticPhaseRuntime
        {
            Workflow = workflow,
            ManagerClient = managerClient,
            ManifestAnalystClient = manifestAnalystClient,
            ContractCriticClient = contractCriticClient,
            Probe = probe,
            Agents = [manager, manifestAnalyst, contractCritic],
            MaxRounds = maxRounds,
            MaxStalls = maxStalls,
            MaxResets = maxResets,
            RequirePlanSignoff = requirePlanSignoff,
        };
    }
}
