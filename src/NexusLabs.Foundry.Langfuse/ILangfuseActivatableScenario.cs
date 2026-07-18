namespace NexusLabs.Foundry.Langfuse;

/// <summary>
/// Provides repeatable ambient activation for a scope-owned Langfuse scenario.
/// </summary>
internal interface ILangfuseActivatableScenario : ILangfuseScenario
{
    bool IsEnabled { get; }

    IDisposable? Activate();
}
