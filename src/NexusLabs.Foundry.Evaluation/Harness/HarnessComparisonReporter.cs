using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using NexusLabs.Foundry.Evaluation.Experiments;
using NexusLabs.Foundry.Evaluation.Harness.Judging;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Builds protocol-bound case-level paired evidence from the complete normalized three-arm scheduling
/// ledger for one <c>harness-001</c> hosted comparison.
/// </summary>
public sealed class HarnessComparisonReporter
{
    /// <summary>The schema version of the outer Harness comparison artifact.</summary>
    public const string ArtifactSchemaVersion = "1.0";

    private static readonly (HarnessComparisonArm X, HarnessComparisonArm Y)[] Contrasts =
    [
        (HarnessComparisonArm.PlainHarness, HarnessComparisonArm.Iterative),
        (HarnessComparisonArm.Hybrid, HarnessComparisonArm.Iterative),
        (HarnessComparisonArm.Hybrid, HarnessComparisonArm.PlainHarness),
    ];

    /// <summary>
    /// Builds a deterministic comparison report.
    /// </summary>
    /// <param name="request">The frozen metadata and normalized scheduling ledger.</param>
    /// <returns>The case-level paired comparison report.</returns>
    /// <exception cref="ArgumentException">
    /// The ledger is incomplete, duplicated, asymmetrically scheduled, inconsistent with the manifest,
    /// or incompatible with the reporter-level run state.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public HarnessComparisonReport Build(HarnessComparisonReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RunState == HarnessHostedRunState.InvalidInput)
        {
            return CreateReport(
                request,
                fullyScheduledCaseCount: 0,
                retentionEligible: false,
                contrasts: []);
        }

