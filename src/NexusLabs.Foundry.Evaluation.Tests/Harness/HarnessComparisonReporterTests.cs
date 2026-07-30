using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using NexusLabs.Foundry.Evaluation.Experiments;
using NexusLabs.Foundry.Evaluation.Harness;
using NexusLabs.Foundry.Evaluation.Harness.Judging;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessComparisonReporterTests
{
    private const double ConfidenceLevel = 0.95;

    [Fact]
    public void Build_CompletedLedger_ProducesThreeCaseLevelContrasts()
    {
        var source = CreateSource();
        var binaryOverrides = new Dictionary<BinaryKey, BinaryOverride>();
        foreach (var caseId in new[] { "h001-01", "h001-02", "h001-03", "h001-04" })
        {
            SetBinaryCase(
                binaryOverrides,
                HarnessComparisonArm.Iterative,
                caseId,
                HarnessEvaluationDimension.Completion,
                value: false);
        }

        foreach (var caseId in new[] { "h001-01", "h001-02" })
        {
            SetBinaryCase(
                binaryOverrides,
                HarnessComparisonArm.PlainHarness,
                caseId,
                HarnessEvaluationDimension.Completion,
                value: false);
        }

        var report = new HarnessComparisonReporter().Build(CreateRequest(
            source,
            HarnessHostedRunState.Completed,
            BuildRows(
                source,
                new HashSet<TrialKey>(),
                new Dictionary<TrialKey, ExperimentItemStatus>(),
                binaryOverrides,
                new Dictionary<ContinuousKey, ContinuousOverride>())));

        Assert.Equal(3, report.Contrasts.Count);
        Assert.Equal(8, report.FullyScheduledCaseCount);
        Assert.True(report.RetentionEligible);

        var contrast = Assert.Single(report.Contrasts, candidate =>
            candidate.XArm == HarnessComparisonArm.PlainHarness &&
            candidate.YArm == HarnessComparisonArm.Iterative);
        Assert.Equal(8, contrast.DiagnosticsParity.ComparableCaseCount);
        Assert.Equal(0, contrast.DiagnosticsParity.NonComparableCaseCount);
        var completion = Assert.Single(contrast.BinaryDimensions, candidate =>
            candidate.Dimension == HarnessEvaluationDimension.Completion);
        Assert.Equal(8, completion.Pessimistic.ValidPairCount);
        Assert.Equal(HarnessComparisonExperiment.PlainHarnessArmId, completion.Pessimistic.XLabel);
        Assert.Equal(HarnessComparisonExperiment.IterativeArmId, completion.Pessimistic.YLabel);
        Assert.Equal(2, completion.Pessimistic.BCount);
        Assert.Equal(0, completion.Pessimistic.CCount);
        Assert.Equal(0.25, completion.Pessimistic.Delta);
        Assert.Equal(8, completion.Inconclusive.ValidPairCount);

        var latency = Assert.Single(contrast.ContinuousDimensions, candidate =>
            candidate.Dimension == HarnessEvaluationDimension.Latency);
        Assert.Equal(1, latency.Conditional.ValidPairCount);
        Assert.Equal(-20, latency.Conditional.MeanDifference);
        Assert.Equal(2, latency.PessimisticSensitivity.ValidPairCount);
    }

    [Fact]
    public void Build_UnscorableScheduledTrial_DivergesPrimaryAndInconclusiveBinaryEvidence()
    {
        var source = CreateSource();
        var statusOverrides = new Dictionary<TrialKey, ExperimentItemStatus>
        {
            [new TrialKey(
                HarnessComparisonArm.PlainHarness,
                "h001-01",
                TrialIndex: 3)] = ExperimentItemStatus.TimedOut,
        };
        var binaryOverrides = new Dictionary<BinaryKey, BinaryOverride>
        {
            [new BinaryKey(
                HarnessComparisonArm.PlainHarness,
                "h001-01",
                TrialIndex: 3,
                HarnessEvaluationDimension.Completion)] = new BinaryOverride(
                    Value: null,
                    IsComparable: true),
        };

        var report = new HarnessComparisonReporter().Build(CreateRequest(
            source,
            HarnessHostedRunState.Completed,
            BuildRows(
                source,
                new HashSet<TrialKey>(),
                statusOverrides,
                binaryOverrides,
                new Dictionary<ContinuousKey, ContinuousOverride>())));
        var contrast = Assert.Single(report.Contrasts, candidate =>
            candidate.XArm == HarnessComparisonArm.PlainHarness &&
            candidate.YArm == HarnessComparisonArm.Iterative);
        var completion = Assert.Single(contrast.BinaryDimensions, candidate =>
            candidate.Dimension == HarnessEvaluationDimension.Completion);

        Assert.Equal(8, completion.Pessimistic.ValidPairCount);
        Assert.Equal(7, completion.Inconclusive.ValidPairCount);
        Assert.Equal(1, completion.Inconclusive.ExcludedCaseCount);
    }

    [Fact]
    public void Build_DiagnosticsMismatch_IsReportedAndGatesAffectedDimension()
    {
        var source = CreateSource();
        var binaryOverrides = new Dictionary<BinaryKey, BinaryOverride>
        {
            [new BinaryKey(
                HarnessComparisonArm.PlainHarness,
                "h001-01",
                TrialIndex: 1,
                HarnessEvaluationDimension.ToolTrajectory)] = new BinaryOverride(
                    Value: true,
                    IsComparable: false),
        };
        var report = new HarnessComparisonReporter().Build(CreateRequest(
            source,
            HarnessHostedRunState.Completed,
            BuildRows(
                source,
                new HashSet<TrialKey>(),
                new Dictionary<TrialKey, ExperimentItemStatus>(),
                binaryOverrides,
                new Dictionary<ContinuousKey, ContinuousOverride>())));
        var contrast = Assert.Single(report.Contrasts, candidate =>
            candidate.XArm == HarnessComparisonArm.PlainHarness &&
            candidate.YArm == HarnessComparisonArm.Iterative);

        Assert.Equal(7, contrast.DiagnosticsParity.ComparableCaseCount);
        Assert.Equal(1, contrast.DiagnosticsParity.NonComparableCaseCount);
        var toolTrajectory = Assert.Single(contrast.BinaryDimensions, candidate =>
            candidate.Dimension == HarnessEvaluationDimension.ToolTrajectory);
        Assert.Equal(1, toolTrajectory.Pessimistic.ValidPairCount);
        Assert.Equal(1, toolTrajectory.Pessimistic.NonComparableCaseCount);
    }

    [Fact]
    public void Build_TruncatedCompleteBatch_ExcludesIncompleteCaseSymmetrically()
    {
        var source = CreateSource();
        var unscheduled = Enum.GetValues<HarnessComparisonArm>()
            .Select(arm => new TrialKey(arm, "h001-08", TrialIndex: 3))
            .ToHashSet();

        var report = new HarnessComparisonReporter().Build(CreateRequest(
            source,
            HarnessHostedRunState.TruncatedByCap,
            BuildRows(
                source,
                unscheduled,
                new Dictionary<TrialKey, ExperimentItemStatus>(),
                new Dictionary<BinaryKey, BinaryOverride>(),
                new Dictionary<ContinuousKey, ContinuousOverride>())));

        Assert.Equal(7, report.FullyScheduledCaseCount);
        Assert.True(report.RetentionEligible);
        Assert.All(report.Contrasts, contrast =>
        {
            var completion = Assert.Single(contrast.BinaryDimensions, candidate =>
                candidate.Dimension == HarnessEvaluationDimension.Completion);
            Assert.Equal(1, completion.IncompleteDueToCapCaseCount);
            Assert.Equal(7, completion.Pessimistic.ValidPairCount);
        });
    }

    [Fact]
    public void Build_AsymmetricScheduling_Throws()
    {
        var source = CreateSource();
        var unscheduled = new HashSet<TrialKey>
        {
            new(HarnessComparisonArm.Iterative, "h001-08", TrialIndex: 3),
        };
        var request = CreateRequest(
            source,
            HarnessHostedRunState.TruncatedByCap,
            BuildRows(
                source,
                unscheduled,
                new Dictionary<TrialKey, ExperimentItemStatus>(),
                new Dictionary<BinaryKey, BinaryOverride>(),
                new Dictionary<ContinuousKey, ContinuousOverride>()));

        Assert.Throws<ArgumentException>(() => new HarnessComparisonReporter().Build(request));
    }

    [Fact]
    public void Build_InvalidInput_SuppressesComparativeEvidence()
    {
        var source = CreateSource();
        var report = new HarnessComparisonReporter().Build(CreateRequest(
            source,
            HarnessHostedRunState.InvalidInput,
            []));

        Assert.Empty(report.Contrasts);
        Assert.Equal(0, report.FullyScheduledCaseCount);
        Assert.False(report.RetentionEligible);
    }

    [Fact]
    public void Build_JudgeConflict_ReportsDisagreementWithDeterministicAuthority()
    {
        var source = CreateSource();
        var observation = new HarnessJudgeComparisonObservation(
            caseId: "h001-01",
            dimension: HarnessEvaluationDimension.Completion,
            xArm: HarnessComparisonArm.PlainHarness,
            yArm: HarnessComparisonArm.Iterative,
            deterministicPreference: HarnessPairwisePreference.Left,
            judgePreference: HarnessPairwisePreference.Right,
            isOrderConsistent: true,
            calibrationState: HarnessJudgeCalibrationState.Uncalibrated);
        var request = CreateRequest(
            source,
            HarnessHostedRunState.Completed,
            BuildRows(
                source,
                new HashSet<TrialKey>(),
                new Dictionary<TrialKey, ExperimentItemStatus>(),
                new Dictionary<BinaryKey, BinaryOverride>(),
                new Dictionary<ContinuousKey, ContinuousOverride>()),
            [observation]);

        var report = new HarnessComparisonReporter().Build(request);

        Assert.Equal(1, report.JudgeDisagreement.TotalObservationCount);
        Assert.Equal(1, report.JudgeDisagreement.DisagreementCount);
        Assert.False(report.JudgeDisagreement.UsableForArmRanking);
        var disagreement = Assert.Single(report.JudgeDisagreement.Observations);
        Assert.True(disagreement.IsDisagreement);
        Assert.True(disagreement.DeterministicGoverns);
    }

    [Fact]
    public void Build_DuplicateJudgeObservation_Throws()
    {
        var source = CreateSource();
        var observation = new HarnessJudgeComparisonObservation(
            caseId: "h001-01",
            dimension: HarnessEvaluationDimension.Completion,
            xArm: HarnessComparisonArm.PlainHarness,
            yArm: HarnessComparisonArm.Iterative,
            deterministicPreference: HarnessPairwisePreference.Left,
            judgePreference: HarnessPairwisePreference.Right,
            isOrderConsistent: true,
            calibrationState: HarnessJudgeCalibrationState.Uncalibrated);
        var request = CreateRequest(
            source,
            HarnessHostedRunState.Completed,
            BuildRows(
                source,
                new HashSet<TrialKey>(),
                new Dictionary<TrialKey, ExperimentItemStatus>(),
                new Dictionary<BinaryKey, BinaryOverride>(),
                new Dictionary<ContinuousKey, ContinuousOverride>()),
            [observation, observation]);

        Assert.Throws<ArgumentException>(() => new HarnessComparisonReporter().Build(request));
    }

    [Fact]
    public async Task WriteAsync_EmbedsCanonicalOutcomesFromExperimentArtifactWriter()
    {
        var source = CreateSource();
        var rows = BuildRows(
            source,
            new HashSet<TrialKey>(),
            new Dictionary<TrialKey, ExperimentItemStatus>(),
            new Dictionary<BinaryKey, BinaryOverride>(),
            new Dictionary<ContinuousKey, ContinuousOverride>());
        var reporter = new HarnessComparisonReporter();
        var report = reporter.Build(CreateRequest(
            source,
            HarnessHostedRunState.Completed,
            rows,
            Array.Empty<HarnessJudgeComparisonObservation>()));
        var runner = new ExperimentRunner();
        var iterative = await RunArmAsync(
            runner,
            HarnessComparisonExperiment.CreateIterative(
                source,
                globalRunSeed: 123,
                (context, _) => ValueTask.FromResult($"iterative:{context.Case.Id}:{context.TrialIndex}")),
            "iterative-run");
        var plainHarness = await RunArmAsync(
            runner,
            HarnessComparisonExperiment.CreatePlainHarness(
                source,
                globalRunSeed: 123,
                (context, _) => ValueTask.FromResult($"plain:{context.Case.Id}:{context.TrialIndex}")),
            "plain-run");
        var hybrid = await RunArmAsync(
            runner,
            HarnessComparisonExperiment.CreateHybrid(
                source,
                globalRunSeed: 123,
                (context, _) => ValueTask.FromResult($"hybrid:{context.Case.Id}:{context.TrialIndex}")),
            "hybrid-run");
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var caseTypeInfo = (JsonTypeInfo<HarnessManifestCase>)options.GetTypeInfo(typeof(HarnessManifestCase));
        var outputTypeInfo = (JsonTypeInfo<string>)options.GetTypeInfo(typeof(string));
        using var stream = new MemoryStream();

        await reporter.WriteAsync(
            stream,
            report,
            new HarnessComparisonArmOutcomes<string>(iterative, plainHarness, hybrid),
            caseTypeInfo,
            outputTypeInfo,
            TestContext.Current.CancellationToken);

        stream.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: TestContext.Current.CancellationToken);
        var root = document.RootElement;
        Assert.Equal(HarnessComparisonReporter.ArtifactSchemaVersion, root.GetProperty("schemaVersion").GetString());
        var reportJson = root.GetProperty("report");
        Assert.Equal("report-1", reportJson.GetProperty("reportId").GetString());
        Assert.True(
            reportJson.GetProperty("contrasts")[0].TryGetProperty("diagnosticsParity", out _));
        var outcomes = root.GetProperty("canonicalOutcomes");
        Assert.Equal(
            HarnessComparisonExperiment.IterativeExperimentName,
            outcomes.GetProperty("iterative").GetProperty("result").GetProperty("experimentName").GetString());
        Assert.Equal(
            HarnessComparisonExperiment.PlainHarnessExperimentName,
            outcomes.GetProperty("plainHarness").GetProperty("result").GetProperty("experimentName").GetString());
        Assert.Equal(
            HarnessComparisonExperiment.HybridExperimentName,
            outcomes.GetProperty("hybrid").GetProperty("result").GetProperty("experimentName").GetString());
    }

    private static HarnessComparisonReportRequest CreateRequest(
        HarnessManifestCaseSource source,
        HarnessHostedRunState runState,
        IReadOnlyList<HarnessComparisonTrialRecord> trials) =>
        CreateRequest(
            source,
            runState,
            trials,
            Array.Empty<HarnessJudgeComparisonObservation>());

    private static HarnessComparisonReportRequest CreateRequest(
        HarnessManifestCaseSource source,
        HarnessHostedRunState runState,
        IReadOnlyList<HarnessComparisonTrialRecord> trials,
        IReadOnlyList<HarnessJudgeComparisonObservation> judgeObservations) =>
        new(
            reportId: "report-1",
            runState,
            source.Manifest,
            manifestSha256: new string('a', 64),
            analysisPlanSha256: new string('b', 64),
            bootstrapSeed: 123,
            confidenceLevel: ConfidenceLevel,
            trials,
            judgeObservations);

    private static Task<ExperimentRunOutcome<HarnessManifestCase, string>> RunArmAsync(
        ExperimentRunner runner,
        ExperimentDefinition<HarnessManifestCase, string> definition,
        string runId) =>
        runner.RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 3,
            },
            CancellationToken.None);

    private static IReadOnlyList<HarnessComparisonTrialRecord> BuildRows(
        HarnessManifestCaseSource source,
        IReadOnlySet<TrialKey> unscheduled,
        IReadOnlyDictionary<TrialKey, ExperimentItemStatus> statusOverrides,
        IReadOnlyDictionary<BinaryKey, BinaryOverride> binaryOverrides,
        IReadOnlyDictionary<ContinuousKey, ContinuousOverride> continuousOverrides)
    {
        var rows = new List<HarnessComparisonTrialRecord>();
        foreach (var manifestCase in source.Manifest.Cases.Where(@case => !@case.Development))
        {
            for (var trialIndex = 1;
                 trialIndex <= HarnessManifestCaseSource.RequiredHostedTrialCount;
                 trialIndex++)
            {
                foreach (var arm in Enum.GetValues<HarnessComparisonArm>())
                {
                    var trialKey = new TrialKey(arm, manifestCase.Id, trialIndex);
                    var scheduled = !unscheduled.Contains(trialKey);
                    if (!scheduled)
                    {
                        rows.Add(new HarnessComparisonTrialRecord(
                            arm,
                            manifestCase.Id,
                            trialIndex,
                            scheduled: false,
                            status: null,
                            binaryValues: [],
                            continuousValues: [],
                            responseCaptureReference: null,
                            evidenceArtifactReference: null));
                        continue;
                    }

                    var status = statusOverrides.TryGetValue(trialKey, out var overriddenStatus)
                        ? overriddenStatus
                        : ExperimentItemStatus.Succeeded;
                    var binaryValues = manifestCase.DeterministicReferences
                        .Where(reference => IsBinary(reference.Dimension))
                        .Select(reference =>
                        {
                            var key = new BinaryKey(arm, manifestCase.Id, trialIndex, reference.Dimension);
                            var value = binaryOverrides.TryGetValue(key, out var overridden)
                                ? overridden
                                : new BinaryOverride(Value: true, IsComparable: true);
                            return new HarnessComparisonBinaryTrialValue(
                                reference.Dimension,
                                value.Value,
                                value.IsComparable);
                        })
                        .ToArray();
                    var continuousValues = manifestCase.DeterministicReferences
                        .Where(reference => IsContinuous(reference.Dimension))
                        .Select(reference =>
                        {
                            var key = new ContinuousKey(arm, manifestCase.Id, trialIndex, reference.Dimension);
                            var value = continuousOverrides.TryGetValue(key, out var overridden)
                                ? overridden
                                : DefaultContinuousValue(arm, reference.Dimension);
                            return new HarnessComparisonContinuousTrialValue(
                                reference.Dimension,
                                value.Value,
                                value.PessimisticValue,
                                value.IsComparable);
                        })
                        .ToArray();

                    rows.Add(new HarnessComparisonTrialRecord(
                        arm,
                        manifestCase.Id,
                        trialIndex,
                        scheduled: true,
                        status,
                        binaryValues,
                        continuousValues,
                        responseCaptureReference: $"responses/{arm}/{manifestCase.Id}/{trialIndex}",
                        evidenceArtifactReference: $"evidence/{arm}/{manifestCase.Id}/{trialIndex}.json"));
                }
            }
        }

        return rows;
    }

    private static ContinuousOverride DefaultContinuousValue(
        HarnessComparisonArm arm,
        HarnessEvaluationDimension dimension)
    {
        var armOffset = arm switch
        {
            HarnessComparisonArm.Iterative => 100,
            HarnessComparisonArm.PlainHarness => 80,
            HarnessComparisonArm.Hybrid => 70,
            _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "The comparison arm is not defined."),
        };
        var value = dimension == HarnessEvaluationDimension.Latency
            ? armOffset
            : armOffset * 10;
        return new ContinuousOverride(
            Value: value,
            PessimisticValue: value * 10,
            IsComparable: true);
    }

    private static void SetBinaryCase(
        Dictionary<BinaryKey, BinaryOverride> overrides,
        HarnessComparisonArm arm,
        string caseId,
        HarnessEvaluationDimension dimension,
        bool value)
    {
        for (var trialIndex = 1;
             trialIndex <= HarnessManifestCaseSource.RequiredHostedTrialCount;
             trialIndex++)
        {
            overrides[new BinaryKey(arm, caseId, trialIndex, dimension)] =
                new BinaryOverride(value, IsComparable: true);
        }
    }

    private static bool IsBinary(HarnessEvaluationDimension dimension) =>
        dimension is
            HarnessEvaluationDimension.Completion or
            HarnessEvaluationDimension.Continuity or
            HarnessEvaluationDimension.ContextSafety or
            HarnessEvaluationDimension.ArtifactReuse or
            HarnessEvaluationDimension.ToolTrajectory or
            HarnessEvaluationDimension.Cancellation or
            HarnessEvaluationDimension.Termination;

    private static bool IsContinuous(HarnessEvaluationDimension dimension) =>
        dimension is
            HarnessEvaluationDimension.CumulativeTokens or
            HarnessEvaluationDimension.PeakTokens or
            HarnessEvaluationDimension.CostAttribution or
            HarnessEvaluationDimension.Latency;

    private static HarnessManifestCaseSource CreateSource()
    {
        var json = HarnessManifestTestFiles.TryReadManifestJson();
        Assert.SkipWhen(json is null, "The on-disk harness-001 v1.0 manifest was not found.");
        return HarnessManifestCaseSource.FromJson(json!);
    }

}
