using AutonomousPhasePipelineApp.Core;

using Microsoft.Agents.AI.Workflows;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedEvaluationArmExecution(
    Workflow workflow,
    HostedManifestStartExecutor start,
    HostedSynthesisArm synthesis,
    ReferenceArtifactStore artifacts,
    IdempotentDeliverySink delivery) : IDisposable
{
    internal Workflow Workflow { get; } = workflow;

    internal HostedManifestStartExecutor Start { get; } = start;

    internal HostedSynthesisArm Synthesis { get; } = synthesis;

    internal ReferenceArtifactStore Artifacts { get; } = artifacts;

    internal IdempotentDeliverySink Delivery { get; } = delivery;

    public void Dispose() => Synthesis.Dispose();
}
