using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedSynthesisArmFactory
{
    internal const int MaxArtifactAttempts = 2;
    internal const int MaxProviderCalls = 24;

    internal static HostedSynthesisArm Create(
        HostedEvaluationArm arm,
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe magenticProbe,
        HostedFaultPlan faultPlan,
        HostedEvaluationChatClientFactory clientFactory)
    {
        var resources = new List<IDisposable>();
        var budget = new ReferenceSynthesisBudget(
            MaxProviderCalls);
        var faultState = new HostedFaultState();
        return arm switch
        {
            HostedEvaluationArm.HarnessPlain =>
                CreatePlainHarness(
                    artifacts,
                    runId,
                    telemetry,
                    faultPlan,
                    faultState,
                    budget,
                    resources,
                    clientFactory),
            HostedEvaluationArm.HarnessDelegated =>
                CreateDelegatedHarness(
                    artifacts,
                    runId,
                    telemetry,
                    faultPlan,
                    faultState,
                    budget,
                    resources,
                    clientFactory),
            HostedEvaluationArm.Magentic =>
                CreateMagentic(
                    artifacts,
                    runId,
                    telemetry,
                    magenticProbe,
                    faultPlan,
                    faultState,
                    budget,
                    resources,
                    clientFactory),
            _ => throw new ArgumentOutOfRangeException(
                nameof(arm),
                arm,
                null),
        };
    }

    private static HostedSynthesisArm CreatePlainHarness(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        HostedFaultPlan faultPlan,
        HostedFaultState faultState,
        ReferenceSynthesisBudget budget,
        List<IDisposable> resources,
        HostedEvaluationChatClientFactory clientFactory)
    {
        AIFunction readManifest = CreateReadManifestTool(
            artifacts,
            runId);
        IChatClient client = clientFactory(
            "synthesis-parent",
            isChild: false,
            budget,
            HostedEvaluationAgentRole.SynthesisParent,
            faultPlan,
            faultState,
            ledgerProbe: null,
            resources);
        AIAgent agent = ReferencePipelineFactory.CreateHarnessAgent(
            "hosted-harness-plain",
            """
            Read the accepted manifest through the tool and synthesize only from
            its authorized artifacts. Return one JSON object whose evidence and
            gaps exactly match the manifest. Do not add markdown commentary.
            """,
            client,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures() with
            {
                EnableLoopEvaluation = true,
            },
            loopEvaluators:
            [
                CreateArtifactEvaluator(
                    artifacts,
                    runId),
            ],
            loopAgentOptions: CreateLoopOptions(),
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: MaxProviderCalls);
        return new HostedSynthesisArm(
            new SynthesisPhaseExecutor(
                agent,
                artifacts,
                runId,
                budget).BindExecutor(),
            telemetry,
            magenticProbe: null,
            budget,
            resources);
    }

    private static HostedSynthesisArm CreateDelegatedHarness(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        HostedFaultPlan faultPlan,
        HostedFaultState faultState,
        ReferenceSynthesisBudget budget,
        List<IDisposable> resources,
        HostedEvaluationChatClientFactory clientFactory)
    {
        AIFunction readManifest = CreateReadManifestTool(
            artifacts,
            runId);
        IChatClient analystClient = clientFactory(
            "manifest-analyst",
            isChild: true,
            budget,
            HostedEvaluationAgentRole.ManifestAnalyst,
            faultPlan,
            faultState,
            ledgerProbe: null,
            resources);
        AIAgent analyst = ReferencePipelineFactory.CreateHarnessAgent(
            "ManifestAnalyst",
            """
            Read the manifest with the tool. Report the accepted artifact
            references and the claims they support. Do not invent evidence.
            """,
            analystClient,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: MaxProviderCalls);
        IChatClient criticClient = clientFactory(
            "contract-critic",
            isChild: true,
            budget,
            HostedEvaluationAgentRole.ContractCritic,
            faultPlan,
            faultState,
            ledgerProbe: null,
            resources);
        AIAgent critic = ReferencePipelineFactory.CreateHarnessAgent(
            "ContractCritic",
            """
            Read the manifest with the tool. Identify its exact gap set and
            reject unsupported or incomplete synthesis claims.
            """,
            criticClient,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: MaxProviderCalls);
        IChatClient parentClient = clientFactory(
            "synthesis-parent",
            isChild: false,
            budget,
            HostedEvaluationAgentRole.SynthesisParent,
            faultPlan,
            faultState,
            ledgerProbe: null,
            resources);
        AIAgent parent = ReferencePipelineFactory.CreateHarnessAgent(
            "hosted-harness-delegated",
            """
            Use the available specialists when they improve the synthesis.
            Return one JSON object whose evidence and gaps exactly match the
            accepted manifest. Do not add markdown commentary.
            """,
            parentClient,
            tools: [],
            features: ReferencePipelineFactory.DisabledFeatures() with
            {
                EnableBackgroundAgents = true,
                EnableLoopEvaluation = true,
            },
            loopEvaluators:
            [
                new BackgroundTaskCompletionLoopEvaluator(),
                CreateArtifactEvaluator(
                    artifacts,
                    runId),
            ],
            loopAgentOptions: CreateLoopOptions(),
            backgroundAgents: [analyst, critic],
            backgroundOptions: null,
            maximumIterationsPerRequest: MaxProviderCalls);
        return new HostedSynthesisArm(
            new SynthesisPhaseExecutor(
                parent,
                artifacts,
                runId,
                budget).BindExecutor(),
            telemetry,
            magenticProbe: null,
            budget,
            resources);
    }

    private static HostedSynthesisArm CreateMagentic(
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultPlan faultPlan,
        HostedFaultState faultState,
        ReferenceSynthesisBudget budget,
        List<IDisposable> resources,
        HostedEvaluationChatClientFactory clientFactory)
    {
        MagenticPhaseRuntime CreateRuntime()
        {
            IChatClient managerClient =
                clientFactory(
                    MagenticPhaseFactory.ManagerName,
                    isChild: false,
                    budget,
                    HostedEvaluationAgentRole.MagenticManager,
                    faultPlan,
                    faultState,
                    probe,
                    resources);
            AIAgent manager = ReferencePipelineFactory.CreateHarnessAgent(
                MagenticPhaseFactory.ManagerName,
                """
                Coordinate the fixed synthesis participants. Require manifest
                inspection before declaring the request satisfied.
                """,
                managerClient,
                tools: [],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: MaxProviderCalls);

            AIFunction readManifest = CreateReadManifestTool(
                artifacts,
                runId);
            IChatClient analystClient =
                clientFactory(
                    MagenticPhaseFactory.ManifestAnalystName,
                    isChild: true,
                    budget,
                    HostedEvaluationAgentRole.ManifestAnalyst,
                    faultPlan,
                    faultState,
                    ledgerProbe: null,
                    resources);
            AIAgent analyst = ReferencePipelineFactory.CreateHarnessAgent(
                MagenticPhaseFactory.ManifestAnalystName,
                """
                Read the accepted manifest with the tool and report its exact
                authorized artifact references.
                """,
                analystClient,
                tools: [readManifest],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: MaxProviderCalls);
            IChatClient criticClient =
                clientFactory(
                    MagenticPhaseFactory.ContractCriticName,
                    isChild: true,
                    budget,
                    HostedEvaluationAgentRole.ContractCritic,
                    faultPlan,
                    faultState,
                    ledgerProbe: null,
                    resources);
            AIAgent critic = ReferencePipelineFactory.CreateHarnessAgent(
                MagenticPhaseFactory.ContractCriticName,
                """
                Read the accepted manifest with the tool. Verify its exact gaps
                and reject unsupported synthesis claims.
                """,
                criticClient,
                tools: [readManifest],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: MaxProviderCalls);
            Workflow workflow = new MagenticWorkflowBuilder(manager)
                .AddParticipants([analyst, critic])
                .WithName("hosted-phase-local-magentic.v2")
                .RequirePlanSignoff(false)
                .WithMaxRounds(8)
                .WithMaxStalls(0)
                .WithMaxResets(2)
                .WithPromptOverrides(
                    new MagenticPromptOverrides
                    {
                        FinalAnswerPrompt =
                            """
                            Complete this synthesis task using only the accepted
                            manifest and participant findings:
                            {task}

                            Return one JSON object whose evidence and gaps
                            exactly match the accepted manifest.
                            """,
                    })
                .Build();
            return new MagenticPhaseRuntime
            {
                Workflow = workflow,
                ManagerClient = managerClient,
                ManifestAnalystClient = analystClient,
                ContractCriticClient = criticClient,
                Probe = probe,
                Budget = budget,
                Agents = [manager, analyst, critic],
                MaxRounds = 8,
                MaxStalls = 0,
                MaxResets = 2,
                RequirePlanSignoff = false,
            };
        }

        return new HostedSynthesisArm(
            new MagenticSynthesisPhaseExecutor(
                artifacts,
                runId,
                CreateRuntime,
                budget,
                MaxArtifactAttempts).BindExecutor(),
            telemetry,
            probe,
            budget,
            resources);
    }

    private static AIFunction CreateReadManifestTool(
        ReferenceArtifactStore artifacts,
        string runId) =>
        AIFunctionFactory.Create(
            (string manifestId) =>
                artifacts.ReadManifestBundle(
                    runId,
                    manifestId),
            new AIFunctionFactoryOptions
            {
                Name = ReferencePipelineFactory.ReadManifestToolName,
                Description =
                    "Reads the accepted manifest and authorized artifact bodies.",
            });

    private static DelegateLoopEvaluator CreateArtifactEvaluator(
        ReferenceArtifactStore artifacts,
        string runId) =>
        new(
            (context, cancellationToken) =>
            {
                _ = cancellationToken;
                string manifestId =
                    SynthesisPhaseExecutor.GetManifestId(
                        context.InitialMessages);
                ReferenceArtifactManifest manifest =
                    artifacts.GetManifest(
                        manifestId,
                        runId);
                bool accepted =
                    ReferenceArtifactValidator.TryNormalizeSynthesis(
                        context.LastResponse.Text,
                        manifest,
                        out _,
                        out string error);
                return ValueTask.FromResult(
                    accepted
                        ? LoopEvaluation.Stop()
                        : LoopEvaluation.Continue(
                            ReferenceArtifactValidator
                                .CreateSynthesisCorrection(
                                    error)));
            });

    private static LoopAgentOptions CreateLoopOptions() =>
        new()
        {
            MaxIterations = MaxArtifactAttempts,
            FreshContextPerIteration = false,
            NonStreamingReturnsLastResponseOnly = true,
        };
}
