using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Specialized.Magentic;
using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedSynthesisArmFactory
{
    internal static HostedSynthesisArm Create(
        HostedEvaluationArm arm,
        string model,
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe magenticProbe,
        HostedFaultMode faultMode)
    {
        var resources = new List<IDisposable>();
        return arm switch
        {
            HostedEvaluationArm.HarnessPlain =>
                CreatePlainHarness(
                    model,
                    artifacts,
                    runId,
                    telemetry,
                    faultMode,
                    resources),
            HostedEvaluationArm.HarnessDelegated =>
                CreateDelegatedHarness(
                    model,
                    artifacts,
                    runId,
                    telemetry,
                    faultMode,
                    resources),
            HostedEvaluationArm.Magentic =>
                CreateMagentic(
                    model,
                    artifacts,
                    runId,
                    telemetry,
                    magenticProbe,
                    faultMode,
                    resources),
            _ => throw new ArgumentOutOfRangeException(
                nameof(arm),
                arm,
                null),
        };
    }

    private static HostedSynthesisArm CreatePlainHarness(
        string model,
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        HostedFaultMode faultMode,
        List<IDisposable> resources)
    {
        AIFunction readManifest = CreateReadManifestTool(
            artifacts,
            runId);
        IChatClient client = HostedProviderClientFactory.Create(
            model,
            telemetry,
            "synthesis-parent",
            isChild: false,
            faultMode,
            ledgerProbe: null,
            resources);
        AIAgent agent = ReferencePipelineFactory.CreateHarnessAgent(
            "hosted-harness-plain",
            """
            Read the accepted manifest through the tool.
            Your entire final response must be one JSON object with exactly these fields:
            {"summary":"nonempty text","evidence":["research","required-specialist"],"recommendation":"nonempty text"}
            Do not use markdown fences or commentary. Evidence values must use only IDs present in the accepted artifacts.
            """,
            client,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures() with
            {
                EnableLoopEvaluation = true,
            },
            loopEvaluators: [CreateArtifactEvaluator()],
            loopAgentOptions: new LoopAgentOptions
            {
                MaxIterations = 3,
                FreshContextPerIteration = false,
                NonStreamingReturnsLastResponseOnly = true,
            },
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 8);
        return new HostedSynthesisArm(
            new SynthesisPhaseExecutor(
                agent,
                artifacts,
                runId).BindExecutor(),
            telemetry,
            magenticProbe: null,
            resources);
    }

    private static HostedSynthesisArm CreateDelegatedHarness(
        string model,
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        HostedFaultMode faultMode,
        List<IDisposable> resources)
    {
        AIFunction readManifest = CreateReadManifestTool(
            artifacts,
            runId);
        IChatClient analystClient = HostedProviderClientFactory.Create(
            model,
            telemetry,
            "manifest-analyst",
            isChild: true,
            HostedFaultMode.None,
            ledgerProbe: null,
            resources);
        AIAgent analyst = ReferencePipelineFactory.CreateHarnessAgent(
            "ManifestAnalyst",
            """
            Read the manifest with the tool.
            Report the exact accepted evidence IDs and no unsupported claims.
            """,
            analystClient,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);
        IChatClient criticClient = HostedProviderClientFactory.Create(
            model,
            telemetry,
            "contract-critic",
            isChild: true,
            HostedFaultMode.None,
            ledgerProbe: null,
            resources);
        AIAgent critic = ReferencePipelineFactory.CreateHarnessAgent(
            "ContractCritic",
            """
            Read the manifest with the tool.
            Report explicit gaps and verify summary, evidence, and recommendation are required.
            """,
            criticClient,
            tools: [readManifest],
            features: ReferencePipelineFactory.DisabledFeatures(),
            loopEvaluators: [],
            loopAgentOptions: null,
            backgroundAgents: [],
            backgroundOptions: null,
            maximumIterationsPerRequest: 6);
        IChatClient parentClient = HostedProviderClientFactory.Create(
            model,
            telemetry,
            "synthesis-parent",
            isChild: false,
            faultMode,
            ledgerProbe: null,
            resources);
        AIAgent parent = ReferencePipelineFactory.CreateHarnessAgent(
            "hosted-harness-delegated",
            """
            You must start both ManifestAnalyst and ContractCritic, wait for both,
            retrieve both results, and then answer.
            Your entire final response must be one JSON object with exactly these fields:
            {"summary":"nonempty text","evidence":["research","required-specialist"],"recommendation":"nonempty text"}
            Do not use markdown fences or commentary. Evidence values must use only accepted IDs reported by the specialists.
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
                CreateArtifactEvaluator(),
            ],
            loopAgentOptions: new LoopAgentOptions
            {
                MaxIterations = 4,
                FreshContextPerIteration = false,
                NonStreamingReturnsLastResponseOnly = true,
            },
            backgroundAgents: [analyst, critic],
            backgroundOptions: null,
            maximumIterationsPerRequest: 16);
        return new HostedSynthesisArm(
            new SynthesisPhaseExecutor(
                parent,
                artifacts,
                runId).BindExecutor(),
            telemetry,
            magenticProbe: null,
            resources);
    }

    private static HostedSynthesisArm CreateMagentic(
        string model,
        ReferenceArtifactStore artifacts,
        string runId,
        HostedEvaluationTelemetry telemetry,
        MagenticPhaseProbe probe,
        HostedFaultMode faultMode,
        List<IDisposable> resources)
    {
        int maxStalls = faultMode == HostedFaultMode.ForceFirstMagenticStall
            ? 0
            : 2;

        MagenticPhaseRuntime CreateRuntime()
        {
            IChatClient managerClient = HostedProviderClientFactory.Create(
                model,
                telemetry,
                MagenticPhaseFactory.ManagerName,
                isChild: false,
                faultMode,
                probe,
                resources);
            AIAgent manager = ReferencePipelineFactory.CreateHarnessAgent(
                MagenticPhaseFactory.ManagerName,
                """
                Coordinate the fixed synthesis participants.
                Delegate manifest inspection before declaring the request satisfied.
                The final answer must obey the exact JSON contract.
                """,
                managerClient,
                tools: [],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: 8);

            AIFunction readManifest = CreateReadManifestTool(
                artifacts,
                runId);
            IChatClient analystClient =
                HostedProviderClientFactory.Create(
                    model,
                    telemetry,
                    MagenticPhaseFactory.ManifestAnalystName,
                    isChild: true,
                    HostedFaultMode.None,
                    ledgerProbe: null,
                    resources);
            AIAgent analyst = ReferencePipelineFactory.CreateHarnessAgent(
                MagenticPhaseFactory.ManifestAnalystName,
                """
                Read the accepted manifest with the tool.
                Return exact accepted evidence IDs and explicit gaps.
                """,
                analystClient,
                tools: [readManifest],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: 8);
            IChatClient criticClient =
                HostedProviderClientFactory.Create(
                    model,
                    telemetry,
                    MagenticPhaseFactory.ContractCriticName,
                    isChild: true,
                    HostedFaultMode.None,
                    ledgerProbe: null,
                    resources);
            AIAgent critic = ReferencePipelineFactory.CreateHarnessAgent(
                MagenticPhaseFactory.ContractCriticName,
                """
                Read the accepted manifest with the tool.
                Check gaps and require summary, evidence, and recommendation.
                """,
                criticClient,
                tools: [readManifest],
                features: ReferencePipelineFactory.DisabledFeatures(),
                loopEvaluators: [],
                loopAgentOptions: null,
                backgroundAgents: [],
                backgroundOptions: null,
                maximumIterationsPerRequest: 8);
            Workflow workflow = new MagenticWorkflowBuilder(manager)
                .AddParticipants([analyst, critic])
                .WithName("hosted-phase-local-magentic.v1")
                .RequirePlanSignoff(false)
                .WithMaxRounds(12)
                .WithMaxStalls(maxStalls)
                .WithMaxResets(3)
                .WithPromptOverrides(
                    new MagenticPromptOverrides
                    {
                        FinalAnswerPrompt =
                            """
                            Complete this synthesis task using only accepted evidence:
                            {task}

                            Return exactly one JSON object and no other text:
                            {"summary":"nonempty text","evidence":["research","required-specialist"],"recommendation":"nonempty text"}
                            Evidence values must use only IDs found in accepted artifacts.
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
                Agents = [manager, analyst, critic],
                MaxRounds = 12,
                MaxStalls = maxStalls,
                MaxResets = 3,
                RequirePlanSignoff = false,
            };
        }

        return new HostedSynthesisArm(
            new MagenticSynthesisPhaseExecutor(
                artifacts,
                runId,
                CreateRuntime).BindExecutor(),
            telemetry,
            probe,
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

    private static DelegateLoopEvaluator CreateArtifactEvaluator() =>
        new(
            (context, cancellationToken) =>
            {
                _ = cancellationToken;
                bool accepted =
                    ReferenceArtifactValidator.TryNormalizeSynthesis(
                        context.LastResponse.Text,
                        out _,
                        out string error);
                return ValueTask.FromResult(
                    accepted
                        ? LoopEvaluation.Stop()
                        : LoopEvaluation.Continue(
                            $$"""
                            {{ReferenceArtifactValidator.SynthesisCorrectionCode}}; validation_error={{error}}
                            Return exactly one JSON object and no other text:
                            {"summary":"nonempty text","evidence":["research","required-specialist"],"recommendation":"nonempty text"}
                            """));
            });
}
