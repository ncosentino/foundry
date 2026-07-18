using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Foundry.Langfuse.Tests;

/// <summary>
/// Supplies one dynamically produced artifact to conformance evaluators.
/// </summary>
internal sealed class ConformanceEvaluationContext(string artifact)
    : EvaluationContext("artifact", [])
{
    public string Artifact { get; } = artifact;
}
