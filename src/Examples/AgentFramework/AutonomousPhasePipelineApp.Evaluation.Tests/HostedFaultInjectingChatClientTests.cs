using System.Text.Json;

using AutonomousPhasePipelineApp.Core;
using AutonomousPhasePipelineApp.Evaluation;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedFaultInjectingChatClientTests
{
    [Fact]
    public async Task DelayTerminal_ActivatesAfterProviderResponse()
    {
        using var inner = new QueueChatClient(CreateValidArtifact());
        var telemetry = new HostedEvaluationTelemetry();
        var budget = new ReferenceSynthesisBudget(4);
        using var recording = new HostedRecordingChatClient(
            inner,
            telemetry,
            "synthesis-parent",
            isChild: false,
            budget);
        using var client = new HostedFaultInjectingChatClient(
            recording,
            new HostedFaultPlan(
                HostedFaultMode.DelayFirstTerminalUntilCanceled,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                HostedEvaluationAgentRole.SynthesisParent,
                MustActivate: true),
            new HostedFaultState(),
            HostedEvaluationAgentRole.SynthesisParent,
            telemetry);
        using var cancellation = new CancellationTokenSource();

        Task<ChatResponse> response = client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "cancel")],
            options: null,
            cancellation.Token);
        await telemetry.FaultActivated.Task.WaitAsync(
            TestContext.Current.CancellationToken);
        HostedEvaluationTelemetrySnapshot snapshot =
            telemetry.Snapshot();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => response);
        Assert.Equal(1, inner.CallCount);
        Assert.True(snapshot.FaultActivated);
        Assert.Equal(1, snapshot.CompletedModelCallsAtFault);
    }

    [Fact]
    public async Task InvalidFirstTerminal_RemovesOnlyFirstRecommendation()
    {
        string valid = CreateValidArtifact();
        using var inner = new QueueChatClient(valid, valid);
        var telemetry = new HostedEvaluationTelemetry();
        using var client = new HostedFaultInjectingChatClient(
            inner,
            new HostedFaultPlan(
                HostedFaultMode.InvalidFirstTerminal,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                HostedEvaluationAgentRole.SynthesisParent,
                MustActivate: true),
            new HostedFaultState(),
            HostedEvaluationAgentRole.SynthesisParent,
            telemetry);

        ChatResponse first = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "one")],
            options: null,
            TestContext.Current.CancellationToken);
        ChatResponse second = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "two")],
            options: null,
            TestContext.Current.CancellationToken);

        using JsonDocument firstDocument =
            JsonDocument.Parse(first.Text!);
        Assert.False(
            firstDocument.RootElement.TryGetProperty(
                "recommendation",
                out _));
        Assert.Equal(valid, second.Text);
        Assert.True(telemetry.Snapshot().FaultActivated);
    }

    [Fact]
    public async Task ForceFirstMagenticStall_ReplacesFirstLedger()
    {
        string ledger = MagenticScriptedResponses.CreateLedger(
            isRequestSatisfied: true,
            isInLoop: false,
            isProgressBeingMade: true,
            nextSpeaker: "ManifestAnalyst",
            instruction: "done");
        using var inner = new QueueChatClient(ledger);
        var telemetry = new HostedEvaluationTelemetry();
        using var client = new HostedFaultInjectingChatClient(
            inner,
            new HostedFaultPlan(
                HostedFaultMode.ForceFirstMagenticStall,
                HostedFaultActivationPoint.AfterMagenticProgressLedger,
                HostedEvaluationAgentRole.MagenticManager,
                MustActivate: true),
            new HostedFaultState(),
            HostedEvaluationAgentRole.MagenticManager,
            telemetry);

        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "progress")],
            options: null,
            TestContext.Current.CancellationToken);

        using JsonDocument document = JsonDocument.Parse(response.Text!);
        Assert.True(
            document.RootElement
                .GetProperty("is_in_loop")
                .GetProperty("answer")
                .GetBoolean());
        Assert.False(
            document.RootElement
                .GetProperty("is_progress_being_made")
                .GetProperty("answer")
                .GetBoolean());
        Assert.True(telemetry.Snapshot().FaultActivated);
    }

    [Fact]
    public async Task InvalidMagenticFinal_DoesNotMatchPlanningText()
    {
        string ledger = MagenticScriptedResponses.CreateLedger(
            isRequestSatisfied: true,
            isInLoop: false,
            isProgressBeingMade: true,
            nextSpeaker: "ManifestAnalyst",
            instruction: "done");
        string valid = CreateValidArtifact();
        using var inner = new QueueChatClient(
            "facts",
            "plan",
            ledger,
            valid);
        var telemetry = new HostedEvaluationTelemetry();
        using var client = new HostedFaultInjectingChatClient(
            inner,
            new HostedFaultPlan(
                HostedFaultMode.InvalidMagenticFinal,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                HostedEvaluationAgentRole.MagenticManager,
                MustActivate: true),
            new HostedFaultState(),
            HostedEvaluationAgentRole.MagenticManager,
            telemetry);

        ChatResponse facts = await RespondAsync(client);
        ChatResponse plan = await RespondAsync(client);
        ChatResponse progress = await RespondAsync(client);
        ChatResponse final = await RespondAsync(client);

        Assert.Equal("facts", facts.Text);
        Assert.Equal("plan", plan.Text);
        Assert.Equal(ledger, progress.Text);
        using JsonDocument finalDocument =
            JsonDocument.Parse(final.Text!);
        Assert.False(
            finalDocument.RootElement.TryGetProperty(
                "recommendation",
                out _));
        Assert.True(telemetry.Snapshot().FaultActivated);
    }

    [Fact]
    public async Task StreamingPath_UsesTheSameTypedFaultInjection()
    {
        using var inner = new QueueChatClient(CreateValidArtifact());
        var telemetry = new HostedEvaluationTelemetry();
        using var client = new HostedFaultInjectingChatClient(
            inner,
            new HostedFaultPlan(
                HostedFaultMode.InvalidFirstTerminal,
                HostedFaultActivationPoint.AfterTerminalProviderResponse,
                HostedEvaluationAgentRole.SynthesisParent,
                MustActivate: true),
            new HostedFaultState(),
            HostedEvaluationAgentRole.SynthesisParent,
            telemetry);
        var updates = new List<ChatResponseUpdate>();

        await foreach (ChatResponseUpdate update in
            client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "stream")],
                options: null,
                TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        ChatResponse response = updates.ToChatResponse();
        using JsonDocument document =
            JsonDocument.Parse(response.Text!);
        Assert.False(
            document.RootElement.TryGetProperty(
                "recommendation",
                out _));
        Assert.True(telemetry.Snapshot().FaultActivated);
    }

    [Fact]
    public async Task SharedFaultState_DoesNotReinjectFirstTerminalOnRetry()
    {
        string valid = CreateValidArtifact();
        var plan = new HostedFaultPlan(
            HostedFaultMode.InvalidFirstTerminal,
            HostedFaultActivationPoint.AfterTerminalProviderResponse,
            HostedEvaluationAgentRole.MagenticManager,
            MustActivate: true);
        var state = new HostedFaultState();
        var telemetry = new HostedEvaluationTelemetry();
        using var firstInner = new QueueChatClient(
            MagenticScriptedResponses.CreateLedger(
                false,
                false,
                true,
                "ManifestAnalyst",
                "continue"),
            valid);
        using var secondInner = new QueueChatClient(
            MagenticScriptedResponses.CreateLedger(
                true,
                false,
                true,
                "ManifestAnalyst",
                "complete"),
            valid);
        using var firstClient = new HostedFaultInjectingChatClient(
            firstInner,
            plan,
            state,
            HostedEvaluationAgentRole.MagenticManager,
            telemetry);
        using var secondClient = new HostedFaultInjectingChatClient(
            secondInner,
            plan,
            state,
            HostedEvaluationAgentRole.MagenticManager,
            telemetry);

        _ = await RespondAsync(firstClient);
        ChatResponse firstFinal = await RespondAsync(firstClient);
        _ = await RespondAsync(secondClient);
        ChatResponse secondFinal = await RespondAsync(secondClient);

        using JsonDocument firstDocument =
            JsonDocument.Parse(firstFinal.Text!);
        Assert.False(
            firstDocument.RootElement.TryGetProperty(
                "recommendation",
                out _));
        Assert.Equal(valid, secondFinal.Text);
    }

    private static Task<ChatResponse> RespondAsync(
        IChatClient client) =>
        client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "next")],
            options: null,
            TestContext.Current.CancellationToken);

    private static string CreateValidArtifact() =>
        """
        {
          "summary": "Synthesis",
          "evidence": ["artifact://sha256/research"],
          "gaps": [],
          "recommendation": "Proceed"
        }
        """;
}
