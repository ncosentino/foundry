using System.Text.RegularExpressions;

using NexusLabs.Foundry.Evaluation.Experiments;

namespace NexusLabs.Foundry.Evaluation.Harness;

/// <summary>
/// A validating <see cref="IExperimentCaseSource{TCase}"/> over a frozen
/// <see cref="HarnessCaseSetManifest"/>. Construction fails fast when the manifest violates a frozen
/// invariant: the hosted (non-development) case IDs must be exactly <c>h001-01</c> through
/// <c>h001-08</c>, the hosted trial count must be three, every case ID must be unique, and every hosted
/// case must carry a well-formed deterministic completion reference. Development cases are validated
/// too but are excluded from the materialized cases by default.
/// </summary>
public sealed partial class HarnessManifestCaseSource : IExperimentCaseSource<HarnessManifestCase>
{
    /// <summary>The frozen hosted trial count for the <c>harness-001</c> v1.0 case set.</summary>
    public const int RequiredHostedTrialCount = 3;

    /// <summary>The frozen, ordered hosted case IDs for the <c>harness-001</c> v1.0 case set.</summary>
    public static readonly IReadOnlyList<string> RequiredHostedCaseIds =
    [
        "h001-01", "h001-02", "h001-03", "h001-04",
        "h001-05", "h001-06", "h001-07", "h001-08",
    ];

    private readonly ExperimentCase<HarnessManifestCase>[] _cases;
    private readonly ExperimentSourceReference _source;

    private HarnessManifestCaseSource(HarnessCaseSetManifest manifest, bool includeDevelopmentCases)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Manifest = manifest;
        Validate(manifest);

        _source = new ExperimentSourceReference { Name = $"{manifest.CaseSetId} {manifest.Version}" };

        var materialized = new List<ExperimentCase<HarnessManifestCase>>(manifest.Cases.Count);
        foreach (var manifestCase in manifest.Cases)
        {
            if (manifestCase.Development && !includeDevelopmentCases)
            {
                continue;
            }

            materialized.Add(new ExperimentCase<HarnessManifestCase>
            {
                Id = manifestCase.Id,
                Value = manifestCase,
                TrialCount = manifest.HostedTrialCount,
                Tags = manifestCase.Tags,
            });
        }

