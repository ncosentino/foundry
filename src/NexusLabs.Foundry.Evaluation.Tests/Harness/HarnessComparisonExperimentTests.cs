using NexusLabs.Foundry.Evaluation.Experiments;
using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessComparisonExperimentTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Factories_ShareFrozenSourceAndDerivePairedTrialSeed()
    {
        var source = CreateSource();
        HarnessComparisonArm? iterativeArm = null;
        HarnessComparisonArm? plainHarnessArm = null;
        HarnessComparisonArm? hybridArm = null;

        var iterative = HarnessComparisonExperiment.CreateIterative(
            source,
            globalRunSeed: 123,
            (context, _) =>
            {
                iterativeArm = context.Arm;
                return ValueTask.FromResult(context.TrialSeed);
            });
        var plainHarness = HarnessComparisonExperiment.CreatePlainHarness(
            source,
            globalRunSeed: 123,
            (context, _) =>
            {
                plainHarnessArm = context.Arm;
                return ValueTask.FromResult(context.TrialSeed);
            });
        var hybrid = HarnessComparisonExperiment.CreateHybrid(
            source,
            globalRunSeed: 123,
            (context, _) =>
            {
                hybridArm = context.Arm;
                return ValueTask.FromResult(context.TrialSeed);
            });

        Assert.Same(source, iterative.CaseSource);
        Assert.Same(source, plainHarness.CaseSource);
        Assert.Same(source, hybrid.CaseSource);
        Assert.Equal("harness-001/v1.0/iterative", iterative.Name);
        Assert.Equal("harness-001/v1.0/plain-harness", plainHarness.Name);
        Assert.Equal("harness-001/v1.0/hybrid", hybrid.Name);

        var taskContext = CreateTaskContext(source, caseIndex: 0, trialIndex: 1, attemptNumber: 1);
        var iterativeSeed = await iterative.Task(taskContext, _ct);
        var plainHarnessSeed = await plainHarness.Task(taskContext, _ct);
        var hybridSeed = await hybrid.Task(taskContext, _ct);

        Assert.Equal(HarnessComparisonArm.Iterative, iterativeArm);
        Assert.Equal(HarnessComparisonArm.PlainHarness, plainHarnessArm);
        Assert.Equal(HarnessComparisonArm.Hybrid, hybridArm);
        Assert.Equal(iterativeSeed, plainHarnessSeed);
        Assert.Equal(iterativeSeed, hybridSeed);
        Assert.Equal(2797741583339985201UL, iterativeSeed);
    }

    [Fact]
    public async Task Seed_IsStableAcrossRetriesAndChangesAcrossCaseTrialOrGlobalSeed()
    {
        var source = CreateSource();
        var seed123 = HarnessComparisonExperiment.CreateIterative(
            source,
            globalRunSeed: 123,
            (context, _) => ValueTask.FromResult(context.TrialSeed));
        var seed124 = HarnessComparisonExperiment.CreateIterative(
            source,
            globalRunSeed: 124,
            (context, _) => ValueTask.FromResult(context.TrialSeed));

        var firstAttempt = await seed123.Task(
            CreateTaskContext(source, caseIndex: 0, trialIndex: 1, attemptNumber: 1),
            _ct);
        var retry = await seed123.Task(
            CreateTaskContext(source, caseIndex: 0, trialIndex: 1, attemptNumber: 2),
            _ct);
        var nextTrial = await seed123.Task(
            CreateTaskContext(source, caseIndex: 0, trialIndex: 2, attemptNumber: 1),
            _ct);
        var nextCase = await seed123.Task(
            CreateTaskContext(source, caseIndex: 1, trialIndex: 1, attemptNumber: 1),
            _ct);
        var differentGlobalSeed = await seed124.Task(
            CreateTaskContext(source, caseIndex: 0, trialIndex: 1, attemptNumber: 1),
            _ct);

        Assert.Equal(firstAttempt, retry);
        Assert.NotEqual(firstAttempt, nextTrial);
        Assert.NotEqual(firstAttempt, nextCase);
        Assert.NotEqual(firstAttempt, differentGlobalSeed);
    }

    [Fact]
    public void Factories_RejectNullSourceOrExecutor()
    {
        var source = CreateSource();
        HarnessComparisonArmExecutor<ulong> executor =
            (context, _) => ValueTask.FromResult(context.TrialSeed);

        Assert.Throws<ArgumentNullException>(() =>
            HarnessComparisonExperiment.CreateIterative<ulong>(null!, 123, executor));
        Assert.Throws<ArgumentNullException>(() =>
            HarnessComparisonExperiment.CreateIterative<ulong>(source, 123, null!));
        Assert.Throws<ArgumentNullException>(() =>
            HarnessComparisonExperiment.CreatePlainHarness<ulong>(null!, 123, executor));
        Assert.Throws<ArgumentNullException>(() =>
            HarnessComparisonExperiment.CreateHybrid<ulong>(source, 123, null!));
    }

    private static HarnessManifestCaseSource CreateSource()
    {
        var json = HarnessManifestTestFiles.TryReadManifestJson();
        Assert.SkipWhen(json is null, "The on-disk harness-001 v1.0 manifest was not found.");
        return HarnessManifestCaseSource.FromJson(json!);
    }

    private static ExperimentTaskContext<HarnessManifestCase> CreateTaskContext(
        HarnessManifestCaseSource source,
        int caseIndex,
        int trialIndex,
        int attemptNumber)
    {
        var manifestCase = source.Manifest.Cases[caseIndex];
        var @case = new ExperimentCase<HarnessManifestCase>
        {
            Id = manifestCase.Id,
            Value = manifestCase,
            TrialCount = HarnessManifestCaseSource.RequiredHostedTrialCount,
            Tags = manifestCase.Tags,
        };
        return new ExperimentTaskContext<HarnessManifestCase>(
            runId: "run-1",
            sequence: (caseIndex * HarnessManifestCaseSource.RequiredHostedTrialCount) + trialIndex - 1,
            @case,
            trialIndex,
            attemptNumber,
            new ExperimentItemFeatureCollection(new Dictionary<Type, object>()));
    }
}
