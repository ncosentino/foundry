using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using NexusLabs.Foundry.MicrosoftAgentFramework;

namespace HarnessCompatibilityProbe;

internal static class ApprovalProbeRunner
{
    internal static async Task<bool> RunAsync(AIFunction function)
    {
        var approved = await RunScenarioAsync(function, approved: true);
        var rejected = await RunScenarioAsync(function, approved: false);
        return approved && rejected;
    }

    private static async Task<bool> RunScenarioAsync(
        AIFunction function,
        bool approved)
    {
        var invocationCount = 0;
        var options = new HarnessAgentOptions
        {
            Name = approved ? "approval-approved-probe" : "approval-rejected-probe",
            HarnessInstructions = string.Empty,
            DisableAgentModeProvider = true,
            DisableAgentSkillsProvider = true,
            DisableCompaction = true,
            DisableFileMemory = true,
            DisableOpenTelemetry = true,
            DisableTodoProvider = true,
            DisableWebSearch = true,
            MaximumIterationsPerRequest = 4,
            ChatOptions = new ChatOptions
            {
                Tools = [new ApprovalRequiredAIFunction(function)],
            },
        };

        var agent = new ProbeChatClient(function.Name).AsHarnessAgent(options);
        var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
        if (functionInvokingChatClient is null ||
            agent.GetService<ToolApprovalAgent>() is null)
        {
            return false;
        }

        functionInvokingChatClient.FunctionInvoker = async (context, cancellationToken) =>
        {
            Interlocked.Increment(ref invocationCount);
            return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
        };

        var session = await agent.CreateSessionAsync();
        var pendingResponse = await agent.RunAsync(
            "Execute the generated probe tool.",
            session);
        var request = pendingResponse.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>()
            .SingleOrDefault();

        if (request?.ToolCall is not FunctionCallContent call ||
            !string.Equals(call.CallId, "probe-call", StringComparison.Ordinal) ||
            !string.Equals(call.Name, "Echo", StringComparison.Ordinal) ||
            invocationCount != 0)
        {
            return false;
        }

        var approvalMessage = new ChatMessage(
            ChatRole.User,
            [request.CreateResponse(approved, approved ? "approved" : "rejected")]);
        var finalResponse = await agent.RunAsync(approvalMessage, session);
        var finalText = finalResponse.GetText();
        Console.WriteLine(
            $"APPROVAL:{(approved ? "approved" : "rejected")}:request={call.CallId}:invocations={invocationCount}:result={finalText}");

        if (approved)
        {
            if (invocationCount != 1 ||
                !string.Equals(finalText, "tool-result:aot", StringComparison.Ordinal))
            {
                return false;
            }
        }
        else if (invocationCount != 0 ||
            !string.Equals(
                finalText,
                "tool-result:Tool call invocation rejected. rejected",
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
