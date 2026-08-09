using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework.Harness.Bundle;

namespace AutonomousPhasePipelineApp.Core;

internal static class ReferencePipelineFactory
{
    internal const string ReadArtifactToolName = "read_artifact";
    internal const string ReadManifestToolName = "read_artifact_manifest";

    private static readonly FoundryHarnessAgentFactory s_harnessFactory = new();

    internal static ReferencePipelineRuntime Create(
        ReferencePipelineRequest request,
        ReferencePipelineOptions options,
        ReferenceArtifactStore artifacts,
        IdempotentDeliverySink delivery)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(delivery);
        var backgroundGate = new ConcurrentInvocationGate(
            expectedParticipants: 2,
            holdUntilCancellation: false,
            holdUntilRelease: options.HoldBackgroundWorkersUntilRelease);
        var evidenceWorkerClient = new BackgroundWorkerChatClient(
            "evidence-complete",
            backgroundGate);
        var feasibilityWorkerClient = new BackgroundWorkerChatClient(
            "feasibility-complete",
            backgroundGate);
        AIAgent evidenceWorker = CreateHarnessAgent(
            "evidence-worker",
            "Collects bounded evidence.",
            evidenceWorkerClient,
            tools: [],
            features: DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);
        AIAgent feasibilityWorker = CreateHarnessAgent(
            "feasibility-worker",
            "Assesses bounded feasibility.",
            feasibilityWorkerClient,
            tools: [],
            features: DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);

        var researchClient = new ResearchCoordinatorChatClient();
        AIAgent researchAgent = CreateHarnessAgent(
            "research-phase",
            "Coordinates bounded delegated research.",
            researchClient,
            tools: [],
            features: DisabledFeatures() with
            {
                EnableBackgroundAgents = true,
                EnableLoopEvaluation = true,
            },
            loopEvaluators:
            [
                new BackgroundTaskCompletionLoopEvaluator(),
            ],
            loopAgentOptions: new LoopAgentOptions
            {
                MaxIterations = 3,
                FreshContextPerIteration = false,
                NonStreamingReturnsLastResponseOnly = true,
            },
            backgroundAgents:
            [
                evidenceWorker,
                feasibilityWorker,
            ],
            backgroundOptions: new BackgroundAgentsProviderOptions
            {
                Instructions =
                    "Delegate bounded research and retrieve every result.\n{background_agents}",
            },
            maximumIterationsPerRequest: 16);

        AIFunction readArtifact = AIFunctionFactory.Create(
            (string artifactId) => artifacts.Read(
                request.RunId,
                artifactId),
            new AIFunctionFactoryOptions
            {
                Name = ReadArtifactToolName,
                Description = "Reads one content-addressed pipeline artifact.",
            });
        var specialistGate = new ConcurrentInvocationGate(
            expectedParticipants: 2,
            holdUntilCancellation: options.HoldSpecialistsUntilCancellation,
            holdUntilRelease: false);
        var riskClient = new SpecialistChatClient(
            "risk",
            options.FailRequiredSpecialist,
            ReadArtifactToolName,
            specialistGate);
        var operationsClient = new SpecialistChatClient(
            "operations",
            options.FailOptionalSpecialist,
            ReadArtifactToolName,
            specialistGate);
        AIAgent riskAgent = CreateHarnessAgent(
            "risk-specialist",
            "Produces the required risk artifact.",
            riskClient,
            tools: [readArtifact],
            features: DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);
        AIAgent operationsAgent = CreateHarnessAgent(
            "operations-specialist",
            "Produces the optional operations artifact.",
            operationsClient,
            tools: [readArtifact],
            features: DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);

        var riskBranch = new ReferenceBranchDefinition(
            "risk",
            0,
            true);
        var operationsBranch = new ReferenceBranchDefinition(
            "operations",
            1,
            false);
        var intake = new IntakeExecutor();
        var research = new ResearchPhaseExecutor(researchAgent);
        var researchBoundary = new ResearchArtifactBoundaryExecutor(
            artifacts,
            request.RunId);
        var risk = new SpecialistPhaseExecutor(
            riskBranch,
            riskAgent);
        var riskBoundary = new SpecialistArtifactBoundaryExecutor(
            riskBranch,
            artifacts,
            request.RunId);
        var operations = new SpecialistPhaseExecutor(
            operationsBranch,
            operationsAgent);
        var operationsBoundary = new SpecialistArtifactBoundaryExecutor(
            operationsBranch,
            artifacts,
            request.RunId);
        var settledJoin = new AllSettledJoinExecutor(
            [riskBranch, operationsBranch]);
        var manifestBarrier = new SpecialistManifestBarrierExecutor(
            artifacts,
            request.RunId,
            [riskBranch, operationsBranch]);
        SynthesisChatClient? synthesisClient = null;
        MagenticPhaseProbe? magenticProbe = null;
        ExecutorBinding synthesis;
        var harnessAgents = new List<AIAgent>
        {
            evidenceWorker,
            feasibilityWorker,
            researchAgent,
            riskAgent,
            operationsAgent,
        };
        if (options.SynthesisExecutorKind ==
            ReferenceSynthesisExecutorKind.Harness)
        {
            AIFunction readManifest = AIFunctionFactory.Create(
                (string manifestId) => artifacts.ReadManifestBundle(
                    request.RunId,
                    manifestId),
                new AIFunctionFactoryOptions
                {
                    Name = ReadManifestToolName,
                    Description =
                        "Reads an accepted outcome manifest and its referenced artifact bodies.",
                });
            synthesisClient = new SynthesisChatClient(
                ReadManifestToolName);
            AIAgent synthesisAgent = CreateHarnessAgent(
                "synthesis-phase",
                "Synthesizes accepted artifact references and explicit gaps.",
                synthesisClient,
                tools: [readManifest],
                features: DisabledFeatures() with
                {
                    EnableLoopEvaluation = true,
                },
                loopEvaluators:
                [
                    new DelegateLoopEvaluator(
                        (context, _) =>
                        {
                            string manifestId =
                                SynthesisPhaseExecutor.GetManifestId(
                                    context.InitialMessages);
                            ReferenceArtifactManifest manifest =
                                artifacts.GetManifest(
                                    manifestId,
                                    request.RunId);
                            bool accepted =
                                ReferenceArtifactValidator.TryValidateSynthesis(
                                    context.LastResponse.Text,
                                    manifest,
                                    out string error);
                            return ValueTask.FromResult(
                                accepted
                                    ? LoopEvaluation.Stop()
                                    : LoopEvaluation.Continue(
                                        $"{ReferenceArtifactValidator.SynthesisCorrectionCode}; validation_error={error}"));
                        }),
                ],
                loopAgentOptions: new LoopAgentOptions
                {
                    MaxIterations = 2,
                    FreshContextPerIteration = false,
                    NonStreamingReturnsLastResponseOnly = true,
                },
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: 6);
            harnessAgents.Add(synthesisAgent);
            synthesis = new SynthesisPhaseExecutor(
                synthesisAgent,
                artifacts,
                request.RunId).BindExecutor();
        }
        else
        {
            magenticProbe = new MagenticPhaseProbe();
            MagenticPhaseProbe probe = magenticProbe;
            synthesis = new MagenticSynthesisPhaseExecutor(
                artifacts,
                request.RunId,
                () => MagenticPhaseFactory.CreateStallThenRecover(
                    artifacts,
                    request.RunId,
                    probe,
                    requirePlanSignoff: false)).BindExecutor();
        }

