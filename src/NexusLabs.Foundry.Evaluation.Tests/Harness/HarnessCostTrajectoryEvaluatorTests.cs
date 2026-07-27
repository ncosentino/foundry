using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Foundry.Evaluation.Harness;
using NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

namespace NexusLabs.Foundry.Evaluation.Tests.Harness;

public sealed class HarnessCostTrajectoryEvaluatorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly HarnessCostAttributionEvaluator _cost = new();
    private readonly HarnessToolTrajectoryEvaluator _trajectory = new();

    // -------- Cost attribution --------

    [Fact]
    public async Task Cost_NullSlice_ReturnsEmpty()
    {
        var result = await HarnessEvaluatorTestHarness.RunAsync(_cost, new HarnessRunEvaluationEvidence(), _ct);
        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Cost_ValidCounters_ReportsValidAndSurfacesTotals()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            CostAttribution = new HarnessCostAttributionEvidence
            {
                ArtifactInputUtf8Bytes = 5000,
                ArtifactOutputUtf8Bytes = 82,
                ContextOriginalSize = 4000,
                ContextFinalSize = 900,
                AttributedTokenCost = 12345,
                MeasurementUnit = HarnessContextMeasurementUnit.Utf8Bytes,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_cost, evidence, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCostAttributionEvaluator.AttributionValidMetricName));
        Assert.Equal(12345, HarnessEvaluatorTestHarness.NumericValue(result, HarnessCostAttributionEvaluator.AttributedTokenCostMetricName));
        Assert.Equal(900, HarnessEvaluatorTestHarness.NumericValue(result, HarnessCostAttributionEvaluator.ContextFinalSizeMetricName));
    }

    [Fact]
    public async Task Cost_NegativeCounter_ReportsInvalid()
    {
        var evidence = new HarnessRunEvaluationEvidence
        {
            CostAttribution = new HarnessCostAttributionEvidence
            {
                ArtifactInputUtf8Bytes = 5000,
                ArtifactOutputUtf8Bytes = 82,
                ContextOriginalSize = 4000,
                ContextFinalSize = 900,
                AttributedTokenCost = -1,
                MeasurementUnit = HarnessContextMeasurementUnit.Utf8Bytes,
            },
        };

        var result = await HarnessEvaluatorTestHarness.RunAsync(_cost, evidence, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessCostAttributionEvaluator.AttributionValidMetricName));
    }

    // -------- Tool trajectory --------

    [Fact]
    public async Task Trajectory_MissingExpectation_ReturnsEmpty()
    {
        var contexts = new EvaluationContext[]
        {
            new AgentRunDiagnosticsContext(Diagnostics("read", "search")),
            new HarnessRunEvaluationContext(new HarnessRunEvaluationEvidence()),
        };

        var result = await HarnessEvaluatorTestHarness.RunWithContextsAsync(_trajectory, contexts, _ct);

        Assert.Empty(result.Metrics);
    }

    [Fact]
    public async Task Trajectory_RequiredInOrderNoForbidden_ReportsCompliant()
    {
        var expectation = new HarnessToolTrajectoryExpectation
        {
            RequiredToolSequence = ["read", "write"],
            ForbiddenTools = ["delete"],
        };
        var contexts = Contexts(expectation, "read", "search", "write");

        var result = await HarnessEvaluatorTestHarness.RunWithContextsAsync(_trajectory, contexts, _ct);

        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.RequiredToolsPresentMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.ForbiddenToolsAbsentMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.TrajectoryCompliantMetricName));
    }

    [Fact]
    public async Task Trajectory_ReusesBaseTrajectoryMetrics()
    {
        var expectation = new HarnessToolTrajectoryExpectation
        {
            RequiredToolSequence = ["read"],
            ForbiddenTools = [],
        };
        var contexts = Contexts(expectation, "read", "write");

        var result = await HarnessEvaluatorTestHarness.RunWithContextsAsync(_trajectory, contexts, _ct);

        // Evidence reused from ToolCallTrajectoryEvaluator is merged into the same result.
        Assert.Equal(2, HarnessEvaluatorTestHarness.NumericValue(result, ToolCallTrajectoryEvaluator.TotalMetricName));
        Assert.True(HarnessEvaluatorTestHarness.BooleanValue(result, ToolCallTrajectoryEvaluator.AllSucceededMetricName));
    }

    [Fact]
    public async Task Trajectory_RequiredOutOfOrder_ReportsMissing()
    {
        var expectation = new HarnessToolTrajectoryExpectation
        {
            RequiredToolSequence = ["read", "write"],
            ForbiddenTools = [],
        };
        var contexts = Contexts(expectation, "write", "read");

        var result = await HarnessEvaluatorTestHarness.RunWithContextsAsync(_trajectory, contexts, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.RequiredToolsPresentMetricName));
        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.TrajectoryCompliantMetricName));
    }

    [Fact]
    public async Task Trajectory_ForbiddenPresent_ReportsViolation()
    {
        var expectation = new HarnessToolTrajectoryExpectation
        {
            RequiredToolSequence = ["read"],
            ForbiddenTools = ["delete"],
        };
        var contexts = Contexts(expectation, "read", "delete");

        var result = await HarnessEvaluatorTestHarness.RunWithContextsAsync(_trajectory, contexts, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.ForbiddenToolsAbsentMetricName));
        Assert.Equal(1, HarnessEvaluatorTestHarness.NumericValue(result, HarnessToolTrajectoryEvaluator.ForbiddenInvocationCountMetricName));
    }

    [Fact]
    public async Task Trajectory_ExpectationWithoutDiagnostics_TreatsObservedAsEmpty()
    {
        var expectation = new HarnessToolTrajectoryExpectation
        {
            RequiredToolSequence = ["read"],
            ForbiddenTools = [],
        };
        var contexts = new EvaluationContext[]
        {
            new HarnessRunEvaluationContext(new HarnessRunEvaluationEvidence { ToolTrajectory = expectation }),
        };

        var result = await HarnessEvaluatorTestHarness.RunWithContextsAsync(_trajectory, contexts, _ct);

        Assert.False(HarnessEvaluatorTestHarness.BooleanValue(result, HarnessToolTrajectoryEvaluator.RequiredToolsPresentMetricName));
    }

    private static EvaluationContext[] Contexts(HarnessToolTrajectoryExpectation expectation, params string[] toolNames) =>
    [
        new AgentRunDiagnosticsContext(Diagnostics(toolNames)),
        new HarnessRunEvaluationContext(new HarnessRunEvaluationEvidence { ToolTrajectory = expectation }),
    ];

    private static IAgentRunDiagnostics Diagnostics(params string[] toolNames)
    {
        var toolCalls = new List<ToolCallDiagnostics>();
        for (var i = 0; i < toolNames.Length; i++)
        {
            toolCalls.Add(new ToolCallDiagnostics(
                Sequence: i,
                ToolName: toolNames[i],
                Duration: TimeSpan.FromMilliseconds(1),
                Succeeded: true,
                ErrorMessage: null,
                StartedAt: DateTimeOffset.UnixEpoch,
                CompletedAt: DateTimeOffset.UnixEpoch,
                CustomMetrics: null));
        }

        return new FakeAgentRunDiagnostics
        {
            AgentName = "agent-1",
            TotalDuration = TimeSpan.FromMilliseconds(10),
            AggregateTokenUsage = new TokenUsage(0, 0, 0, 0, 0),
            ChatCompletions = [],
            ToolCalls = toolCalls,
            TotalInputMessages = 1,
            TotalOutputMessages = 1,
            InputMessages = [new ChatMessage(ChatRole.User, "Hello.")],
            OutputResponse = null,
            Succeeded = true,
            ErrorMessage = null,
            StartedAt = DateTimeOffset.UnixEpoch,
            CompletedAt = DateTimeOffset.UnixEpoch,
            ExecutionMode = "Hybrid",
        };
    }
}
