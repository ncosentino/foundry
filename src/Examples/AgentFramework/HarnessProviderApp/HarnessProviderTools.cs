using System.ComponentModel;

using NexusLabs.Foundry.MicrosoftAgentFramework;
using NexusLabs.Foundry.MicrosoftAgentFramework.Context;
using NexusLabs.Foundry.MicrosoftAgentFramework.Workspace;

namespace HarnessProviderApp;

/// <summary>
/// Source-generated workspace tools the Harness agent can call. These run in-process and never
/// reach the network, so the same tools behave identically under the scripted and Copilot
/// providers.
/// </summary>
[AgentFunctionGroup("harness-provider")]
internal sealed class HarnessProviderTools(
    IAgentExecutionContextAccessor contextAccessor)
{
    [AgentFunction]
    [Description("Writes a note to the agent workspace at the supplied relative path.")]
    public string WriteNote(
        [Description("Workspace-relative path, for example notes/summary.md.")] string path,
        [Description("The text content to persist.")] string content)
    {
        var workspace = RequireWorkspace();
        var write = workspace.TryWriteFile(path, content);
        if (!write.Success)
        {
            throw new InvalidOperationException(
                $"The workspace write to '{path}' failed.",
                write.Exception);
        }

        return $"wrote {content.Length} characters to {path}";
    }

    [AgentFunction]
    [Description("Reads a note previously written to the agent workspace.")]
    public string ReadNote(
        [Description("Workspace-relative path to read.")] string path)
    {
        var workspace = RequireWorkspace();
        var read = workspace.TryReadFile(path);
        return read.Success
            ? read.Value.Content
            : $"no file exists at {path}";
    }

    private IWorkspace RequireWorkspace()
    {
        var context = contextAccessor.Current
            ?? throw new InvalidOperationException("No execution context is active.");
        return context.GetWorkspace()
            ?? throw new InvalidOperationException("No workspace is authorized.");
    }
}
