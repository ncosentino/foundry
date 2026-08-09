using AutonomousPhasePipelineApp.Core;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationExpectedOutput(
    ReferencePipelineOutcome? PipelineOutcome,
    ReferencePipelineOutcome? SynthesisOutcome,
    string[] EvidenceIds,
    string[] Gaps,
    string[] PreservedSiblingPhases,
    int AuthoritativeDeliveries);