        var hostedCases = request.Manifest.Cases
            .Where(@case => !@case.Development)
            .ToArray();
        ValidateJudgeObservations(request.JudgeObservations, hostedCases);
        var ledger = ValidateLedger(request, hostedCases);
        var fullyScheduledCaseCount = hostedCases.Count(@case =>
            IsCaseFullyScheduled(ledger, @case.Id));
        var reports = Contrasts
            .Select(contrast => BuildContrast(
                request,
                hostedCases,
                ledger,
                contrast.X,
                contrast.Y))
            .ToArray();
        var retentionEligible =
            request.RunState != HarnessHostedRunState.CanceledByCaller &&
            fullyScheduledCaseCount >= 6;
        return CreateReport(
            request,
            fullyScheduledCaseCount,
            retentionEligible,
            reports);
    }

    /// <summary>
    /// Writes the comparison report together with all three canonical experiment outcome envelopes.
    /// </summary>
    /// <typeparam name="TOutput">The caller-owned arm output type.</typeparam>
    /// <param name="destination">The destination stream, which remains open.</param>
    /// <param name="report">The deterministic comparison report.</param>
    /// <param name="outcomes">The three canonical arm outcomes.</param>
    /// <param name="caseTypeInfo">Serialization metadata for <see cref="HarnessManifestCase"/>.</param>
    /// <param name="outputTypeInfo">Serialization metadata for <typeparamref name="TOutput"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes after the artifact is flushed.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public async Task WriteAsync<TOutput>(
        Stream destination,
        HarnessComparisonReport report,
        HarnessComparisonArmOutcomes<TOutput> outcomes,
        JsonTypeInfo<HarnessManifestCase> caseTypeInfo,
        JsonTypeInfo<TOutput> outputTypeInfo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(caseTypeInfo);
        ArgumentNullException.ThrowIfNull(outputTypeInfo);
        cancellationToken.ThrowIfCancellationRequested();

        var artifactWriter = new ExperimentJsonArtifactWriter();
        using var writer = new Utf8JsonWriter(
            destination,
            new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", ArtifactSchemaVersion);
        writer.WritePropertyName("report");
        JsonSerializer.Serialize(
            writer,
            report,
            HarnessComparisonReportJsonContext.Default.HarnessComparisonReport);
        writer.WritePropertyName("canonicalOutcomes");
        writer.WriteStartObject();
        WriteOutcome(
            writer,
            "iterative",
            artifactWriter.Serialize(outcomes.Iterative, caseTypeInfo, outputTypeInfo));
        WriteOutcome(
            writer,
            "plainHarness",
            artifactWriter.Serialize(outcomes.PlainHarness, caseTypeInfo, outputTypeInfo));
        WriteOutcome(
            writer,
            "hybrid",
            artifactWriter.Serialize(outcomes.Hybrid, caseTypeInfo, outputTypeInfo));
        writer.WriteEndObject();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HarnessComparisonReport CreateReport(
        HarnessComparisonReportRequest request,
        int fullyScheduledCaseCount,
        bool retentionEligible,
        IReadOnlyList<HarnessPairwiseContrastReport> contrasts) =>
        new(
            request.ReportId,
            request.RunState,
            request.Manifest.CaseSetId,
            request.Manifest.Version,
            request.ManifestSha256,
            request.AnalysisPlanSha256,
            fullyScheduledCaseCount,
            retentionEligible,
            contrasts,
            HarnessJudgeDisagreementReport.Create(request.JudgeObservations));

    private static void WriteOutcome(
        Utf8JsonWriter writer,
        string propertyName,
        string canonicalOutcomeJson)
    {
        writer.WritePropertyName(propertyName);
        using var document = JsonDocument.Parse(canonicalOutcomeJson);
        document.RootElement.WriteTo(writer);
    }

    private static Dictionary<(HarnessComparisonArm Arm, string CaseId, int TrialIndex), HarnessComparisonTrialRecord>
        ValidateLedger(
            HarnessComparisonReportRequest request,
            IReadOnlyList<HarnessManifestCase> hostedCases)
    {
        var expectedCount =
            hostedCases.Count *
            HarnessManifestCaseSource.RequiredHostedTrialCount *
            Enum.GetValues<HarnessComparisonArm>().Length;
        if (request.Trials.Count != expectedCount)
        {
            throw new ArgumentException(
                $"The normalized ledger must contain exactly {expectedCount} arm/case/trial rows.",
                nameof(request));
        }

        var hostedById = hostedCases.ToDictionary(@case => @case.Id, StringComparer.Ordinal);
        var ledger = new Dictionary<
            (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
            HarnessComparisonTrialRecord>();
        foreach (var trial in request.Trials)
        {
            if (!hostedById.TryGetValue(trial.CaseId, out var manifestCase))
            {
                throw new ArgumentException(
                    $"Ledger row references unknown hosted case '{trial.CaseId}'.",
                    nameof(request));
            }

            if (!ledger.TryAdd((trial.Arm, trial.CaseId, trial.TrialIndex), trial))
            {
                throw new ArgumentException(
                    $"Ledger row '{trial.Arm}/{trial.CaseId}/{trial.TrialIndex}' appears more than once.",
                    nameof(request));
            }

            if (trial.Scheduled)
            {
                ValidateScheduledDimensions(trial, manifestCase);
            }
        }

        foreach (var manifestCase in hostedCases)
        {
            for (var trialIndex = 1;
                 trialIndex <= HarnessManifestCaseSource.RequiredHostedTrialCount;
                 trialIndex++)
            {
                var scheduled = new bool[Enum.GetValues<HarnessComparisonArm>().Length];
                var armIndex = 0;
                foreach (var arm in Enum.GetValues<HarnessComparisonArm>())
                {
                    if (!ledger.TryGetValue((arm, manifestCase.Id, trialIndex), out var trial))
                    {
                        throw new ArgumentException(
                            $"Ledger row '{arm}/{manifestCase.Id}/{trialIndex}' is missing.",
                            nameof(request));
                    }

                    scheduled[armIndex++] = trial.Scheduled;
                }

                if (scheduled.Any(value => value != scheduled[0]))
                {
                    throw new ArgumentException(
                        $"Paired batch '{manifestCase.Id}/{trialIndex}' was scheduled asymmetrically.",
                        nameof(request));
                }

                if (request.RunState == HarnessHostedRunState.Completed && !scheduled[0])
                {
                    throw new ArgumentException(
                        "A completed hosted run cannot contain unscheduled paired batches.",
                        nameof(request));
                }
            }
        }

        return ledger;
    }

    private static void ValidateScheduledDimensions(
        HarnessComparisonTrialRecord trial,
        HarnessManifestCase manifestCase)
    {
        var expectedBinary = manifestCase.DeterministicReferences
            .Select(reference => reference.Dimension)
            .Where(HarnessComparisonDimensionClassification.IsBinary)
            .ToHashSet();
        var actualBinary = trial.BinaryValues
            .Select(value => value.Dimension)
            .ToHashSet();
        if (!actualBinary.SetEquals(expectedBinary))
        {
            throw new ArgumentException(
                $"Scheduled row '{trial.Arm}/{trial.CaseId}/{trial.TrialIndex}' does not match its binary reference dimensions.",
                nameof(trial));
        }

        var expectedContinuous = manifestCase.DeterministicReferences
            .Select(reference => reference.Dimension)
            .Where(HarnessComparisonDimensionClassification.IsContinuous)
            .ToHashSet();
        var actualContinuous = trial.ContinuousValues
            .Select(value => value.Dimension)
            .ToHashSet();
        if (!actualContinuous.SetEquals(expectedContinuous))
        {
            throw new ArgumentException(
                $"Scheduled row '{trial.Arm}/{trial.CaseId}/{trial.TrialIndex}' does not match its continuous reference dimensions.",
                nameof(trial));
        }
    }

    private static bool IsCaseFullyScheduled(
        IReadOnlyDictionary<
            (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
            HarnessComparisonTrialRecord> ledger,
        string caseId)
    {
        for (var trialIndex = 1;
             trialIndex <= HarnessManifestCaseSource.RequiredHostedTrialCount;
             trialIndex++)
        {
            if (!ledger[(HarnessComparisonArm.Iterative, caseId, trialIndex)].Scheduled)
            {
                return false;
            }
        }

        return true;
    }

    private static HarnessPairwiseContrastReport BuildContrast(
        HarnessComparisonReportRequest request,
        IReadOnlyList<HarnessManifestCase> hostedCases,
        IReadOnlyDictionary<
            (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
            HarnessComparisonTrialRecord> ledger,
        HarnessComparisonArm xArm,
        HarnessComparisonArm yArm)
    {
        var dimensions = hostedCases
            .SelectMany(@case => @case.DeterministicReferences)
            .Select(reference => reference.Dimension)
            .Distinct()
            .Order()
            .ToArray();
        var binary = dimensions
            .Where(HarnessComparisonDimensionClassification.IsBinary)
            .Select(dimension => BuildBinaryComparison(
                request,
                hostedCases,
                ledger,
                xArm,
                yArm,
                dimension))
            .ToArray();
        var continuous = dimensions
            .Where(HarnessComparisonDimensionClassification.IsContinuous)
            .Select(dimension => BuildContinuousComparison(
                request,
                hostedCases,
                ledger,
                xArm,
                yArm,
                dimension))
            .ToArray();
        return new HarnessPairwiseContrastReport(xArm, yArm, binary, continuous);
    }

    private static HarnessBinaryDimensionComparison BuildBinaryComparison(
        HarnessComparisonReportRequest request,
        IReadOnlyList<HarnessManifestCase> hostedCases,
        IReadOnlyDictionary<
            (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
            HarnessComparisonTrialRecord> ledger,
        HarnessComparisonArm xArm,
        HarnessComparisonArm yArm,
        HarnessEvaluationDimension dimension)
    {
        var primaryCases = new List<ExperimentPairedBinaryCaseOutcome>();
        var inconclusiveCases = new List<ExperimentPairedBinaryCaseOutcome>();
        var incompleteDueToCapCaseCount = 0;
        foreach (var manifestCase in CasesForDimension(hostedCases, dimension))
        {
            var xRows = GetCaseRows(ledger, xArm, manifestCase.Id);
            var yRows = GetCaseRows(ledger, yArm, manifestCase.Id);
            var fullyScheduled = xRows.All(row => row.Scheduled) && yRows.All(row => row.Scheduled);
            if (!fullyScheduled)
            {
                incompleteDueToCapCaseCount++;
            }

            var isComparable =
                fullyScheduled &&
                xRows.All(row => BinaryValue(row, dimension).IsComparable) &&
                yRows.All(row => BinaryValue(row, dimension).IsComparable);
            var xPrimary = fullyScheduled
                ? xRows.Count(row =>
                    row.Status == ExperimentItemStatus.Succeeded &&
                    BinaryValue(row, dimension).Value == true) >= 2
                : (bool?)null;
            var yPrimary = fullyScheduled
                ? yRows.Count(row =>
                    row.Status == ExperimentItemStatus.Succeeded &&
                    BinaryValue(row, dimension).Value == true) >= 2
                : (bool?)null;
            primaryCases.Add(new ExperimentPairedBinaryCaseOutcome(
                manifestCase.Id,
                xPrimary,
                fullyScheduled ? ExperimentItemStatus.Succeeded : ExperimentItemStatus.EvaluationFailed,
                yPrimary,
                fullyScheduled ? ExperimentItemStatus.Succeeded : ExperimentItemStatus.EvaluationFailed,
                isComparable));

            var xInconclusive = TryAggregateScorableBinary(xRows, dimension, out var xValue);
            var yInconclusive = TryAggregateScorableBinary(yRows, dimension, out var yValue);
            inconclusiveCases.Add(new ExperimentPairedBinaryCaseOutcome(
                manifestCase.Id,
                xInconclusive ? xValue : null,
                xInconclusive ? ExperimentItemStatus.Succeeded : ExperimentItemStatus.EvaluationFailed,
                yInconclusive ? yValue : null,
                yInconclusive ? ExperimentItemStatus.Succeeded : ExperimentItemStatus.EvaluationFailed,
                isComparable));
        }

        return new HarnessBinaryDimensionComparison(
            dimension,
            incompleteDueToCapCaseCount,
            ExperimentPairedComparisonEvidence.CreateBinary(
                HarnessComparisonExperiment.GetArmId(xArm),
                HarnessComparisonExperiment.GetArmId(yArm),
                primaryCases,
                ExperimentUnknownSampleTreatment.CountAsFailure,
                request.ConfidenceLevel),
            ExperimentPairedComparisonEvidence.CreateBinary(
                HarnessComparisonExperiment.GetArmId(xArm),
                HarnessComparisonExperiment.GetArmId(yArm),
                inconclusiveCases,
                ExperimentUnknownSampleTreatment.Inconclusive,
                request.ConfidenceLevel));
    }

    private static HarnessContinuousDimensionComparison BuildContinuousComparison(
        HarnessComparisonReportRequest request,
        IReadOnlyList<HarnessManifestCase> hostedCases,
        IReadOnlyDictionary<
            (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
            HarnessComparisonTrialRecord> ledger,
        HarnessComparisonArm xArm,
        HarnessComparisonArm yArm,
        HarnessEvaluationDimension dimension)
    {
        var conditionalCases = new List<ExperimentPairedContinuousCaseMeasurement>();
        var sensitivityCases = new List<ExperimentPairedContinuousPessimisticCaseMeasurement>();
        var incompleteDueToCapCaseCount = 0;
        foreach (var manifestCase in CasesForDimension(hostedCases, dimension))
        {
            var xRows = GetCaseRows(ledger, xArm, manifestCase.Id);
            var yRows = GetCaseRows(ledger, yArm, manifestCase.Id);
            var fullyScheduled = xRows.All(row => row.Scheduled) && yRows.All(row => row.Scheduled);
            if (!fullyScheduled)
            {
                incompleteDueToCapCaseCount++;
            }

            var isComparable =
                fullyScheduled &&
                xRows.All(row => ContinuousValue(row, dimension).IsComparable) &&
                yRows.All(row => ContinuousValue(row, dimension).IsComparable);
            var xConditional = TryAggregateConditionalContinuous(
                xRows,
                dimension,
                out var xConditionalValue);
            var yConditional = TryAggregateConditionalContinuous(
                yRows,
                dimension,
                out var yConditionalValue);
            conditionalCases.Add(new ExperimentPairedContinuousCaseMeasurement(
                manifestCase.Id,
                xConditional ? xConditionalValue : null,
                xConditional ? ExperimentItemStatus.Succeeded : ExperimentItemStatus.EvaluationFailed,
                yConditional ? yConditionalValue : null,
                yConditional ? ExperimentItemStatus.Succeeded : ExperimentItemStatus.EvaluationFailed,
                isComparable));

            var xSensitivity = TryAggregatePessimisticContinuous(
                xRows,
                dimension,
                fullyScheduled,
                isComparable,
                out var xSensitivityValue,
                out var xUsedSubstitution);
            var ySensitivity = TryAggregatePessimisticContinuous(
                yRows,
                dimension,
                fullyScheduled,
                isComparable,
                out var ySensitivityValue,
                out var yUsedSubstitution);
            sensitivityCases.Add(new ExperimentPairedContinuousPessimisticCaseMeasurement(
                manifestCase.Id,
                xSensitivity ? xSensitivityValue : null,
                xUsedSubstitution,
                ySensitivity ? ySensitivityValue : null,
                yUsedSubstitution,
                fullyScheduled,
                isComparable));
        }

        return new HarnessContinuousDimensionComparison(
            dimension,
            incompleteDueToCapCaseCount,
            ExperimentPairedComparisonEvidence.CreateContinuous(
                HarnessComparisonExperiment.GetArmId(xArm),
                HarnessComparisonExperiment.GetArmId(yArm),
                conditionalCases,
                request.BootstrapSeed,
                request.ConfidenceLevel),
            ExperimentPairedComparisonEvidence.CreateContinuousPessimisticSensitivity(
                HarnessComparisonExperiment.GetArmId(xArm),
                HarnessComparisonExperiment.GetArmId(yArm),
                sensitivityCases,
                request.BootstrapSeed,
                request.ConfidenceLevel));
    }

    private static IEnumerable<HarnessManifestCase> CasesForDimension(
        IEnumerable<HarnessManifestCase> hostedCases,
        HarnessEvaluationDimension dimension) =>
        hostedCases.Where(@case =>
            @case.DeterministicReferences.Any(reference => reference.Dimension == dimension));

    private static void ValidateJudgeObservations(
        IReadOnlyList<HarnessJudgeComparisonObservation> observations,
        IReadOnlyList<HarnessManifestCase> hostedCases)
    {
        var hostedById = hostedCases.ToDictionary(@case => @case.Id, StringComparer.Ordinal);
        foreach (var observation in observations)
        {
            if (!hostedById.TryGetValue(observation.CaseId, out var manifestCase))
            {
                throw new ArgumentException(
                    $"Judge observation references unknown hosted case '{observation.CaseId}'.",
                    nameof(observations));
            }

            if (!manifestCase.DeterministicReferences.Any(reference =>
                    reference.Dimension == observation.Dimension))
            {
                throw new ArgumentException(
                    $"Judge observation dimension '{observation.Dimension}' is not declared by case '{observation.CaseId}'.",
                    nameof(observations));
            }

            if (!Contrasts.Contains((observation.XArm, observation.YArm)))
            {
                throw new ArgumentException(
                    $"Judge observation contrast '{observation.XArm}-{observation.YArm}' is not registered.",
                    nameof(observations));
            }
        }
    }

    private static HarnessComparisonTrialRecord[] GetCaseRows(
        IReadOnlyDictionary<
            (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
            HarnessComparisonTrialRecord> ledger,
        HarnessComparisonArm arm,
        string caseId) =>
        Enumerable.Range(1, HarnessManifestCaseSource.RequiredHostedTrialCount)
            .Select(trialIndex => ledger[(arm, caseId, trialIndex)])
            .ToArray();

    private static HarnessComparisonBinaryTrialValue BinaryValue(
        HarnessComparisonTrialRecord row,
        HarnessEvaluationDimension dimension) =>
        row.BinaryValues.Single(value => value.Dimension == dimension);

    private static HarnessComparisonContinuousTrialValue ContinuousValue(
        HarnessComparisonTrialRecord row,
        HarnessEvaluationDimension dimension) =>
        row.ContinuousValues.Single(value => value.Dimension == dimension);

    private static bool TryAggregateScorableBinary(
        IReadOnlyList<HarnessComparisonTrialRecord> rows,
        HarnessEvaluationDimension dimension,
        out bool value)
    {
        value = false;
        if (rows.Any(row =>
                !row.Scheduled ||
                row.Status != ExperimentItemStatus.Succeeded ||
                !BinaryValue(row, dimension).Value.HasValue))
        {
            return false;
        }

        value = rows.Count(row => BinaryValue(row, dimension).Value == true) >= 2;
        return true;
    }

    private static bool TryAggregateConditionalContinuous(
        IReadOnlyList<HarnessComparisonTrialRecord> rows,
        HarnessEvaluationDimension dimension,
        out double value)
    {
        value = default;
        if (rows.Any(row =>
                !row.Scheduled ||
                row.Status != ExperimentItemStatus.Succeeded ||
                BinaryValue(row, HarnessEvaluationDimension.Completion).Value != true ||
                !ContinuousValue(row, dimension).Value.HasValue))
        {
            return false;
        }

        value = rows.Average(row => ContinuousValue(row, dimension).Value!.Value);
        return true;
    }

    private static bool TryAggregatePessimisticContinuous(
        IReadOnlyList<HarnessComparisonTrialRecord> rows,
        HarnessEvaluationDimension dimension,
        bool fullyScheduled,
        bool isComparable,
        out double value,
        out bool usedSubstitution)
    {
        value = default;
        usedSubstitution = false;
        if (!fullyScheduled || !isComparable)
        {
            return false;
        }

        var total = 0d;
        foreach (var row in rows)
        {
            var continuous = ContinuousValue(row, dimension);
            var completion = BinaryValue(row, HarnessEvaluationDimension.Completion);
            if (row.Status == ExperimentItemStatus.Succeeded &&
                completion.Value == true &&
                continuous.Value.HasValue)
            {
                total += continuous.Value.Value;
            }
            else
            {
                total += continuous.PessimisticScheduledFailureValue;
                usedSubstitution = true;
            }
        }

        value = total / rows.Count;
        return true;
    }
}