        _cases = [.. materialized];
    }

    /// <summary>Gets the validated manifest backing this source.</summary>
    public HarnessCaseSetManifest Manifest { get; }

    /// <summary>
    /// Creates a source over the supplied manifest, materializing only the hosted cases.
    /// </summary>
    /// <param name="manifest">The frozen manifest.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is <see langword="null"/>.</exception>
    /// <exception cref="HarnessCaseSetManifestException">The manifest violates a frozen invariant.</exception>
    public HarnessManifestCaseSource(HarnessCaseSetManifest manifest)
        : this(manifest, includeDevelopmentCases: false)
    {
    }

    /// <summary>
    /// Creates a source over the supplied manifest, materializing hosted and development cases.
    /// </summary>
    /// <param name="manifest">The frozen manifest.</param>
    /// <returns>The validated source including development cases.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is <see langword="null"/>.</exception>
    /// <exception cref="HarnessCaseSetManifestException">The manifest violates a frozen invariant.</exception>
    public static HarnessManifestCaseSource IncludingDevelopmentCases(HarnessCaseSetManifest manifest) =>
        new(manifest, includeDevelopmentCases: true);

    /// <summary>
    /// Parses a manifest from JSON and creates a source materializing only the hosted cases.
    /// </summary>
    /// <param name="json">The manifest JSON document text.</param>
    /// <returns>The validated source.</returns>
    /// <exception cref="ArgumentException"><paramref name="json"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="HarnessCaseSetManifestException">The JSON is invalid or violates a frozen invariant.</exception>
    public static HarnessManifestCaseSource FromJson(string json) =>
        new(HarnessCaseSetManifestSerializer.Deserialize(json), includeDevelopmentCases: false);

    /// <summary>
    /// Parses a manifest from JSON and creates a source materializing hosted and development cases.
    /// </summary>
    /// <param name="json">The manifest JSON document text.</param>
    /// <returns>The validated source including development cases.</returns>
    /// <exception cref="ArgumentException"><paramref name="json"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="HarnessCaseSetManifestException">The JSON is invalid or violates a frozen invariant.</exception>
    public static HarnessManifestCaseSource FromJsonIncludingDevelopmentCases(string json) =>
        new(HarnessCaseSetManifestSerializer.Deserialize(json), includeDevelopmentCases: true);

    /// <summary>
    /// Loads the materialized case collection without caller cancellation.
    /// </summary>
    /// <returns>The source identity and ordered cases.</returns>
    public ValueTask<ExperimentCaseSourceResult<HarnessManifestCase>> LoadAsync() =>
        LoadAsync(CancellationToken.None);

    /// <summary>
    /// Loads the materialized case collection with caller cancellation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The source identity and ordered cases.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public ValueTask<ExperimentCaseSourceResult<HarnessManifestCase>> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ExperimentCaseSourceResult<HarnessManifestCase>
        {
            Source = _source,
            Cases = Array.AsReadOnly(_cases),
        });
    }

    private static void Validate(HarnessCaseSetManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            throw new HarnessCaseSetManifestException("The manifest is missing a schema version.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CaseSetId))
        {
            throw new HarnessCaseSetManifestException("The manifest is missing a case-set ID.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new HarnessCaseSetManifestException("The manifest is missing a version.");
        }

        if (manifest.HostedTrialCount != RequiredHostedTrialCount)
        {
            throw new HarnessCaseSetManifestException(
                $"The hosted trial count must be exactly {RequiredHostedTrialCount}, but was {manifest.HostedTrialCount}.");
        }

        if (manifest.Cases is null || manifest.Cases.Count == 0)
        {
            throw new HarnessCaseSetManifestException("The manifest declares no cases.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var hostedIds = new List<string>();
        foreach (var manifestCase in manifest.Cases)
        {
            ValidateCase(manifestCase, seenIds);
            if (!manifestCase.Development)
            {
                hostedIds.Add(manifestCase.Id);
            }
        }

        ValidateHostedIds(hostedIds);
    }

    private static void ValidateCase(HarnessManifestCase manifestCase, HashSet<string> seenIds)
    {
        if (manifestCase is null)
        {
            throw new HarnessCaseSetManifestException("The manifest contains a null case.");
        }

        if (string.IsNullOrWhiteSpace(manifestCase.Id))
        {
            throw new HarnessCaseSetManifestException("A manifest case is missing its ID.");
        }

        if (!seenIds.Add(manifestCase.Id))
        {
            throw new HarnessCaseSetManifestException($"Duplicate case ID '{manifestCase.Id}' in the manifest.");
        }

        if (string.IsNullOrWhiteSpace(manifestCase.TaskCategory))
        {
            throw new HarnessCaseSetManifestException($"Case '{manifestCase.Id}' is missing a task category.");
        }

        if (manifestCase.DeterministicReferences is null)
        {
            throw new HarnessCaseSetManifestException(
                $"Case '{manifestCase.Id}' has a null deterministic reference collection.");
        }

        var seenDimensions = new HashSet<HarnessEvaluationDimension>();
        foreach (var reference in manifestCase.DeterministicReferences)
        {
            ValidateReference(manifestCase.Id, reference);
            if (!seenDimensions.Add(reference.Dimension))
            {
                throw new HarnessCaseSetManifestException(
                    $"Case '{manifestCase.Id}' declares more than one reference for dimension '{reference.Dimension}'.");
            }
        }

        // A hosted case must anchor at least the completion predicate; a development case need not.
        if (!manifestCase.Development && !seenDimensions.Contains(HarnessEvaluationDimension.Completion))
        {
            throw new HarnessCaseSetManifestException(
                $"Hosted case '{manifestCase.Id}' is missing a deterministic completion reference.");
        }
    }

    private static void ValidateReference(string caseId, HarnessDeterministicReference reference)
    {
        if (reference is null)
        {
            throw new HarnessCaseSetManifestException($"Case '{caseId}' contains a null deterministic reference.");
        }

        if (!Enum.IsDefined(reference.Dimension))
        {
            throw new HarnessCaseSetManifestException(
                $"Case '{caseId}' references an undefined dimension value '{reference.Dimension}'.");
        }

        if (string.IsNullOrWhiteSpace(reference.ReferenceId))
        {
            throw new HarnessCaseSetManifestException(
                $"Case '{caseId}' has a '{reference.Dimension}' reference with no reference ID.");
        }

        if (string.IsNullOrWhiteSpace(reference.RelativePath))
        {
            throw new HarnessCaseSetManifestException(
                $"Case '{caseId}' has a '{reference.Dimension}' reference with no relative path.");
        }

        if (reference.Sha256 is not null && !DigestPattern().IsMatch(reference.Sha256))
        {
            throw new HarnessCaseSetManifestException(
                $"Case '{caseId}' has a '{reference.Dimension}' reference with a malformed SHA-256 digest.");
        }
    }

    private static void ValidateHostedIds(List<string> hostedIds)
    {
        var hostedSet = new HashSet<string>(hostedIds, StringComparer.Ordinal);
        var requiredSet = new HashSet<string>(RequiredHostedCaseIds, StringComparer.Ordinal);

        if (!hostedSet.SetEquals(requiredSet))
        {
            var missing = requiredSet.Except(hostedSet).Order(StringComparer.Ordinal);
            var extra = hostedSet.Except(requiredSet).Order(StringComparer.Ordinal);
            throw new HarnessCaseSetManifestException(
                "The hosted case IDs must be exactly h001-01 through h001-08. " +
                $"Missing: [{string.Join(", ", missing)}]. Unexpected: [{string.Join(", ", extra)}].");
        }
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();
}
