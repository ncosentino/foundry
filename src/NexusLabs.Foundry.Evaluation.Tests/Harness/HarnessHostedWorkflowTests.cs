namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessHostedWorkflowTests
{
    [Fact]
    public void Workflow_UsesOnlyManualAndScheduledTriggers()
    {
        var workflow = ReadRepositoryFile(".github/workflows/harness-evaluation.yml");

        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("schedule:", workflow);
        Assert.DoesNotContain("pull_request:", workflow);
        Assert.DoesNotContain("push:", workflow);
    }

    [Fact]
    public void Workflow_FreezesProtocolCapsAndHostedNonGatingBehavior()
    {
        var workflow = ReadRepositoryFile(".github/workflows/harness-evaluation.yml");

        Assert.Contains("runs-on: ubuntu-latest", workflow);
        Assert.Contains("timeout-minutes: 60", workflow);
        Assert.Contains("models: read", workflow);
        Assert.Contains("HARNESS_EVAL_PLANNED_RUNS: 72", workflow);
        Assert.Contains("HARNESS_EVAL_MAX_ATTEMPTS: 144", workflow);
        Assert.Contains("HARNESS_EVAL_MAX_REQUESTS_PER_ATTEMPT: 8", workflow);
        Assert.Contains("HARNESS_EVAL_MAX_RESERVED_REQUESTS: 1152", workflow);
        Assert.Contains("HARNESS_EVAL_SCHEDULING_DEADLINE_MINUTES: 50", workflow);
        Assert.Contains("HARNESS_EVAL_MAX_ATTEMPT_SECONDS: 120", workflow);
        Assert.Contains("HARNESS_EVAL_MAX_OUTPUT_TOKENS: 2000", workflow);
        Assert.Contains("HARNESS_EVAL_MAX_CONCURRENCY: 3", workflow);
        Assert.Contains("HARNESS_EVAL_COST_CAP_USD: 25", workflow);
        Assert.DoesNotContain("continue-on-error: true", workflow);
        Assert.Contains("if: always()", workflow);
        Assert.Contains("github.event_name == 'schedule'", workflow);
    }

    [Fact]
    public void Preflight_SeparatesQuotaProviderAndSuccessStates()
    {
        var script = ReadRepositoryFile("scripts/Invoke-HarnessEvaluationPreflight.ps1");
        var workflow = ReadRepositoryFile(".github/workflows/harness-evaluation.yml");

        Assert.Contains("https://models.github.ai/inference/chat/completions", script);
        Assert.Contains("QuotaNotConfirmed", script);
        Assert.Contains("Succeeded", script);
        Assert.Contains("Failed", script);
        Assert.Contains("confirmPaidModelsQuota", script);
        Assert.Contains("replay-smoke-response.json", script);
        Assert.Contains("checksums.sha256", script);
        Assert.Contains("reserved worst-case request budget", script);
        Assert.Contains("$env:GITHUB_TOKEN", script);
        Assert.DoesNotContain("-GitHubToken", workflow);
    }

    [Fact]
    public void GateEvidence_RecordsWorkflowAsNonRequired()
    {
        var evidence = ReadRepositoryFile(
            "specs/001-maf-harness-first-class/evidence/hosted-eval-gate.md");

        Assert.Contains("build-test-pack", evidence);
        Assert.Contains("docs", evidence);
        Assert.Contains("aot", evidence);
        Assert.Contains("Harness Evaluation", evidence);
        Assert.Contains("not a required status", evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegisteredCiWorkflow_ProvidesManualIntegrationDispatchBridge()
    {
        var workflow = ReadRepositoryFile(".github/workflows/ci.yml");

        Assert.Contains("run_harness_evaluation:", workflow);
        Assert.Contains("harness-evaluation-dispatch:", workflow);
        Assert.Contains("inputs.run_harness_evaluation", workflow);
        Assert.Contains(
            "if: github.event_name != 'workflow_dispatch' || !inputs.run_harness_evaluation",
            workflow);
        Assert.Contains("runs-on: ubuntu-latest", workflow);
        Assert.Contains("models: read", workflow);
        Assert.Contains("Invoke-HarnessEvaluationPreflight.ps1", workflow);
        Assert.Contains("if-no-files-found: warn", workflow);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }
}
