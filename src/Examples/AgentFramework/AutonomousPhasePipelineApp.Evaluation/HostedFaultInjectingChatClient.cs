using AutonomousPhasePipelineApp.Core;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Evaluation;

internal sealed class HostedFaultInjectingChatClient(
    IChatClient innerClient,
    HostedFaultMode mode) : DelegatingChatClient(innerClient)
{
    private int _callCount;
    private int _ledgerCount;
    private int _terminalCount;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        ChatMessage[] messageSnapshot = [.. messages];
        int call = Interlocked.Increment(ref _callCount);
        if (mode == HostedFaultMode.DelayFirstCallUntilCanceled &&
            call == 1)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        ChatResponse response;
        if (mode == HostedFaultMode.FailFirstTerminal &&
            Volatile.Read(ref _terminalCount) == 0)
        {
            response = await base.GetResponseAsync(
                messageSnapshot,
                options,
                cancellationToken);
            if (!HasFunctionCall(response))
            {
                Interlocked.Increment(ref _terminalCount);
                throw new InvalidOperationException(
                    "Injected synthesis failure after the macro checkpoint.");
            }

            return response;
        }

        response = await base.GetResponseAsync(
            messageSnapshot,
            options,
            cancellationToken);
        if (mode == HostedFaultMode.ForceFirstMagenticStall &&
            MagenticScriptedResponses.TryParseLedger(
                response.Text ?? string.Empty,
                out _) &&
            Interlocked.Increment(ref _ledgerCount) == 1)
        {
            return ReplaceText(
                response,
                MagenticScriptedResponses.CreateLedger(
                    isRequestSatisfied: false,
                    isInLoop: true,
                    isProgressBeingMade: false,
                    nextSpeaker: MagenticPhaseFactory.ManifestAnalystName,
                    instruction: "The current approach is ineffective."));
        }

        if (mode == HostedFaultMode.InvalidMagenticFinal &&
            IsMagenticFinalRequest(messageSnapshot))
        {
            return ReplaceText(
                response,
                ReferenceSynthesisArtifacts.Invalid);
        }

        if (!HasFunctionCall(response))
        {
            int terminal = Interlocked.Increment(ref _terminalCount);
            if (mode == HostedFaultMode.InvalidEveryTerminal ||
                (mode == HostedFaultMode.InvalidFirstTerminal &&
                 terminal == 1))
            {
                return ReplaceText(
                    response,
                    ReferenceSynthesisArtifacts.Invalid);
            }
        }

        return response;
    }

    private static bool HasFunctionCall(ChatResponse response) =>
        response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Any();

    private static bool IsMagenticFinalRequest(
        IEnumerable<ChatMessage> messages) =>
        messages
            .Select(message => message.Text)
            .OfType<string>()
            .Any(text => text.Contains(
                "Return one JSON object",
                StringComparison.Ordinal));

    private static ChatResponse ReplaceText(
        ChatResponse response,
        string text) =>
        new(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = response.ModelId,
            Usage = response.Usage,
            ResponseId = response.ResponseId,
            RawRepresentation = response.RawRepresentation,
            AdditionalProperties = response.AdditionalProperties,
        };
}
