using NexusLabs.Foundry.Evaluation.Harness;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessCaseSetManifestTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void Serialize_Deserialize_RoundTripsEquivalentManifest()
    {
        var manifest = BuildValidManifest(includeDevelopmentCase: true);

        var json = HarnessCaseSetManifestSerializer.Serialize(manifest);
        var roundTripped = HarnessCaseSetManifestSerializer.Deserialize(json);

        // Records compare list properties by reference, so assert structural round-trip fidelity by
        // comparing the canonical re-serialized JSON of the deserialized manifest.
        Assert.Equal(json, HarnessCaseSetManifestSerializer.Serialize(roundTripped));
        Assert.Equal(manifest.CaseSetId, roundTripped.CaseSetId);
        Assert.Equal(manifest.Version, roundTripped.Version);
        Assert.Equal(manifest.HostedTrialCount, roundTripped.HostedTrialCount);
        Assert.Equal(manifest.Cases.Count, roundTripped.Cases.Count);
        Assert.Equal(
            manifest.Cases[0].DeterministicReferences[0].Dimension,
            roundTripped.Cases[0].DeterministicReferences[0].Dimension);
    }

    [Fact]
    public void Serialize_EmitsEnumMembersAsStrings()
    {
        var json = HarnessCaseSetManifestSerializer.Serialize(BuildValidManifest());

        Assert.Contains("\"Completion\"", json);
        Assert.DoesNotContain("\"dimension\": 0", json);
    }

    [Fact]
    public async Task FromJson_ValidManifest_MaterializesOnlyHostedCases()
    {
        var json = HarnessCaseSetManifestSerializer.Serialize(BuildValidManifest(includeDevelopmentCase: true));

        var source = HarnessManifestCaseSource.FromJson(json);
        var result = await source.LoadAsync(_ct);

        Assert.Equal(HarnessManifestCaseSource.RequiredHostedCaseIds, result.Cases.Select(c => c.Id).ToArray());
        Assert.All(result.Cases, c => Assert.Equal(HarnessManifestCaseSource.RequiredHostedTrialCount, c.TrialCount));
        Assert.DoesNotContain(result.Cases, c => c.Value.Development);
    }

    [Fact]
    public async Task FromJsonIncludingDevelopmentCases_MaterializesDevelopmentCases()
    {
        var json = HarnessCaseSetManifestSerializer.Serialize(BuildValidManifest(includeDevelopmentCase: true));

        var source = HarnessManifestCaseSource.FromJsonIncludingDevelopmentCases(json);
        var result = await source.LoadAsync(_ct);

        Assert.Equal(9, result.Cases.Count);
        Assert.Contains(result.Cases, c => c.Value.Development);
    }

    [Fact]
    public void Construct_WrongTrialCount_Throws()
    {
        var manifest = BuildValidManifest() with { HostedTrialCount = 5 };

        var ex = Assert.Throws<HarnessCaseSetManifestException>(() => new HarnessManifestCaseSource(manifest));
        Assert.Contains("trial count", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construct_MissingHostedId_Throws()
    {
        var cases = BuildValidManifest().Cases.Where(c => c.Id != "h001-08").ToArray();
        var manifest = BuildValidManifest() with { Cases = cases };

        var ex = Assert.Throws<HarnessCaseSetManifestException>(() => new HarnessManifestCaseSource(manifest));
        Assert.Contains("h001-08", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Construct_ExtraHostedId_Throws()
    {
        var cases = BuildValidManifest().Cases.ToList();
        cases.Add(new HarnessManifestCase
        {
            Id = "h001-09",
            TaskCategory = "extra",
            Development = false,
            DeterministicReferences = [CompletionReference("h001-09")],
        });
        var manifest = BuildValidManifest() with { Cases = cases };

        var ex = Assert.Throws<HarnessCaseSetManifestException>(() => new HarnessManifestCaseSource(manifest));
        Assert.Contains("h001-09", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Construct_DuplicateCaseId_Throws()
    {
        var cases = BuildValidManifest().Cases.ToList();
        cases[1] = cases[1] with { Id = "h001-01" };
        var manifest = BuildValidManifest() with { Cases = cases };

        var ex = Assert.Throws<HarnessCaseSetManifestException>(() => new HarnessManifestCaseSource(manifest));
        Assert.Contains("Duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construct_HostedCaseMissingCompletionReference_Throws()
    {
        var cases = BuildValidManifest().Cases.ToList();
        cases[0] = cases[0] with { DeterministicReferences = [] };
        var manifest = BuildValidManifest() with { Cases = cases };

        var ex = Assert.Throws<HarnessCaseSetManifestException>(() => new HarnessManifestCaseSource(manifest));
        Assert.Contains("completion", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construct_MalformedDigest_Throws()
    {
        var cases = BuildValidManifest().Cases.ToList();
        cases[0] = cases[0] with
        {
            DeterministicReferences =
            [
                CompletionReference("h001-01") with { Sha256 = "NOT-A-DIGEST" },
            ],
        };
        var manifest = BuildValidManifest() with { Cases = cases };

        var ex = Assert.Throws<HarnessCaseSetManifestException>(() => new HarnessManifestCaseSource(manifest));
        Assert.Contains("digest", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construct_WellFormedDigest_IsAccepted()
    {
        var digest = new string('a', 64);
        var cases = BuildValidManifest().Cases.ToList();
        cases[0] = cases[0] with
        {
            DeterministicReferences = [CompletionReference("h001-01") with { Sha256 = digest }],
        };
        var manifest = BuildValidManifest() with { Cases = cases };

        var source = new HarnessManifestCaseSource(manifest);

        Assert.Equal(digest, source.Manifest.Cases[0].DeterministicReferences[0].Sha256);
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        Assert.Throws<HarnessCaseSetManifestException>(() => HarnessManifestCaseSource.FromJson("{ not json"));
    }

    [Fact]
    public async Task OnDiskManifest_SatisfiesFrozenInvariants()
    {
        var json = HarnessManifestTestFiles.TryReadManifestJson();
        Assert.SkipWhen(json is null, "The on-disk harness-001 v1.0 manifest was not found from the test output directory.");

        var manifest = HarnessCaseSetManifestSerializer.Deserialize(json!);
        Assert.Equal("harness-001", manifest.CaseSetId);
        Assert.Equal("v1.0", manifest.Version);
        Assert.Equal(HarnessManifestCaseSource.RequiredHostedTrialCount, manifest.HostedTrialCount);

        var hosted = await HarnessManifestCaseSource.FromJson(json!).LoadAsync(_ct);
        Assert.Equal(HarnessManifestCaseSource.RequiredHostedCaseIds, hosted.Cases.Select(c => c.Id).ToArray());

        var withDev = await HarnessManifestCaseSource.FromJsonIncludingDevelopmentCases(json!).LoadAsync(_ct);
        Assert.True(withDev.Cases.Count >= hosted.Cases.Count);
    }

    private static HarnessDeterministicReference CompletionReference(string caseId) => new()
    {
        Dimension = HarnessEvaluationDimension.Completion,
        ReferenceId = $"harness-001/v1.0/{caseId}/completion",
        RelativePath = $"cases/{caseId}/completion.json",
    };

    private static HarnessCaseSetManifest BuildValidManifest(bool includeDevelopmentCase = false)
    {
        var cases = new List<HarnessManifestCase>();
        foreach (var id in HarnessManifestCaseSource.RequiredHostedCaseIds)
        {
            cases.Add(new HarnessManifestCase
            {
                Id = id,
                TaskCategory = "test-category",
                Development = false,
                Tags = ["hosted"],
                DeterministicReferences = [CompletionReference(id)],
            });
        }

        if (includeDevelopmentCase)
        {
            cases.Add(new HarnessManifestCase
            {
                Id = "h001-dev-01",
                TaskCategory = "development",
                Development = true,
                DeterministicReferences = [CompletionReference("h001-dev-01")],
            });
        }

        return new HarnessCaseSetManifest
        {
            SchemaVersion = "1.0",
            CaseSetId = "harness-001",
            Version = "v1.0",
            HostedTrialCount = 3,
            Cases = cases,
        };
    }
}
