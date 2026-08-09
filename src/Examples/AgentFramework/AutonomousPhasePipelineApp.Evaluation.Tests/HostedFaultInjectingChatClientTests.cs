using System.Text.Json;

using AutonomousPhasePipelineApp.Core;
using AutonomousPhasePipelineApp.Evaluation;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation.Tests;

public sealed class HostedFaultInjectingChatClientTests
{
    [Fact]
    public async Task InvalidFirstTerminal_ReplacesOnlyFirstPlainResponse()
    {
        using var inner = new QueueChatClient(
            "first",
            "second");
        using var client = new HostedFaultInjectingChatClient(
            inner,
            HostedFaultMode.InvalidFirstTerminal);

        ChatResponse first = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "one")],
            options: null,
            TestContext.Current.CancellationToken);
        ChatResponse second = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "two")],
            options: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ReferenceSynthesisArtifacts.Invalid, first.Text);
        Assert.Equal("second", second.Text);
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
        using var client = new HostedFaultInjectingChatClient(
            inner,
            HostedFaultMode.ForceFirstMagenticStall);

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
    }
}
