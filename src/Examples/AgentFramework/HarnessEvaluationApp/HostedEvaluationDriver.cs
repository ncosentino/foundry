using System.Diagnostics;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.AI;

using NexusLabs.Foundry.Evaluation.Experiments;
using NexusLabs.Foundry.Evaluation.Harness;

namespace HarnessEvaluationApp;

internal sealed class HostedEvaluationDriver
{
    private const int AttemptsPerBatchReservation = 6;
    // Three arms times two attempts times eight provider requests.
    private const int RequestsPerBatchReservation = 48;

    private readonly HostedEvaluationOptions _options;
    private readonly HostedRequestBudget _requestBudget;
    private readonly HostedArmExecutors _executors;
    private readonly HarnessManifestCaseSource _caseSource;
    private readonly IReadOnlyDictionary<
        HarnessComparisonArm,
        ExperimentDefinition<HarnessManifestCase, HostedTrialOutput>> _definitions;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() },
    };
    private readonly Dictionary<
        (HarnessComparisonArm Arm, string CaseId, int TrialIndex),
        HostedTrialExecutionResult> _results = [];
    private readonly Dictionary<HostedBatchKey, string> _batchArtifactReferences = [];
    private int _attemptsUsed;

    internal HostedEvaluationDriver(
        HostedEvaluationOptions options,
        Func<IChatClient>? realChatClientFactory)
    {
        ValidateOptions(options);
        _options = options;
        _requestBudget = new HostedRequestBudget(
            options.MaximumRequests,
            TimeSpan.FromMilliseconds(options.MinimumProviderRequestIntervalMilliseconds));
        _executors = new HostedArmExecutors(options, _requestBudget, realChatClientFactory);
        var manifestPath = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "eval",
            "case-sets",
            "harness-001",
            "v1.0",
            "manifest.json");
        _caseSource = HarnessManifestCaseSource.FromJson(File.ReadAllText(manifestPath));
        _definitions = new Dictionary<
            HarnessComparisonArm,
            ExperimentDefinition<HarnessManifestCase, HostedTrialOutput>>
        {
            [HarnessComparisonArm.Iterative] = HarnessComparisonExperiment.CreateIterative(
                _caseSource,
                options.GlobalRunSeed,
                _executors.RunIterativeAsync),
            [HarnessComparisonArm.PlainHarness] = HarnessComparisonExperiment.CreatePlainHarness(
                _caseSource,
                options.GlobalRunSeed,
                _executors.RunPlainHarnessAsync),
            [HarnessComparisonArm.Hybrid] = HarnessComparisonExperiment.CreateHybrid(
                _caseSource,
                options.GlobalRunSeed,
                _executors.RunHybridAsync),
        };
    }

    private static void ValidateOptions(HostedEvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelId);
        if (options.MaximumAttempts != 144 ||
            options.MaximumRequests != 1152 ||
            options.MaximumRequestsPerAttempt != 8 ||
            options.MaximumOutputTokens != 2000 ||
            options.MinimumProviderRequestIntervalMilliseconds < 0 ||
            (!options.DryRun && options.MinimumProviderRequestIntervalMilliseconds != 4000) ||
            options.WorkflowTimeoutMinutes != 60 ||
            options.SchedulingDeadlineMinutes != 50 ||
            options.MaximumConcurrency != 3 ||
            options.CostCapUsd != 25m ||
            options.EstimatedCostPerRequest != 0.02m ||
            options.GlobalRunSeed != 137 ||
            options.BatchOrderingSeed != 104729 ||
            options.ArmOrderingSeed != 130363 ||
            options.BootstrapSeed != 155921 ||
            options.AttemptTimeoutSeconds <= 0 ||
            (!options.DryRun && options.AttemptTimeoutSeconds != 120))
        {
            throw new ArgumentException(
                "Hosted evaluation options do not match the frozen harness-001 v1.0 protocol.",
                nameof(options));
        }
    }

    internal async Task<HarnessHostedRunState> RunAsync(CancellationToken cancellationToken)
    {
        PrepareOutputDirectory();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var plan = BuildPlan();
        await WriteJsonAtomicAsync(
            Path.Combine(_options.OutputDirectory, "inputs", "run-plan.json"),
            new HostedRunPlanArtifact(
                "1.0",
                _options.ModelId,
                _options.GlobalRunSeed,
                _options.BatchOrderingSeed,
                _options.ArmOrderingSeed,
                _options.BootstrapSeed,
                _options.MaximumAttempts,
                _options.MaximumRequests,
                _options.MaximumRequestsPerAttempt,
                _options.MaximumOutputTokens,
                _options.MinimumProviderRequestIntervalMilliseconds,
                _options.WorkflowTimeoutMinutes,
                _options.SchedulingDeadlineMinutes,
                _options.AttemptTimeoutSeconds,
                _options.MaximumConcurrency,
                _options.CostCapUsd,
                _options.EstimatedCostPerRequest,
                plan),
            CancellationToken.None).ConfigureAwait(false);

        var state = HarnessHostedRunState.Completed;
        var reason = "All planned complete paired batches reached a terminal state.";
        var scheduledBatchCount = 0;
        try
        {
            for (var batchIndex = 0; batchIndex < plan.Count; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CanReserveBatch(stopwatch.Elapsed))
                {
                    state = HarnessHostedRunState.TruncatedByCap;
                    reason = "A scheduling deadline, attempt, request, or estimated-cost reservation cap prevented the next complete paired batch.";
                    break;
                }

                var batch = plan[batchIndex];
                var armOrder = BuildArmOrder(batch);
                var attemptsBefore = Volatile.Read(ref _attemptsUsed);
                var requestsBefore = _requestBudget.Requests;
                var pending = new List<(
                    HarnessComparisonArm Arm,
                    Task<HostedTrialExecutionResult> Task)>();
                foreach (var arm in armOrder)
                {
                    pending.Add((
                        arm,
                        ExecuteSlotAsync(
                            arm,
                            batch,
                            cancellationToken)));
                }

                await Task.WhenAll(pending.Select(entry => entry.Task)).ConfigureAwait(false);
                var batchResults = new Dictionary<HarnessComparisonArm, HostedTrialExecutionResult>();
                foreach (var entry in pending)
                {
                    var result = await entry.Task.ConfigureAwait(false);
                    _results[(entry.Arm, batch.CaseId, batch.TrialIndex)] = result;
                    batchResults[entry.Arm] = result;
                }

                var batchRelativePath =
                    $"batches/{batchIndex + 1:D3}-{batch.CaseId}-t{batch.TrialIndex}.json";
                var batchArtifact = new HostedBatchArtifact(
                    batchIndex + 1,
                    batch,
                    armOrder,
                    batchResults,
                    Volatile.Read(ref _attemptsUsed) - attemptsBefore,
                    _requestBudget.Requests - requestsBefore,
                    (_requestBudget.Requests - requestsBefore) * _options.EstimatedCostPerRequest);
                await WriteJsonAtomicAsync(
                    Path.Combine(
                        _options.OutputDirectory,
                        batchRelativePath.Replace('/', Path.DirectorySeparatorChar)),
                    batchArtifact,
                    CancellationToken.None).ConfigureAwait(false);
                _batchArtifactReferences[batch] = batchRelativePath;
                scheduledBatchCount++;
                await WriteLedgerAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state = HarnessHostedRunState.CanceledByCaller;
            reason = "The hosted run was canceled by its caller.";
        }

        stopwatch.Stop();
        var finalizationToken = CancellationToken.None;
        var ledger = BuildLedger();
        ValidateEvidenceReferences(ledger);
        await WriteJsonAtomicAsync(
            Path.Combine(_options.OutputDirectory, "ledger", "trial-records.json"),
            ledger,
            finalizationToken).ConfigureAwait(false);
        var outcomes = BuildArmOutcomes(startedAt, stopwatch.Elapsed);
        var manifestPath = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "eval",
            "case-sets",
            "harness-001",
            "v1.0",
            "manifest.json");
        var analysisPlanPath = Path.Combine(
            Path.GetDirectoryName(manifestPath)!,
            "analysis-plan.md");
        var request = new HarnessComparisonReportRequest(
            reportId: $"harness-001-{Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "local"}",
            state,
            _caseSource.Manifest,
            CanonicalSha256(manifestPath),
            CanonicalSha256(analysisPlanPath),
            _options.BootstrapSeed,
            confidenceLevel: 0.95,
            ledger);
        var reporter = new HarnessComparisonReporter();
        var report = reporter.Build(request);
        var caseTypeInfo = (JsonTypeInfo<HarnessManifestCase>)_jsonOptions
            .GetTypeInfo(typeof(HarnessManifestCase));
        var outputTypeInfo = (JsonTypeInfo<HostedTrialOutput>)_jsonOptions
            .GetTypeInfo(typeof(HostedTrialOutput));
        await using (var stream = File.Create(
            Path.Combine(_options.OutputDirectory, "comparison-artifact.json")))
        {
            await reporter.WriteAsync(
                stream,
                report,
                outcomes,
                caseTypeInfo,
                outputTypeInfo,
                finalizationToken).ConfigureAwait(false);
        }

        await WriteJsonAtomicAsync(
            Path.Combine(_options.OutputDirectory, "run-status.json"),
            new HostedRunStatusArtifact(
                "1.0",
                state,
                reason,
                scheduledBatchCount,
                plan.Count,
                Volatile.Read(ref _attemptsUsed),
                _requestBudget.Requests,
                _requestBudget.Requests * _options.EstimatedCostPerRequest,
                AdvisoryOnly: true),
            CancellationToken.None).ConfigureAwait(false);
        WriteChecksums();
        return state;
    }

    private async Task<HostedTrialExecutionResult> ExecuteSlotAsync(
        HarnessComparisonArm arm,
        HostedBatchKey batch,
        CancellationToken callerCancellationToken)
    {
        var manifestCase = _caseSource.Manifest.Cases.Single(@case => @case.Id == batch.CaseId);
        var caseIndex = HarnessManifestCaseSource.RequiredHostedCaseIds
            .Select((id, index) => (id, index))
            .Single(entry => entry.id == batch.CaseId)
            .index;
        var experimentCase = new ExperimentCase<HarnessManifestCase>
        {
            Id = manifestCase.Id,
            Value = manifestCase,
            TrialCount = HarnessManifestCaseSource.RequiredHostedTrialCount,
            Tags = manifestCase.Tags,
        };
        var attempts = new List<ExperimentAttemptResult>();
        for (var attemptNumber = 1; attemptNumber <= 2; attemptNumber++)
        {
            callerCancellationToken.ThrowIfCancellationRequested();
            var attemptOrdinal = Interlocked.Increment(ref _attemptsUsed);
            if (attemptOrdinal > _options.MaximumAttempts)
            {
                throw new InvalidOperationException("The global attempt cap was exhausted after batch reservation.");
            }
            using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                callerCancellationToken);
            attemptCancellation.CancelAfter(TimeSpan.FromSeconds(_options.AttemptTimeoutSeconds));
            var startedAt = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var context = new ExperimentTaskContext<HarnessManifestCase>(
                    runId: $"hosted-{arm}",
                    sequence: (caseIndex * HarnessManifestCaseSource.RequiredHostedTrialCount) +
                        batch.TrialIndex - 1,
                    experimentCase,
                    batch.TrialIndex,
                    attemptNumber,
                    new ExperimentItemFeatureCollection(new Dictionary<Type, object>()));
                var output = await _definitions[arm]
                    .Task(context, attemptCancellation.Token)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                attempts.Add(ExperimentAttemptResult.Succeeded(
                    attemptNumber,
                    startedAt,
                    stopwatch.Elapsed));
                return new HostedTrialExecutionResult(
                    ExperimentItemStatus.Succeeded,
                    output,
                    attempts,
                    Failure: null);
            }
            catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception) when (
                !callerCancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                var failure = Failure(
                    ExperimentFailureCode.AttemptTimedOut,
                    exception,
                    isRetryable: attemptNumber == 1);
                if (attemptNumber == 1)
                {
                    attempts.Add(ExperimentAttemptResult.RetryScheduled(
                        attemptNumber,
                        ExperimentAttemptStatus.TimedOut,
                        startedAt,
                        stopwatch.Elapsed,
                        failure,
                        RetryDelay()));
                    await DelayBeforeRetryAsync(callerCancellationToken).ConfigureAwait(false);
                    continue;
                }

                attempts.Add(ExperimentAttemptResult.Unsuccessful(
                    attemptNumber,
                    ExperimentAttemptStatus.TimedOut,
                    startedAt,
                    stopwatch.Elapsed,
                    failure));
                return new HostedTrialExecutionResult(
                    ExperimentItemStatus.TimedOut,
                    Output: null,
                    attempts,
                    Failure(
                        ExperimentFailureCode.AttemptTimedOut,
                        exception,
                        isRetryable: false));
            }
            catch (Exception exception) when (IsTransientProviderFailure(exception))
            {
                stopwatch.Stop();
                var failure = Failure(
                    ExperimentFailureCode.ExecutionFailed,
                    exception,
                    isRetryable: attemptNumber == 1);
                if (attemptNumber == 1)
                {
                    attempts.Add(ExperimentAttemptResult.RetryScheduled(
                        attemptNumber,
                        ExperimentAttemptStatus.Failed,
                        startedAt,
                        stopwatch.Elapsed,
                        failure,
                        RetryDelay()));
                    await DelayBeforeRetryAsync(callerCancellationToken).ConfigureAwait(false);
                    continue;
                }

                attempts.Add(ExperimentAttemptResult.Unsuccessful(
                    attemptNumber,
                    ExperimentAttemptStatus.Failed,
                    startedAt,
                    stopwatch.Elapsed,
                    failure));
                return new HostedTrialExecutionResult(
                    ExperimentItemStatus.ExecutionFailed,
                    Output: null,
                    attempts,
                    Failure(
                        ExperimentFailureCode.ExecutionFailed,
                        exception,
                        isRetryable: false));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                var failure = Failure(
                    ExperimentFailureCode.ExecutionFailed,
                    exception,
                    isRetryable: false);
                attempts.Add(ExperimentAttemptResult.Unsuccessful(
                    attemptNumber,
                    ExperimentAttemptStatus.Failed,
                    startedAt,
                    stopwatch.Elapsed,
                    failure));
                return new HostedTrialExecutionResult(
                    ExperimentItemStatus.ExecutionFailed,
                    Output: null,
                    attempts,
                    failure);
            }
        }

        throw new InvalidOperationException("The bounded attempt loop did not produce a terminal result.");
    }

    private List<HostedBatchKey> BuildPlan()
    {
        var plan = HarnessManifestCaseSource.RequiredHostedCaseIds
            .SelectMany(caseId =>
                Enumerable.Range(1, HarnessManifestCaseSource.RequiredHostedTrialCount)
                    .Select(trialIndex => new HostedBatchKey(caseId, trialIndex)))
            .ToList();
        Shuffle(plan, new ExperimentDeterministicRandom(_options.BatchOrderingSeed));
        return plan;
    }

    private HarnessComparisonArm[] BuildArmOrder(HostedBatchKey batch)
    {
        var arms = Enum.GetValues<HarnessComparisonArm>();
        var seed = DeriveArmOrderingSeed(batch);
        Shuffle(arms, new ExperimentDeterministicRandom(seed));
        return arms;
    }

    private ulong DeriveArmOrderingSeed(HostedBatchKey batch)
    {
        var caseIdBytes = Encoding.UTF8.GetBytes(batch.CaseId);
        var input = new byte[sizeof(ulong) + sizeof(int) + caseIdBytes.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(input, _options.ArmOrderingSeed);
        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(sizeof(ulong)), batch.TrialIndex);
        caseIdBytes.CopyTo(input.AsSpan(sizeof(ulong) + sizeof(int)));
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(input));
    }

    private static void Shuffle<T>(IList<T> values, ExperimentDeterministicRandom random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var selected = random.NextInt32(index + 1);
            (values[index], values[selected]) = (values[selected], values[index]);
        }
    }

    private bool CanReserveBatch(TimeSpan elapsed) =>
        elapsed < TimeSpan.FromMinutes(_options.SchedulingDeadlineMinutes) &&
        elapsed + WorstCaseBatchDuration() <= TimeSpan.FromMinutes(_options.WorkflowTimeoutMinutes) &&
        Volatile.Read(ref _attemptsUsed) + AttemptsPerBatchReservation <= _options.MaximumAttempts &&
        _requestBudget.Requests + RequestsPerBatchReservation <= _options.MaximumRequests &&
        (_requestBudget.Requests + RequestsPerBatchReservation) *
            _options.EstimatedCostPerRequest <= _options.CostCapUsd;

    private TimeSpan WorstCaseBatchDuration() =>
        TimeSpan.FromSeconds(_options.AttemptTimeoutSeconds * 2) +
        TimeSpan.FromMilliseconds(
            RequestsPerBatchReservation *
            _options.MinimumProviderRequestIntervalMilliseconds);

    private IReadOnlyList<HarnessComparisonTrialRecord> BuildLedger()
    {
        var records = new List<HarnessComparisonTrialRecord>(72);
        foreach (var manifestCase in _caseSource.Manifest.Cases.Where(@case => !@case.Development))
        {
            for (var trialIndex = 1;
                 trialIndex <= HarnessManifestCaseSource.RequiredHostedTrialCount;
                 trialIndex++)
            {
                foreach (var arm in Enum.GetValues<HarnessComparisonArm>())
                {
                    records.Add(_results.TryGetValue(
                            (arm, manifestCase.Id, trialIndex),
                            out var result)
                        ? ToTrialRecord(manifestCase, arm, trialIndex, result)
                        : new HarnessComparisonTrialRecord(
                            arm,
                            manifestCase.Id,
                            trialIndex,
                            scheduled: false,
                            status: null,
                            binaryValues: [],
                            continuousValues: [],
                            responseCaptureReference: null,
                            evidenceArtifactReference: null));
                }
            }
        }

        return records;
    }

    private HarnessComparisonTrialRecord ToTrialRecord(
        HarnessManifestCase manifestCase,
        HarnessComparisonArm arm,
        int trialIndex,
        HostedTrialExecutionResult result)
    {
        var definition = HostedCaseCatalog.Get(manifestCase.Id);
        var output = result.Output;
        var binary = definition.BinaryDimensions
            .Select(dimension => new HarnessComparisonBinaryTrialValue(
                dimension,
                result.Status == ExperimentItemStatus.Succeeded
                    ? BinaryValue(definition, dimension, output)
                    : null,
                isComparable: true))
            .ToArray();
        var continuous = definition.ContinuousDimensions
            .Select(dimension => new HarnessComparisonContinuousTrialValue(
                dimension,
                result.Status == ExperimentItemStatus.Succeeded
                    ? ContinuousValue(dimension, output)
                    : null,
                PessimisticValue(dimension),
                isComparable: true))
            .ToArray();
        return new HarnessComparisonTrialRecord(
            arm,
            manifestCase.Id,
            trialIndex,
            scheduled: true,
            result.Status,
            binary,
            continuous,
            output?.CaptureReference ?? TrialCaptureReference(arm, manifestCase.Id, trialIndex),
            _batchArtifactReferences[new HostedBatchKey(manifestCase.Id, trialIndex)]);
    }

    private static string TrialCaptureReference(
        HarnessComparisonArm arm,
        string caseId,
        int trialIndex) =>
        $"capture/{HarnessComparisonExperiment.GetArmId(arm)}/{caseId}/trial-{trialIndex}/capture-manifest.json";

    private static bool BinaryValue(
        HostedCaseDefinition definition,
        HarnessEvaluationDimension dimension,
        HostedTrialOutput? output)
    {
        if (output is null)
        {
            return false;
        }

        return dimension switch
        {
            HarnessEvaluationDimension.Completion => output.Completion,
            HarnessEvaluationDimension.Continuity => output.Completion,
            HarnessEvaluationDimension.ContextSafety =>
                output.TerminalCategory == HarnessRunTerminalCategory.Completed &&
                output.CumulativeTokens <= 80_000,
            HarnessEvaluationDimension.ArtifactReuse => output.Completion,
            HarnessEvaluationDimension.ToolTrajectory =>
                ContainsInOrder(output.ToolCalls, definition.RequiredTools) &&
                !output.ToolCalls.Any(definition.ForbiddenTools.Contains),
            HarnessEvaluationDimension.Cancellation =>
                output.TerminalCategory == HarnessRunTerminalCategory.PerAttemptTimeout,
            HarnessEvaluationDimension.Termination => definition.ExpectsTimeout
                ? output.TerminalCategory == HarnessRunTerminalCategory.PerAttemptTimeout
                : output.TerminalCategory == HarnessRunTerminalCategory.Completed,
            _ => throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "The hosted binary dimension is not supported."),
        };
    }

    private static double ContinuousValue(
        HarnessEvaluationDimension dimension,
        HostedTrialOutput? output) =>
        dimension switch
        {
            HarnessEvaluationDimension.CumulativeTokens => output?.CumulativeTokens ?? 0,
            HarnessEvaluationDimension.PeakTokens => output?.PeakTokens ?? 0,
            HarnessEvaluationDimension.CostAttribution => output?.CumulativeTokens ?? 0,
            HarnessEvaluationDimension.Latency => output?.LatencyMilliseconds ?? 0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "The hosted continuous dimension is not supported."),
        };

    private double PessimisticValue(HarnessEvaluationDimension dimension) =>
        dimension switch
        {
            HarnessEvaluationDimension.CumulativeTokens =>
                _options.MaximumRequestsPerAttempt * 10_000,
            HarnessEvaluationDimension.PeakTokens => 10_000,
            HarnessEvaluationDimension.CostAttribution =>
                _options.MaximumRequestsPerAttempt * 10_000,
            HarnessEvaluationDimension.Latency =>
                _options.AttemptTimeoutSeconds * 2 * 1000,
            _ => throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "The hosted continuous dimension is not supported."),
        };

    private HarnessComparisonArmOutcomes<HostedTrialOutput> BuildArmOutcomes(
        DateTimeOffset startedAt,
        TimeSpan duration) =>
        new(
            BuildArmOutcome(HarnessComparisonArm.Iterative, startedAt, duration),
            BuildArmOutcome(HarnessComparisonArm.PlainHarness, startedAt, duration),
            BuildArmOutcome(HarnessComparisonArm.Hybrid, startedAt, duration));

    private ExperimentRunOutcome<HarnessManifestCase, HostedTrialOutput> BuildArmOutcome(
        HarnessComparisonArm arm,
        DateTimeOffset startedAt,
        TimeSpan duration)
    {
        var items = new List<ExperimentItemResult<HarnessManifestCase, HostedTrialOutput>>();
        foreach (var caseEntry in HarnessManifestCaseSource.RequiredHostedCaseIds
                     .Select((id, index) => (id, index)))
        {
            var manifestCase = _caseSource.Manifest.Cases.Single(@case => @case.Id == caseEntry.id);
            for (var trialIndex = 1;
                 trialIndex <= HarnessManifestCaseSource.RequiredHostedTrialCount;
                 trialIndex++)
            {
                if (!_results.TryGetValue((arm, manifestCase.Id, trialIndex), out var result))
                {
                    continue;
                }

                var @case = new ExperimentCase<HarnessManifestCase>
                {
                    Id = manifestCase.Id,
                    Value = manifestCase,
                    TrialCount = HarnessManifestCaseSource.RequiredHostedTrialCount,
                    Tags = manifestCase.Tags,
                };
                var sequence = items.Count;
                items.Add(result.Status == ExperimentItemStatus.Succeeded
                    ? ExperimentItemResult<HarnessManifestCase, HostedTrialOutput>.Succeeded(
                        sequence,
                        @case,
                        trialIndex,
                        result.Attempts,
                        result.Output!,
                        evaluation: null,
                        publications: [])
                    : ExperimentItemResult<HarnessManifestCase, HostedTrialOutput>.Failed(
                        sequence,
                        @case,
                        trialIndex,
                        result.Status,
                        result.Attempts,
                        result.Failure!,
                        publications: []));
            }
        }

        var runResult = new ExperimentRunResult<HarnessManifestCase, HostedTrialOutput>(
            runId: $"hosted-{HarnessComparisonExperiment.GetArmId(arm)}",
            experimentName: _definitions[arm].Name,
            source: new ExperimentSourceReference
            {
                Name = "harness-001 v1.0",
                Id = "harness-001",
                Version = "v1.0",
            },
            startedAt,
            duration,
            maxConcurrency: _options.MaximumConcurrency,
            workerCount: items.Count == 0
                ? 0
                : Math.Min(_options.MaximumConcurrency, items.Count),
            items,
            runEvaluations: [],
            policyResults: []);
        return new ExperimentRunOutcome<HarnessManifestCase, HostedTrialOutput>(
            runResult,
            sinkResults: []);
    }

    private async Task WriteLedgerAsync(CancellationToken cancellationToken) =>
        await WriteJsonAtomicAsync(
            Path.Combine(_options.OutputDirectory, "ledger", "trial-records.json"),
            BuildLedger(),
            cancellationToken).ConfigureAwait(false);

    private void ValidateEvidenceReferences(
        IReadOnlyList<HarnessComparisonTrialRecord> ledger)
    {
        foreach (var record in ledger.Where(record => record.Scheduled))
        {
            if (string.IsNullOrWhiteSpace(record.EvidenceArtifactReference) ||
                string.IsNullOrWhiteSpace(record.ResponseCaptureReference))
            {
                throw new InvalidDataException(
                    $"Scheduled row '{record.Arm}/{record.CaseId}/{record.TrialIndex}' is missing an evidence or capture reference.");
            }

            var evidencePath = Path.Combine(
                _options.OutputDirectory,
                record.EvidenceArtifactReference
                    .Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(evidencePath))
            {
                throw new InvalidDataException(
                    $"Scheduled row '{record.Arm}/{record.CaseId}/{record.TrialIndex}' references missing evidence '{record.EvidenceArtifactReference}'.");
            }

            var capturePath = Path.Combine(
                _options.OutputDirectory,
                record.ResponseCaptureReference
                    .Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(capturePath))
            {
                throw new InvalidDataException(
                    $"Scheduled row '{record.Arm}/{record.CaseId}/{record.TrialIndex}' references missing capture '{record.ResponseCaptureReference}'.");
            }
        }
    }

    private async Task WriteJsonAtomicAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                value,
                _jsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    private void PrepareOutputDirectory()
    {
        Directory.CreateDirectory(_options.OutputDirectory);
        Directory.CreateDirectory(Path.Combine(_options.OutputDirectory, "inputs"));
        Directory.CreateDirectory(Path.Combine(_options.OutputDirectory, "batches"));
        Directory.CreateDirectory(Path.Combine(_options.OutputDirectory, "ledger"));
    }

    private void WriteChecksums()
    {
        var checksumPath = Path.Combine(_options.OutputDirectory, "checksums.sha256");
        var lines = Directory
            .EnumerateFiles(_options.OutputDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(path, checksumPath, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(path =>
                $"{CanonicalSha256(path)}  {Path.GetRelativePath(_options.OutputDirectory, path).Replace('\\', '/')}")
            .ToArray();
        File.WriteAllLines(checksumPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string CanonicalSha256(string path)
    {
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
    }

    private static ExperimentFailure Failure(
        ExperimentFailureCode code,
        Exception exception,
        bool isRetryable) =>
        new(
            code,
            ExperimentFailureStage.Execution,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            isRetryable);

    private TimeSpan RetryDelay() =>
        _options.DryRun ? TimeSpan.Zero : TimeSpan.FromSeconds(1);

    private async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        var delay = RetryDelay();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static bool IsTransientProviderFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException httpRequest &&
                (httpRequest.StatusCode is null ||
                 IsRetryableStatus((int)httpRequest.StatusCode)))
            {
                return true;
            }

            if (current.Message.Contains("Status: 429", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Status: 500", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Status: 502", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Status: 503", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Status: 504", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRetryableStatus(int status) =>
        status is 429 or 500 or 502 or 503 or 504;

    private static bool ContainsInOrder(
        IReadOnlyList<string> observed,
        IReadOnlyList<string> required)
    {
        var requiredIndex = 0;
        foreach (var tool in observed)
        {
            if (requiredIndex < required.Count &&
                string.Equals(tool, required[requiredIndex], StringComparison.Ordinal))
            {
                requiredIndex++;
            }
        }

        return requiredIndex == required.Count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "artifacts",
                    "eval",
                    "case-sets",
                    "harness-001",
                    "v1.0",
                    "manifest.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
