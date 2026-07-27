using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// Creates the three provider-neutral experiment definitions used by the <c>harness-001</c> paired
/// comparison while keeping concrete iterative, Harness, and hybrid execution behind injected
/// arm-executor delegates.
/// </summary>
public static class HarnessComparisonExperiment
{
    /// <summary>The stable arm identifier for the iterative arm.</summary>
    public const string IterativeArmId = "iterative";

    /// <summary>The stable arm identifier for the plain Harness arm.</summary>
    public const string PlainHarnessArmId = "plain-harness";

    /// <summary>The stable arm identifier for the hybrid arm.</summary>
    public const string HybridArmId = "hybrid";

    /// <summary>The stable experiment name for the iterative arm.</summary>
    public const string IterativeExperimentName = "harness-001/v1.0/" + IterativeArmId;

    /// <summary>The stable experiment name for the plain Harness arm.</summary>
    public const string PlainHarnessExperimentName = "harness-001/v1.0/" + PlainHarnessArmId;

    /// <summary>The stable experiment name for the hybrid Harness/workspace arm.</summary>
    public const string HybridExperimentName = "harness-001/v1.0/" + HybridArmId;

    /// <summary>
    /// Creates the current Foundry iterative-loop arm.
    /// </summary>
    /// <typeparam name="TOutput">The caller-owned normalized trial output type.</typeparam>
    /// <param name="caseSource">The frozen case source shared by all three arms.</param>
    /// <param name="globalRunSeed">The pinned global seed used to derive case/trial seeds.</param>
    /// <param name="executor">The injected iterative arm executor.</param>
    /// <returns>The provider-neutral iterative experiment definition.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="caseSource"/> or <paramref name="executor"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentDefinition<HarnessManifestCase, TOutput> CreateIterative<TOutput>(
        HarnessManifestCaseSource caseSource,
        ulong globalRunSeed,
        HarnessComparisonArmExecutor<TOutput> executor) =>
        Create(
            IterativeExperimentName,
            HarnessComparisonArm.Iterative,
            caseSource,
            globalRunSeed,
            executor);

    /// <summary>
    /// Creates the plain upstream Harness arm.
    /// </summary>
    /// <typeparam name="TOutput">The caller-owned normalized trial output type.</typeparam>
    /// <param name="caseSource">The frozen case source shared by all three arms.</param>
    /// <param name="globalRunSeed">The pinned global seed used to derive case/trial seeds.</param>
    /// <param name="executor">The injected plain Harness arm executor.</param>
    /// <returns>The provider-neutral plain Harness experiment definition.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="caseSource"/> or <paramref name="executor"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentDefinition<HarnessManifestCase, TOutput> CreatePlainHarness<TOutput>(
        HarnessManifestCaseSource caseSource,
        ulong globalRunSeed,
        HarnessComparisonArmExecutor<TOutput> executor) =>
        Create(
            PlainHarnessExperimentName,
            HarnessComparisonArm.PlainHarness,
            caseSource,
            globalRunSeed,
            executor);

    /// <summary>
    /// Creates the hybrid Harness/workspace arm.
    /// </summary>
    /// <typeparam name="TOutput">The caller-owned normalized trial output type.</typeparam>
    /// <param name="caseSource">The frozen case source shared by all three arms.</param>
    /// <param name="globalRunSeed">The pinned global seed used to derive case/trial seeds.</param>
    /// <param name="executor">The injected hybrid arm executor.</param>
    /// <returns>The provider-neutral hybrid experiment definition.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="caseSource"/> or <paramref name="executor"/> is <see langword="null"/>.
    /// </exception>
    public static ExperimentDefinition<HarnessManifestCase, TOutput> CreateHybrid<TOutput>(
        HarnessManifestCaseSource caseSource,
        ulong globalRunSeed,
        HarnessComparisonArmExecutor<TOutput> executor) =>
        Create(
            HybridExperimentName,
            HarnessComparisonArm.Hybrid,
            caseSource,
            globalRunSeed,
            executor);

    private static ExperimentDefinition<HarnessManifestCase, TOutput> Create<TOutput>(
        string experimentName,
        HarnessComparisonArm arm,
        HarnessManifestCaseSource caseSource,
        ulong globalRunSeed,
        HarnessComparisonArmExecutor<TOutput> executor)
    {
        ArgumentNullException.ThrowIfNull(caseSource);
        ArgumentNullException.ThrowIfNull(executor);

        return new ExperimentDefinition<HarnessManifestCase, TOutput>
        {
            Name = experimentName,
            CaseSource = caseSource,
            Task = (context, cancellationToken) =>
                executor(
                    new HarnessComparisonArmExecutionContext(
                        arm,
                        context,
                        DeriveTrialSeed(globalRunSeed, context.Case.Id, context.TrialIndex)),
                    cancellationToken),
        };
    }

    internal static string GetArmId(HarnessComparisonArm arm) =>
        arm switch
        {
            HarnessComparisonArm.Iterative => IterativeArmId,
            HarnessComparisonArm.PlainHarness => PlainHarnessArmId,
            HarnessComparisonArm.Hybrid => HybridArmId,
            _ => throw new ArgumentOutOfRangeException(
                nameof(arm),
                arm,
                "The comparison arm is not defined."),
        };

    private static ulong DeriveTrialSeed(
        ulong globalRunSeed,
        string caseId,
        int trialIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        if (trialIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trialIndex),
                trialIndex,
                "The trial index must be positive.");
        }

        var caseIdBytes = Encoding.UTF8.GetBytes(caseId);
        var input = new byte[sizeof(ulong) + sizeof(int) + caseIdBytes.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(input, globalRunSeed);
        BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(sizeof(ulong)), trialIndex);
        caseIdBytes.CopyTo(input.AsSpan(sizeof(ulong) + sizeof(int)));
        var digest = SHA256.HashData(input);
        return BinaryPrimitives.ReadUInt64LittleEndian(digest);
    }
}
