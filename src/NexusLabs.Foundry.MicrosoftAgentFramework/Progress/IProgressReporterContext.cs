namespace NexusLabs.Foundry.MicrosoftAgentFramework.Progress;

/// <summary>
/// Narrow, internal companion to <see cref="IProgressReporter"/> exposing the current reporter's
/// parent agent ID without adding a member to the public <see cref="IProgressReporter"/> contract.
/// A caller building a progress event should read <see cref="ParentAgentId"/> through an
/// <see langword="as"/> cast against the current reporter and fall back to <see langword="null"/>
/// when the reporter does not implement this interface (for example an arbitrary custom
/// <see cref="IProgressReporter"/> implementation), so ordinary event emission is never broken by a
/// reporter that predates this interface.
/// </summary>
internal interface IProgressReporterContext
{
    /// <summary>
    /// The parent agent ID this reporter was created with via <see cref="IProgressReporter.CreateChild"/>,
    /// or <see langword="null"/> for a workflow-level/root reporter with no parent.
    /// </summary>
    string? ParentAgentId { get; }
}