        var synthesisBoundary = new SynthesisArtifactBoundaryExecutor(
            artifacts,
            request.RunId);
        var deliveryExecutor = new DeliveryExecutor(
            artifacts,
            delivery,
            request.RunId);

        Workflow workflow = new WorkflowBuilder(intake)
            .AddEdge(intake, research)
            .AddEdge(research, researchBoundary)
            .AddFanOutEdge(
                researchBoundary,
                [risk, operations])
            .AddEdge(risk, riskBoundary)
            .AddEdge(operations, operationsBoundary)
            .AddEdge(riskBoundary, settledJoin)
            .AddEdge(operationsBoundary, settledJoin)
            .AddEdge(settledJoin, manifestBarrier)
            .AddEdge(manifestBarrier, synthesis)
            .AddEdge(synthesis, synthesisBoundary)
            .AddEdge(synthesisBoundary, deliveryExecutor)
            .WithOutputFrom(deliveryExecutor)
            .WithName("offline-fixed-macro-v1")
            .Build();

        return new ReferencePipelineRuntime
        {
            Request = request,
            Workflow = workflow,
            Artifacts = artifacts,
            Delivery = delivery,
            BackgroundGate = backgroundGate,
            SpecialistGate = specialistGate,
            EvidenceWorkerClient = evidenceWorkerClient,
            FeasibilityWorkerClient = feasibilityWorkerClient,
            ResearchClient = researchClient,
            RiskClient = riskClient,
            OperationsClient = operationsClient,
            SynthesisClient = synthesisClient,
            MagenticProbe = magenticProbe,
            HarnessAgents = harnessAgents,
        };
    }

    internal static AIAgent CreateHarnessAgent(
        string id,
        string description,
        IChatClient chatClient,
        IReadOnlyList<AITool> tools,
        FoundryHarnessFeatureSelections features,
        IReadOnlyList<LoopEvaluator> loopEvaluators,
        LoopAgentOptions? loopAgentOptions,
        IReadOnlyList<AIAgent> backgroundAgents,
        BackgroundAgentsProviderOptions? backgroundOptions,
        int? maximumIterationsPerRequest)
    {
        var configuration = new FoundryHarnessAgentConfiguration
        {
            Id = id,
            Name = id,
            Description = description,
            Instructions = description,
            HarnessInstructionsOverride = string.Empty,
            ChatClient = chatClient,
            Tools = tools,
            Features = features,
            ProgressAccessor = null,
            MaxContextWindowTokens = null,
            MaxOutputTokens = null,
            MaximumIterationsPerRequest = maximumIterationsPerRequest,
            LoopEvaluators = loopEvaluators,
            LoopAgentOptions = loopAgentOptions,
            BackgroundAgents = backgroundAgents,
            BackgroundAgentsProviderOptions = backgroundOptions,
            FileAccessStore = null,
            FileAccessProviderOptions = null,
            ChatHistoryProvider = null,
            FileMemoryStore = null,
            AgentSkillsSource = null,
            ToolApprovalAgentOptions = null,
            AgentModeProviderOptions = null,
            CompactionStrategy = null,
            HybridCompactionOptions = null,
            OpenTelemetrySourceName = null,
            AdditionalContextProviders = [],
        };
        return s_harnessFactory.Create(configuration);
    }

    internal static FoundryHarnessFeatureSelections DisabledFeatures() =>
        new()
        {
            EnableWebSearch = false,
            EnableFileMemory = false,
            EnableAgentSkills = false,
            EnableToolAutoApproval = false,
            EnableApprovalNotRequiredFunctionBypassing = false,
            EnableApprovalResponseBinding = false,
            EnableOpenTelemetry = false,
            EnableTodoProvider = false,
            EnableAgentModeProvider = false,
            EnableBackgroundAgents = false,
            EnableLoopEvaluation = false,
            EnableCompaction = false,
            EnableHybridCompaction = false,
        };
}
