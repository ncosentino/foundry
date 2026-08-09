using System.Text.Json;

using Microsoft.Extensions.AI;

namespace AutonomousPhasePipelineApp.Core;

internal static class MagenticScriptedResponses
{
    internal static bool TryParseLedger(
        string response,
        out MagenticLedgerSnapshot? snapshot)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement root = document.RootElement;
            if (!TryGetBoolean(
                    root,
                    "is_request_satisfied",
                    out bool satisfied) ||
                !TryGetBoolean(
                    root,
                    "is_in_loop",
                    out bool inLoop) ||
                !TryGetBoolean(
                    root,
                    "is_progress_being_made",
                    out bool progressing) ||
                !TryGetString(
                    root,
                    "next_speaker",
                    out string speaker) ||
                !TryGetString(
                    root,
                    "instruction_or_question",
                    out string instruction))
            {
                snapshot = null;
                return false;
            }

            snapshot = new MagenticLedgerSnapshot(
                satisfied,
                inLoop,
                progressing,
                speaker,
                instruction);
            return true;
        }
        catch (JsonException)
        {
            snapshot = null;
            return false;
        }
    }

    private static bool TryGetBoolean(
        JsonElement root,
        string propertyName,
        out bool value)
    {
        if (root.TryGetProperty(
                propertyName,
                out JsonElement slot) &&
            slot.TryGetProperty(
                "answer",
                out JsonElement answer) &&
            answer.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = answer.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetString(
        JsonElement root,
        string propertyName,
        out string value)
    {
        if (root.TryGetProperty(
                propertyName,
                out JsonElement slot) &&
            slot.TryGetProperty(
                "answer",
                out JsonElement answer) &&
            answer.ValueKind == JsonValueKind.String)
        {
            value = answer.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    internal static Func<IReadOnlyList<ChatMessage>, string> Static(
        string response) =>
        _ => response;

    internal static Func<IReadOnlyList<ChatMessage>, string>
        SynthesisArtifact(
            ReferenceArtifactStore artifacts,
            string runId,
            bool includeRecommendation) =>
        messages =>
        {
            string manifestId = ExtractManifestId(messages);
            ReferenceArtifactManifest manifest = artifacts.GetManifest(
                manifestId,
                runId);
            return ReferenceSynthesisArtifacts.Create(
                manifest,
                includeRecommendation);
        };

    internal static Func<IReadOnlyList<ChatMessage>, string>
        CorrectionAwareSynthesisArtifact(
            ReferenceArtifactStore artifacts,
            string runId) =>
        messages =>
        {
            bool corrected = messages
                .Select(message => message.Text)
                .OfType<string>()
                .Any(text => text.Contains(
                    ReferenceArtifactValidator.SynthesisCorrectionCode,
                    StringComparison.Ordinal));
            return SynthesisArtifact(
                artifacts,
                runId,
                includeRecommendation: corrected)(messages);
        };

    internal static Func<IReadOnlyList<ChatMessage>, string> Ledger(
        bool isRequestSatisfied,
        bool isInLoop,
        bool isProgressBeingMade,
        string nextSpeaker,
        Func<IReadOnlyList<ChatMessage>, string> instructionFactory) =>
        messages => CreateLedger(
            isRequestSatisfied,
            isInLoop,
            isProgressBeingMade,
            nextSpeaker,
            instructionFactory(messages));

    internal static string InstructionWithManifestId(
        IReadOnlyList<ChatMessage> messages,
        string participantName)
    {
        string manifestId = ExtractManifestId(messages);
        return
            $"Participant={participantName}\nmanifest_id={manifestId}\nReview the accepted artifact manifest.";
    }

    private static string ExtractManifestId(
        IReadOnlyList<ChatMessage> messages)
    {
        const string Prefix = "manifest_id=";
        string source = messages
            .Select(message => message.Text)
            .OfType<string>()
            .Last(text => text.Contains(
                Prefix,
                StringComparison.Ordinal));
        int start = source.IndexOf(Prefix, StringComparison.Ordinal);
        start += Prefix.Length;
        int end = source.IndexOf('\n', start);
        string manifestId = (end < 0
            ? source[start..]
            : source[start..end]).Trim();
        if (string.IsNullOrWhiteSpace(manifestId))
        {
            throw new InvalidOperationException(
                "The Magentic conversation contained an empty manifest ID.");
        }

        return manifestId;
    }

    private static string CreateLedger(
        bool isRequestSatisfied,
        bool isInLoop,
        bool isProgressBeingMade,
        string nextSpeaker,
        string instruction)
    {
        string satisfied = isRequestSatisfied ? "true" : "false";
        string inLoop = isInLoop ? "true" : "false";
        string progressing = isProgressBeingMade ? "true" : "false";
        string speakerJson = JsonSerializer.Serialize(nextSpeaker);
        string instructionJson = JsonSerializer.Serialize(instruction);
        return
            $$"""
            {
              "is_request_satisfied": { "answer": {{satisfied}}, "reason": "offline comparison" },
              "is_in_loop": { "answer": {{inLoop}}, "reason": "offline comparison" },
              "is_progress_being_made": { "answer": {{progressing}}, "reason": "offline comparison" },
              "next_speaker": { "answer": {{speakerJson}}, "reason": "offline comparison" },
              "instruction_or_question": { "answer": {{instructionJson}}, "reason": "offline comparison" }
            }
            """;
    }
}
