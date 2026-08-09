namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed record HostedEvaluationProvenance(
    string Repository,
    string CommitSha,
    string Ref,
    string ProtocolVersion,
    string RequestedModel,
    string? ObservedModel,
    string FoundryVersion,
    string MafVersion,
    string MeaiVersion,
    string ConfigurationHash,
    string CaseId,
    string TrialId);
