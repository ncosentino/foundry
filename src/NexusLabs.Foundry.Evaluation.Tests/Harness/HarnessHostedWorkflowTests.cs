using System.Text.Json;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessHostedWorkflowTests
{
    [Fact]
    public void Workflow_UsesOnlyManualAndScheduledTriggers()
    {
        var workflow = ReadRepositoryFile(".github/workflows/harness-evaluation.yml");

        Assert.Contains("workflow_dispatch:", workflow);
        Assert.Contains("schedule:", workflow);
        Assert.Contains("confirm_copilot_enterprise_billing:", workflow);
        Assert.DoesNotContain("confirm_paid_models_quota:", workflow);
        Assert.DoesNotContain("pull_request:", workflow);
        Assert.DoesNotContain("push:", workflow);
    }

    [Fact]
    public void Workflow_FreezesProtocolCapsAndHostedNonGatingBehavior()
    {
        var workflow = ReadRepositoryFile(".github/workflows/harness-evaluation.yml");

        Assert.Contains("runs-on: [self-hosted, linux, x64, general-purpose]", workflow);
        Assert.DoesNotContain("runs-on: ubuntu-latest", workflow);
        Assert.Contains("timeout-minutes: 60", workflow);
        Assert.Contains("copilot-requests: write", workflow);
        Assert.DoesNotContain("models: read", workflow);
        Assert.Contains("default: gpt-5-mini", workflow);
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
        Assert.Contains("HarnessEvaluationApp/HarnessEvaluationApp.csproj", workflow);
    }

    [Fact]
    public void CopilotPricing_FreezesModelAndConservativeReservation()
    {
        using var pricing = JsonDocument.Parse(ReadRepositoryFile(
            "artifacts/eval/case-sets/harness-001/v1.0/pricing/github-copilot.v1.json"));
        var root = pricing.RootElement;
        var model = Assert.Single(root.GetProperty("models").EnumerateArray());

        Assert.Equal("GitHub Copilot Enterprise", root.GetProperty("billingProduct").GetString());
        Assert.Equal("gpt-5-mini", model.GetProperty("modelId").GetString());
        Assert.Equal(2000, model.GetProperty("maximumOutputTokensPerRequest").GetInt32());
        Assert.Equal(4000, model.GetProperty("minimumRequestIntervalMilliseconds").GetInt32());
        Assert.Equal(0.02m, model.GetProperty("reservedWorstCaseUsdPerRequest").GetDecimal());
    }

    [Fact]
    public void Preflight_RequiresPitCrewAndCopilotBillingWithoutInferenceSmoke()
    {
        var script = ReadRepositoryFile("scripts/Invoke-HarnessEvaluationPreflight.ps1");
        var workflow = ReadRepositoryFile(".github/workflows/harness-evaluation.yml");

        Assert.Contains("CopilotBillingNotConfirmed", script);
        Assert.Contains("Ready", script);
        Assert.Contains("Failed", script);
        Assert.Contains("confirmCopilotEnterpriseBilling", script);
        Assert.Contains("github-copilot.v1.json", script);
        Assert.Contains("HARNESS_EVAL_RUNNER_ENVIRONMENT", script);
        Assert.Contains("self-hosted", script);
        Assert.Contains("checksums.sha256", script);
        Assert.Contains("reserved worst-case request budget", script);
        Assert.Contains("$env:GITHUB_TOKEN", script);
        Assert.DoesNotContain("Invoke-RestMethod", script);
        Assert.DoesNotContain("models.github.ai", script);
        Assert.DoesNotContain("replay-smoke-response.json", script);
        Assert.DoesNotContain("-GitHubToken", workflow);
        Assert.Contains("Run Copilot SDK provider probe", workflow);
        Assert.Contains("--provider-probe", workflow);
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
        Assert.Contains("confirm_copilot_enterprise_billing:", workflow);
        Assert.DoesNotContain("confirm_paid_models_quota:", workflow);
        Assert.Contains("harness-evaluation-dispatch:", workflow);
        Assert.Contains("inputs.run_harness_evaluation", workflow);
        Assert.Contains(
            "if: github.event_name != 'workflow_dispatch' || !inputs.run_harness_evaluation",
            workflow);
        Assert.Contains("runs-on: [self-hosted, linux, x64, general-purpose]", workflow);
        Assert.DoesNotContain("runs-on: ubuntu-latest", workflow);
        Assert.Contains("copilot-requests: write", workflow);
        Assert.DoesNotContain("models: read", workflow);
        Assert.Contains("Invoke-HarnessEvaluationPreflight.ps1", workflow);
        Assert.Contains("HarnessEvaluationApp/HarnessEvaluationApp.csproj", workflow);
        Assert.Contains("if-no-files-found: warn", workflow);
    }

    [Fact]
    public void HostedDriver_UsesOfficialCopilotSdkWithExplicitWorkflowToken()
    {
        var program = ReadRepositoryFile(
            "src/Examples/AgentFramework/HarnessEvaluationApp/Program.cs");
        var project = ReadRepositoryFile(
            "src/Examples/AgentFramework/HarnessEvaluationApp/HarnessEvaluationApp.csproj");
        var client = ReadRepositoryFile(
            "src/Examples/AgentFramework/HarnessEvaluationApp/CopilotSdkChatClient.cs");
        var executor = ReadRepositoryFile(
            "src/Examples/AgentFramework/HarnessEvaluationApp/CopilotSdkTurnExecutor.cs");
        var collector = ReadRepositoryFile(
            "src/Examples/AgentFramework/HarnessEvaluationApp/CopilotSdkTurnCollector.cs");

        Assert.Contains("new CopilotClient", program);
        Assert.Contains("new CopilotSdkChatClient", program);
        Assert.Contains("GitHubToken = token", program);
        Assert.Contains("CopilotClientMode.Empty", program);
        Assert.Contains("GitHub.Copilot.SDK", project);
        Assert.DoesNotContain("NexusLabs.Foundry.Copilot.csproj", project);
        Assert.Contains("AsDeclarationOnly", executor);
        Assert.Contains("ExternalToolRequestedEvent", collector);
        Assert.Contains("ModelCapabilitiesOverrideLimits", executor);
        Assert.Contains("BuildTranscriptJson", client);
        Assert.DoesNotContain("OpenAIClient", program);
        Assert.DoesNotContain("models.github.ai", program);
        Assert.DoesNotContain("new CopilotChatClient", program);
        Assert.DoesNotContain("<PackageReference Include=\"OpenAI\"", project);
        Assert.DoesNotContain("<PackageReference Include=\"Microsoft.Extensions.AI.OpenAI\"", project);
    }

    [Fact]
    public void LiveCopilotTests_RequireExplicitPitCrewActionsOptIn()
    {
        var copilotTests = ReadRepositoryFile(
            "src/NexusLabs.Foundry.Copilot.Tests/IntegrationSmokeTests.cs");
        var evaluationTest = ReadRepositoryFile(
            "src/NexusLabs.Foundry.Evaluation.Tests/CopilotSmokeTests.cs");
        var copilotGuard = ReadRepositoryFile(
            "src/NexusLabs.Foundry.Copilot.Tests/LiveCopilotTestGuard.cs");
        var evaluationGuard = ReadRepositoryFile(
            "src/NexusLabs.Foundry.Evaluation.Tests/LiveCopilotTestGuard.cs");

        Assert.Contains("LiveCopilotTestGuard.RequirePitCrewOptIn();", copilotTests);
        Assert.Contains("LiveCopilotTestGuard.RequirePitCrewOptIn();", evaluationTest);
        foreach (var guard in new[] { copilotGuard, evaluationGuard })
        {
            Assert.Contains("GITHUB_ACTIONS", guard);
            Assert.Contains("FOUNDRY_LIVE_COPILOT_RUNNER", guard);
            Assert.Contains("FOUNDRY_ALLOW_LIVE_COPILOT_TESTS", guard);
            Assert.Contains("\"pitcrew\"", guard);
            Assert.Contains("Assert.Skip", guard);
        }
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
