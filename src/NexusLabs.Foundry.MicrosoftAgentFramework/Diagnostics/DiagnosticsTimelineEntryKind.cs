namespace NexusLabs.Foundry.MicrosoftAgentFramework.Diagnostics;

/// <summary>
/// Identifies the kind of event a <see cref="DiagnosticsTimelineEntry"/> represents.
/// </summary>
public enum DiagnosticsTimelineEntryKind
{
    /// <summary>An LLM chat completion call.</summary>
    ChatCompletion,

    /// <summary>A tool invocation.</summary>
    ToolCall,
}
