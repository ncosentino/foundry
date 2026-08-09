namespace AutonomousPhasePipelineApp.Evaluation;

internal static class HostedEvaluationRecommendationGate
{
    internal static HostedEvaluationRecommendationDecision Evaluate(
        HostedEvaluationProtocol protocol,
        IReadOnlyList<HostedEvaluationBlockResult> blocks,
        int itemFailureCount)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(blocks);
        HostedEvaluationArmResult[] applicable = blocks
            .SelectMany(block => block.Arms)
            .Where(result => result.Applicable)
            .ToArray();
        string[] observedModels = applicable
            .Select(result => result.Provenance.ObservedModel)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        bool missingObservedModel = applicable.Any(result =>
            string.IsNullOrWhiteSpace(
                result.Provenance.ObservedModel));
        bool systemicProtocolInvalid = applicable.Any(result =>
            result.Resources.ProviderCallLimit !=
                HostedSynthesisArmFactory.MaxProviderCalls ||
            result.Infrastructure.SemanticQualityStatus !=
                HostedSemanticQualityStatus
                    .NotScoredCalibrationRequired) ||
            (applicable.Length > 0 &&
             (missingObservedModel ||
              observedModels.Length != 1));
        if (systemicProtocolInvalid)
        {
            return new(
                HostedEvaluationRecommendationStatus.ProtocolInvalid,
                "The run violated the common budget, observed-model, or semantic-calibration contract.",
                AdmissibleResults: 0,
                ExcludedResults: applicable.Length);
        }

        int infrastructureExcluded = applicable.Count(result =>
            result.Infrastructure.ProviderFailureCount > 0 ||
            result.Infrastructure.PhaseFailureCount > 0);
        int failedArmSlots =
            itemFailureCount *
            Enum.GetValues<HostedEvaluationArm>().Length;
        int infrastructureDenominator =
            applicable.Length + failedArmSlots;
        double infrastructureFailureRate =
            infrastructureDenominator == 0
                ? 0
                : (double)(
                    infrastructureExcluded +
                    failedArmSlots) /
                    infrastructureDenominator;
        if (infrastructureFailureRate > 0.10)
        {
            return new(
                HostedEvaluationRecommendationStatus
                    .InfrastructureUnreliable,
                $"Infrastructure exclusion rate {infrastructureFailureRate:P1} exceeded the predeclared 10% limit.",
                AdmissibleResults:
                    applicable.Length -
                    infrastructureExcluded,
                ExcludedResults:
                    infrastructureExcluded +
                    failedArmSlots);
        }

        HostedEvaluationArmResult[] admissible = applicable
            .Where(IsAdmissible)
            .ToArray();
        HostedEvaluationArmResult[] pooled = admissible
            .Where(result =>
                HostedEvaluationDatasetCatalog.IsPoolable(
                    result.Scenario))
            .ToArray();
        bool eachArmHasThree = Enum
            .GetValues<HostedEvaluationArm>()
            .All(arm => pooled.Count(result =>
                result.Arm == arm) >= 3);
        int completePairedBlocks = blocks.Count(block =>
            HostedEvaluationDatasetCatalog.IsPoolable(
                block.Scenario) &&
            Enum.GetValues<HostedEvaluationArm>().All(arm =>
                block.Arms.Any(result =>
                    result.Arm == arm &&
                    result.Applicable &&
                    IsAdmissible(result))));
        int excluded =
            applicable.Length - admissible.Length +
            failedArmSlots;
        if (protocol.TrialCount < 3 ||
            !eachArmHasThree ||
            completePairedBlocks < 3)
        {
            return new(
                HostedEvaluationRecommendationStatus
                    .InsufficientlyPowered,
                "The protocol requires at least three trials, three pooled admissible results per arm, and three complete paired blocks.",
                admissible.Length,
                excluded);
        }

        return new(
            HostedEvaluationRecommendationStatus.Pilot,
            "The protocol is admissible, but no preregistered effect-size and paired-inference gate has admitted an architecture recommendation.",
            admissible.Length,
            excluded);
    }

    internal static bool IsAdmissible(
        HostedEvaluationArmResult result) =>
        result.Infrastructure.ProviderFailureCount == 0 &&
        result.Infrastructure.PhaseFailureCount == 0 &&
        (!result.Resilience.FaultRequired ||
         (result.Resilience.FaultActivated &&
          result.Resilience.FaultActivatedAfterProviderWork &&
          result.Resilience.FaultActivatedAfterCollaboratorWork &&
          (result.OutputText is null ||
           result.Infrastructure.PostFaultOutputCaptured)));
}
