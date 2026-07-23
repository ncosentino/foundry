using Microsoft.Agents.AI;

namespace HarnessCompatibilityProbe;

internal sealed class TraceAIContextProvider(
    LifecycleTrace lifecycleTrace) : AIContextProvider
{
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        lifecycleTrace.Add("context.provide");
        return ValueTask.FromResult(
            new AIContext
            {
                Instructions = "context-probe",
            });
    }

    protected override ValueTask StoreAIContextAsync(
        InvokedContext context,
        CancellationToken cancellationToken)
    {
        lifecycleTrace.Add("context.store");
        return ValueTask.CompletedTask;
    }
}
